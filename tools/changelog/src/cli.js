import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { config, categories } from './config.js';
import { repositoryRoot, readCommits, rangeShas } from './git.js';
import { readVersions } from './versions.js';
import { developerVersions } from './commits.js';
import { validateOverrides, playerVersions } from './players.js';
import { renderDev } from './render-dev.js';
import {
  playersDocument,
  renderPlayers,
  renderReleaseNotes,
  validatePlayersDocument,
  categoryMarkdown,
} from './render-players.js';

const usage = 'Usage: node src/cli.js <generate|check|preview|release-notes> '
  + '[--root R] [--ref REF] [--repo-url URL] [--strict] [--only dev|players|all] '
  + '[--range A..B] [--tag vX.Y.Z]';

const allowedFlags = {
  generate: ['root', 'ref', 'repo-url', 'strict', 'only'],
  check: ['root', 'ref', 'repo-url', 'strict', 'range'],
  preview: ['root', 'ref', 'repo-url', 'strict', 'range'],
  'release-notes': ['root', 'ref', 'repo-url', 'strict', 'tag'],
};

function validateOptions(command, options) {
  const invalidOnly = options.only && !['dev', 'players', 'all'].includes(options.only);
  const missingTag = command === 'release-notes' && !options.tag;
  const invalidRange = options.range && !/^[^-].*\.\.[^-].*$/.test(options.range);

  if (invalidOnly || missingTag || invalidRange || options.ref?.startsWith('-')) {
    throw Error('usage');
  }
}

function parseArguments(args) {
  const command = args.shift();
  const options = {};

  if (!allowedFlags[command]) {
    throw Error('usage');
  }

  while (args.length > 0) {
    const flag = args.shift();
    const key = flag.startsWith('--') ? flag.slice(2) : '';

    if (!allowedFlags[command].includes(key) || key in options) {
      throw Error('usage');
    }

    if (key === 'strict') {
      options[key] = true;
      continue;
    }

    const value = args.shift();
    if (!value || value.startsWith('--')) {
      throw Error('usage');
    }

    options[key] = value;
  }

  validateOptions(command, options);
  return { command, options };
}

function readText(path) {
  try {
    return readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
  } catch (error) {
    if (error.code === 'ENOENT') {
      return null;
    }

    throw error;
  }
}

function readOverrides(root, settings, history) {
  const rawDocument = readText(resolve(root, settings.overridesPath));
  if (rawDocument === null) {
    throw Error(`Missing overrides file: ${settings.overridesPath}`);
  }

  return validateOverrides(JSON.parse(rawDocument), history);
}

function warningScope(command, options, root, players, overrides) {
  const pending = players.versions.at(-1);
  const range = options.range;

  if (command === 'release-notes') {
    const release = players.versions.find(version => version.tag === options.tag);
    const releaseShas = new Set((release?.commits ?? []).map(commit => commit.sha));

    return {
      pending,
      warnings: players.warnings.filter(warning => releaseShas.has(warning.sha)),
    };
  }

  if (!range) {
    return { pending, warnings: players.warnings };
  }

  const shas = rangeShas(root, range);
  const rangePlayers = playerVersions([
    { version: 'Unreleased', commits: readCommits(root, range) },
  ], overrides);

  if (command === 'preview') {
    return { pending: rangePlayers.versions[0], warnings: rangePlayers.warnings };
  }

  return {
    pending,
    warnings: players.warnings.filter(warning => shas.has(warning.sha)),
  };
}

function printPreview(pending) {
  const previewCategories = categories.map(([id, title]) => ({
    id,
    title,
    entries: pending.notes
      .filter(note => note.category === id)
      .map(note => ({ text: `${note.text} (${note.sha.slice(0, 7)})` })),
  }));
  const markdown = categoryMarkdown(
    previewCategories,
    '_No player-facing notes pending._',
  );

  console.log(markdown.trimEnd());
}

function requireValidDocument(document) {
  const problems = validatePlayersDocument(document);
  if (problems.length > 0) {
    throw Error(problems.join('\n'));
  }
}

function outputFiles(versions, document, settings) {
  return [
    {
      name: settings.devFile,
      text: renderDev(developerVersions(versions), settings),
      type: 'dev',
    },
    {
      name: settings.playersFile,
      text: renderPlayers(document),
      type: 'players',
    },
    {
      name: settings.jsonFile,
      text: `${JSON.stringify(document, null, 2)}\n`,
      type: 'players',
    },
  ];
}

function synchronizeOutputs(command, options, root, files, settings) {
  let changedCount = 0;

  for (const file of files) {
    const excluded = options.only && options.only !== 'all' && options.only !== file.type;
    if (command === 'generate' && excluded) {
      continue;
    }

    const path = resolve(root, file.name);
    const diskText = readText(path);

    if (command === 'check' && file.name === settings.jsonFile && diskText !== null) {
      requireValidDocument(JSON.parse(diskText));
    }

    if (diskText !== file.text) {
      changedCount += 1;
      const label = command === 'check' ? 'Differs:' : 'Changed:';
      console.log(`${label} ${file.name}`);

      if (command === 'generate') {
        writeFileSync(path, file.text, 'utf8');
      }
    }
  }

  if (changedCount === 0) {
    console.log('up to date');
  }

  if (command === 'check' && changedCount > 0) {
    process.exitCode = 1;
  }
}

function printReleaseNotes(document, tag, settings) {
  const release = document.releases.find(version => version.tag === tag);
  if (!release) {
    throw Error(`Unknown version tag: ${tag}`);
  }

  console.log(renderReleaseNotes(release, settings).trimEnd());
}

function runCommand(command, options) {
  const root = resolve(options.root || repositoryRoot());
  const ref = options.ref || 'HEAD';
  const settings = {
    ...config,
    repoUrl: (options['repo-url'] || config.repoUrl).replace(/\/$/, ''),
  };
  const versions = readVersions(root, ref);
  const history = readCommits(root, ref);
  const overrides = readOverrides(root, settings, history);
  const players = playerVersions(versions, overrides);
  const { pending, warnings } = warningScope(command, options, root, players, overrides);

  for (const warning of warnings) {
    console.error(`${warning.sha}: ${warning.message}`);
  }

  if (command === 'preview') {
    printPreview(pending);
  } else {
    const document = playersDocument(players.versions, settings);
    requireValidDocument(document);

    if (command === 'release-notes') {
      printReleaseNotes(document, options.tag, settings);
    } else {
      const files = outputFiles(versions, document, settings);
      synchronizeOutputs(command, options, root, files, settings);
    }
  }

  if (options.strict && warnings.length > 0) {
    process.exitCode = 1;
  }
}

function main() {
  let parsed;

  try {
    parsed = parseArguments(process.argv.slice(2));
  } catch {
    console.error(usage);
    process.exitCode = 2;
    return;
  }

  try {
    runCommand(parsed.command, parsed.options);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

main();

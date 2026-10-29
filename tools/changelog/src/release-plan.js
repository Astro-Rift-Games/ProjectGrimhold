import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { git, readCommits } from './git.js';
import { developerVersions, activeCommits, commitIdentities } from './commits.js';
import { config } from './config.js';
import { readPublicationVersions } from './versions.js';
import { playerVersions, validateOverrides } from './players.js';
import { developerArtifacts } from './render-dev.js';
import { writeGeneratedFiles } from './artifacts.js';
import { playersDocument, renderPlayers, validatePlayersDocument } from './render-players.js';

function functional(commit, history, visiting = new Set()) {
  if (!commit || commit.excluded || visiting.has(commit.sha)) return false;
  if (commit.breaking || ['feat', 'fix', 'perf'].includes(commit.type)) return true;
  if (!commit.reverts) return false;
  visiting.add(commit.sha);
  return functional(history.get(commit.reverts), history, visiting);
}

function pendingImpact(versions) {
  const pending = versions.at(-1).commits;
  const pendingShas = new Set(pending.map(commit => commit.sha));
  const identities = commitIdentities(versions.flatMap(version => version.commits));
  const active = activeCommits(pending, identities);
  const activeShas = new Set(active.map(commit => commit.sha));
  const published = new Map(versions.slice(0, -1)
    .flatMap(version => version.commits).map(commit => [commit.sha, commit]));
  // Reuse the developer's identity and bot exclusions, including cross-release
  // deduplication. Keep cancelled entries in technical history, not in impact.
  const entries = developerVersions(versions).at(-1).entries;
  let rank = 0;

  for (const entry of entries) {
    if (entry.refs.every(sha => !activeShas.has(sha))) {
      continue;
    }
    if (entry.reverts && pendingShas.has(entry.reverts)) {
      continue;
    }

    if (entry.breaking) {
      rank = 3;
    } else if (entry.type === 'feat') {
      rank = Math.max(rank, 2);
    } else if (['fix', 'perf'].includes(entry.type)
      || (entry.reverts && functional(published.get(entry.reverts), published))) {
      rank = Math.max(rank, 1);
    }
  }

  return rank;
}

function nextVersion(previous, rank) {
  if (!rank) {
    return { version: null, bump: null };
  }

  const parts = (previous ?? '0.0.0').split('.').map(BigInt);
  const bump = rank === 3 && parts[0] > 0n ? 'major' : rank >= 2 ? 'minor' : 'patch';
  const index = { major: 0, minor: 1, patch: 2 }[bump];
  parts[index] += 1n;
  for (let offset = index + 1; offset < parts.length; offset += 1) {
    parts[offset] = 0n;
  }
  return { version: parts.join('.'), bump };
}

/** Read-only plan of releases on the supplied main/candidate first-parent history. */
export function createReleasePlan(root, ref = 'HEAD') {
  if (typeof ref !== 'string' || !ref || ref.startsWith('-')) {
    throw Error('Invalid release ref');
  }
  const sourceCommit = git(root, ['rev-parse', '--verify', '--end-of-options', `${ref}^{commit}`]).trim();
  readCommits(root, sourceCommit); // Prime immutable patch evidence across release boundaries.
  const versions = readPublicationVersions(root, sourceCommit);
  const previous = versions.at(-2);
  const { version, bump } = nextVersion(previous?.version, pendingImpact(versions));
  const allTags = new Set(git(root, ['tag']).trim().split('\n'));
  if (version && allTags.has(`v${version}`)) {
    throw Error(`Version tag v${version} already exists; publication cannot reuse it`);
  }
  const date = git(root, ['show', '-s', '--format=%cI', sourceCommit]).trim();

  return {
    previousTag: previous?.tag ?? null,
    previousVersion: previous?.version ?? null,
    version,
    bump,
    sourceCommit,
    releaseDate: new Date(date).toISOString().slice(0, 10),
  };
}

/** Validate everything before writing generated public and developer artifacts. */
export function writePlannedRelease(root, plan) {
  if (!plan?.version) {
    throw Error('No release is planned');
  }
  const expected = createReleasePlan(root, plan.sourceCommit);
  if (Object.keys(expected).some(key => plan[key] !== expected[key])) {
    throw Error('Release plan is stale or has been modified');
  }

  const versions = readPublicationVersions(root, plan.sourceCommit);
  const pending = versions.pop();
  versions.push({
    ...pending,
    version: plan.version,
    tag: `v${plan.version}`,
    commit: plan.sourceCommit,
    date: plan.releaseDate,
  }, { version: 'Unreleased', commits: [] });

  const history = readCommits(root, plan.sourceCommit);
  const overrides = validateOverrides(JSON.parse(readFileSync(resolve(root, config.overridesPath), 'utf8')), history);
  const players = playerVersions(versions, overrides);
  if (players.warnings.length) {
    throw Error(players.warnings.map(warning => `${warning.sha}: ${warning.message}`).join('\n'));
  }
  const document = playersDocument(players.versions, config);
  const problems = validatePlayersDocument(document);
  if (problems.length) {
    throw Error(problems.join('\n'));
  }

  const files = [
    ...developerArtifacts(developerVersions(versions), config).map(file => [file.name, file.text]),
    [config.playersFile, renderPlayers(document)],
    [config.jsonFile, `${JSON.stringify(document, null, 2)}\n`],
  ];
  writeGeneratedFiles(root, files.map(([name, text]) => ({ name, text, type: name === config.devFile || name.startsWith(config.historyDirectory + '/') ? 'dev' : 'players' })), config);
  return files.map(([name]) => name);
}

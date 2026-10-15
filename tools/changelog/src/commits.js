import { normalize } from './config.js';

const sections = {
  feat: 'Features',
  fix: 'Bug Fixes',
  perf: 'Performance',
  refactor: 'Refactoring',
  test: 'Tests',
  docs: 'Documentation',
  build: 'Architecture & Infrastructure',
  ci: 'Architecture & Infrastructure',
  chore: 'Architecture & Infrastructure',
  revert: 'Reverts',
};

const headerPattern =
  /^(?<type>[a-z]+)(?:\((?<scope>[^()\r\n]+)\))?(?<bang>!)?: (?<description>.+)$/;
const trailerPattern = /^([A-Za-z][A-Za-z0-9-]*|BREAKING CHANGE): (.*)$/;
const cherryPickPattern = /^\s*\(cherry picked from commit [0-9a-f]+\)\s*$/i;

export const sectionOrder = [...new Set(Object.values(sections)), 'Other Changes'];

function parseFooter(body) {
  const paragraphs = body.replace(/^\n+|\n+$/g, '').split(/\n[ \t]*\n/);
  const lastParagraph = paragraphs.at(-1) || '';
  const trailers = [];
  let valid = true;

  for (const line of lastParagraph.split('\n')) {
    if (/^\s/.test(line) && trailers.length > 0) {
      trailers.at(-1).value += ` ${line.trim()}`;
      continue;
    }

    const match = trailerPattern.exec(line);
    if (match) {
      trailers.push({ key: match[1], value: match[2] });
    } else {
      valid = false;
    }
  }

  return {
    trailers: valid ? trailers : [],
    outsideFooter: valid ? paragraphs.slice(0, -1).join('\n\n') : body,
  };
}

function commitIdentity(subject, body) {
  const identityBody = body
    .split('\n')
    .filter(line => !cherryPickPattern.test(line))
    .join('\n');

  return `${subject}\n${identityBody}`
    .split('\n')
    .map(line => line.trimEnd())
    .join('\n')
    .trim()
    .replace(/[ \t]+/g, ' ')
    .toLowerCase();
}

function isBreakingTrailer(trailer) {
  return trailer.key === 'BREAKING CHANGE' || trailer.key === 'BREAKING-CHANGE';
}

export function parseCommit(rawCommit) {
  const body = rawCommit.body.replace(/\r\n/g, '\n');
  const footer = parseFooter(body);
  const header = headerPattern.exec(rawCommit.subject)?.groups || {};
  const section = sections[header.type] || 'Other Changes';
  const description = section === 'Other Changes'
    ? rawCommit.subject
    : header.description || rawCommit.subject;
  const automation = header.type === 'chore' && header.scope === 'changelog';
  const revertedSha = /This reverts commit ([0-9a-f]{40})/i.exec(body)?.[1];

  return {
    ...rawCommit,
    body,
    ...header,
    description: normalize(description),
    scope: header.scope,
    section,
    ...footer,
    breaking: Boolean(header.bang) || footer.trailers.some(isBreakingTrailer),
    excluded: automation || rawCommit.subject.startsWith('Merge '),
    identity: commitIdentity(rawCommit.subject, body),
    reverts: revertedSha?.toLowerCase(),
  };
}

function addDeveloperEntry(commit, entries, localEntries) {
  const existing = localEntries.get(commit.identity);

  if (existing) {
    if (!existing.refs.includes(commit.sha)) {
      existing.refs.push(commit.sha);
    }
    return;
  }

  const entry = { ...commit, refs: [commit.sha] };
  entries.push(entry);
  localEntries.set(commit.identity, entry);
}

export function developerVersions(versions) {
  const publishedIdentities = new Set();

  return versions.map(version => {
    const entries = [];
    const localEntries = new Map();

    for (const commit of version.commits) {
      if (commit.excluded || publishedIdentities.has(commit.identity)) {
        continue;
      }

      addDeveloperEntry(commit, entries, localEntries);
    }

    for (const identity of localEntries.keys()) {
      publishedIdentities.add(identity);
    }

    return { ...version, entries };
  });
}

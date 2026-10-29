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
const cherryPickPattern = /^\s*\(cherry picked from commit ([0-9a-f]+)\)\s*$/i;
const fullHashPattern = /^(?:[0-9a-f]{40}|[0-9a-f]{64})$/i;

export const sectionOrder = [...new Set(Object.values(sections)), 'Other Changes'];

function parseFooter(body) {
  // Git's -x provenance is metadata, not prose invalidating the public footer.
  const content = body.split('\n').filter(line => !cherryPickPattern.test(line)).join('\n');
  const paragraphs = content.replace(/^\n+|\n+$/g, '').split(/\n[ \t]*\n/);
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
    outsideFooter: valid ? paragraphs.slice(0, -1).join('\n\n') : content,
  };
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
  const revertedSha = /This reverts commit ((?:[0-9a-f]{64}|[0-9a-f]{40}))(?![0-9a-f])/i.exec(body)?.[1];
  const cherryPickOrigins = body.split('\n')
    .map(line => cherryPickPattern.exec(line)?.[1])
    .filter(origin => origin && fullHashPattern.test(origin))
    .map(origin => origin.toLowerCase());

  return {
    ...rawCommit,
    body,
    ...header,
    description: normalize(description),
    scope: header.scope,
    section,
    ...footer,
    breaking: Boolean(header.bang) || footer.trailers.some(isBreakingTrailer),
    excluded: automation,
    identity: `sha:${rawCommit.sha.toLowerCase()}`,
    cherryPickOrigins,
    reverts: revertedSha?.toLowerCase(),
  };
}

// Only immutable Git evidence proves equivalence. Subject/body similarity does not.
export function commitIdentities(commits) {
  const parents = new Map();
  const patches = new Map();
  const related = new Map(commits.map(commit => [commit.sha.toLowerCase(), new Set(commit.patchRelatedShas || [])]));
  function find(key) {
    if (!parents.has(key)) parents.set(key, key);
    if (parents.get(key) !== key) parents.set(key, find(parents.get(key)));
    return parents.get(key);
  }
  function unite(first, second) {
    const left = find(first);
    const right = find(second);
    if (left !== right) parents.set(left > right ? left : right, left > right ? right : left);
  }

  for (const commit of commits) {
    const sha = commit.sha.toLowerCase();
    find(sha);
    for (const origin of commit.cherryPickOrigins || []) {
      if (fullHashPattern.test(origin)) unite(sha, origin.toLowerCase());
    }
    // A revert-of-revert may repeat the original patch, but is a distinct action.
    if (!commit.reverts && typeof commit.patchId === 'string' && fullHashPattern.test(commit.patchId)) {
      const patch = commit.patchId.toLowerCase();
      const candidates = patches.get(patch) || [];
      for (const candidate of candidates) {
        const left = [sha, ...candidates.filter(member => find(member) === find(sha))];
        const right = candidates.filter(member => find(member) === find(candidate));
        const separateExecutions = left.some(first => right.some(second =>
          related.get(first)?.has(second) || related.get(second)?.has(first)));
        if (!separateExecutions) unite(sha, candidate);
      }
      if (!candidates.includes(sha)) candidates.push(sha);
      patches.set(patch, candidates);
    }
  }

  return new Map([...parents.keys()].map(sha => [sha, `sha:${find(sha)}`]));
}

// Input order is Git's newest-first topological order, never author-date order.
export function activeCommits(commits, identities = commitIdentities(commits)) {
  const suppressed = new Set();
  const identity = sha => identities.get(sha.toLowerCase()) || `sha:${sha.toLowerCase()}`;
  return commits.filter(commit => {
    if (suppressed.has(identity(commit.sha))) return false;
    if (commit.reverts) suppressed.add(identity(commit.reverts));
    return true;
  });
}

// Equivalent patches are new changes when a prior revert removed their effect.
// Versions are chronological; each version's commits remain newest-first.
export function commitActionIdentities(versions, identities = commitIdentities(versions.flatMap(version => version.commits))) {
  const commits = versions.flatMap(version => [...version.commits].reverse());
  const identity = sha => identities.get(sha.toLowerCase()) || `sha:${sha.toLowerCase()}`;
  const representatives = new Map(commits.map(commit => [identity(commit.sha), commit]));
  const effects = new Map();

  function effect(key, visiting = new Set()) {
    if (effects.has(key)) return effects.get(key);
    const commit = representatives.get(key);
    if (!commit?.reverts || visiting.has(key)) return { root: key, added: true };
    visiting.add(key);
    const target = effect(identity(commit.reverts), visiting);
    const result = { root: target.root, added: !target.added };
    effects.set(key, result);
    return result;
  }

  const states = new Map();
  const actions = new Map();
  for (const commit of commits) {
    const sha = commit.sha.toLowerCase();
    // Repeated source records are not new executions of the same physical commit.
    if (actions.has(sha)) continue;
    const key = identity(sha);
    const { root, added } = effect(key);
    const state = states.get(root) || { added: false, epoch: 0 };
    if (added && !state.added) state.epoch += 1;
    state.added = added;
    states.set(root, state);
    actions.set(sha, `${key}:epoch:${state.epoch}`);
  }
  return actions;
}
export function developerVersions(versions) {
  const identities = commitActionIdentities(versions);
  const publishedEntries = new Map();

  return versions.map(version => {
    const entries = [];
    for (const commit of version.commits) {
      if (commit.excluded) continue;
      const identity = identities.get(commit.sha.toLowerCase());
      const existing = publishedEntries.get(identity);
      if (existing) {
        if (!existing.refs.includes(commit.sha)) existing.refs.push(commit.sha);
        continue;
      }
      const entry = { ...commit, identity, refs: [commit.sha] };
      entries.push(entry);
      publishedEntries.set(identity, entry);
    }
    return { ...version, entries };
  });
}

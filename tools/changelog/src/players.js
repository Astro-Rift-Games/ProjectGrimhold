import { categories, normalize } from './config.js';
import { activeCommits, commitActionIdentities, commitIdentities } from './commits.js';

const playerKeys = {
  'Player-Added': 'added',
  'Player-Improved': 'improved',
  'Player-Fixed': 'fixed',
  'Player-Balanced': 'balanced',
};

function validText(text) {
  if (typeof text !== 'string') return false;
  const length = normalize(text).length;
  return length > 0 && length <= 240;
}

function resolveOverrideCommit(prefix, commits) {
  if (typeof prefix !== 'string' || !/^[a-f0-9]{7,40}$/i.test(prefix)) {
    throw Error('Invalid override commit prefix');
  }
  const matches = [...new Set(commits.filter(commit => (
    commit.sha.startsWith(prefix.toLowerCase())
  )).map(commit => commit.sha))];
  if (matches.length !== 1) {
    throw Error(`Override prefix ${prefix} matches ${matches.length} commits`);
  }
  return matches[0];
}

function validateOverrideEntries(entries) {
  if (!Array.isArray(entries)) throw Error('Invalid override entries');
  for (const entry of entries) {
    const knownCategory = entry && categories.some(([id]) => id === entry.category);
    if (!knownCategory || !validText(entry.text)) {
      throw Error('Invalid override category or text');
    }
  }
  return entries.map(entry => ({ category: entry.category, text: normalize(entry.text) }));
}

export function validateOverrides(document, commits) {
  if (!document || document.schemaVersion !== 1 || !Array.isArray(document.overrides)) {
    throw Error('Invalid overrides schema');
  }
  const overrides = new Map();
  for (const override of document.overrides) {
    const sha = resolveOverrideCommit(override?.commit, commits);
    if (overrides.has(sha)) throw Error(`Duplicate override for ${sha}`);
    overrides.set(sha, validateOverrideEntries(override.entries));
  }
  return overrides;
}

function unknownTrailerMessage(key) {
  const wrongCase = Object.keys(playerKeys).some(knownKey => (
    knownKey.toLowerCase() === key.toLowerCase()
  ));
  return wrongCase ? 'Wrong case for player trailer' : `Unknown player trailer ${key}`;
}

function trailerNotes(commit, warnings) {
  const entries = [];
  const warn = message => warnings.push({
    sha: commit.sha, code: 'invalid-player-note', severity: 'error', message,
  });
  for (const line of commit.outsideFooter.split('\n')) {
    if (/^\s*Player-[A-Za-z]+(?:\s*:|\s*$|\s+[^,])/i.test(line)) {
      warn('Misplaced or malformed player trailer; use Player-Added, Player-Improved, Player-Fixed or Player-Balanced: text in the final footer');
    }
  }
  for (const trailer of commit.trailers) {
    if (!/^player-/i.test(trailer.key)) continue;
    if (commit.excluded) warn('Player trailer on automation commit');
    if (!playerKeys[trailer.key]) {
      warn(unknownTrailerMessage(trailer.key));
      continue;
    }
    if (!validText(trailer.value)) {
      warn('Player note must contain 1–240 characters');
      continue;
    }
    entries.push({ category: playerKeys[trailer.key], text: normalize(trailer.value) });
  }
  return entries;
}

function appendUniqueNotes(entries, commit, identity, notes, localNotes, publishedNotes) {
  entries.forEach((entry, index) => {
    const noteKey = `${entry.category}:${entry.text.toLowerCase()}`;
    const provenKey = `${identity}:${noteKey}`;
    if (publishedNotes.has(provenKey)) return;
    publishedNotes.add(provenKey);
    // One release may consolidate repeated wording, but different releases may not.
    if (localNotes.has(noteKey)) return;
    localNotes.add(noteKey);
    notes.push({ ...entry, id: `${commit.sha.slice(0, 12)}-${index + 1}`, sha: commit.sha });
  });
}

export function validatePublicNotes(versions, warnings = []) {
  const diagnostics = warnings.map(warning => ({ severity: 'error', ...warning }));
  for (const version of versions) {
    if (version.version === 'Unreleased' || !version.tag || version.notes.length > 0) continue;
    if (diagnostics.some(item => item.code === 'empty-player-release' && item.tag === version.tag)) continue;
    diagnostics.push({
      code: 'empty-player-release', severity: 'warning', sha: version.commit,
      tag: version.tag, version: version.version,
      message: `Release ${version.tag} has no visible player notes; add explicit Player-* trailers or an approved override before publication`,
    });
  }
  return diagnostics;
}

export function playerVersions(versions, overrides) {
  const warnings = [];
  const publishedNotes = new Set();
  const identities = commitIdentities(versions.flatMap(version => version.commits));
  const actions = commitActionIdentities(versions, identities);
  const output = versions.map(version => {
    const notes = [];
    const localNotes = new Set();
    const active = new Set(activeCommits(version.commits, identities));
    // Git supplies newest-first topo-order; reverse without consulting author dates.
    for (const commit of [...version.commits].reverse()) {
      const entries = overrides.has(commit.sha)
        ? overrides.get(commit.sha)
        : trailerNotes(commit, warnings);
      if (commit.excluded || !active.has(commit)) continue;
      appendUniqueNotes(entries, commit, actions.get(commit.sha.toLowerCase()), notes, localNotes, publishedNotes);
    }
    return { ...version, notes };
  });
  return { versions: output, warnings: validatePublicNotes(output, warnings) };
}

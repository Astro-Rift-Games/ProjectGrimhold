import { categories, normalize } from './config.js';

const playerKeys = {
  'Player-Added': 'added',
  'Player-Improved': 'improved',
  'Player-Fixed': 'fixed',
  'Player-Balanced': 'balanced',
};

function validText(text) {
  if (typeof text !== 'string') {
    return false;
  }

  const length = normalize(text).length;
  return length > 0 && length <= 240;
}

function resolveOverrideCommit(prefix, commits) {
  if (typeof prefix !== 'string' || !/^[a-f0-9]{7,40}$/i.test(prefix)) {
    throw Error('Invalid override commit prefix');
  }

  const matches = commits.filter(commit => commit.sha.startsWith(prefix.toLowerCase()));
  if (matches.length !== 1) {
    throw Error(`Override prefix ${prefix} matches ${matches.length} commits`);
  }

  return matches[0].sha;
}

function validateOverrideEntries(entries) {
  if (!Array.isArray(entries)) {
    throw Error('Invalid override entries');
  }

  for (const entry of entries) {
    const knownCategory = categories.some(([id]) => id === entry.category);
    if (!knownCategory || !validText(entry.text)) {
      throw Error('Invalid override category or text');
    }
  }

  return entries.map(entry => ({
    category: entry.category,
    text: normalize(entry.text),
  }));
}

export function validateOverrides(document, commits) {
  if (!document || document.schemaVersion !== 1 || !Array.isArray(document.overrides)) {
    throw Error('Invalid overrides schema');
  }

  const overrides = new Map();

  for (const override of document.overrides) {
    const sha = resolveOverrideCommit(override.commit, commits);
    if (overrides.has(sha)) {
      throw Error(`Duplicate override for ${sha}`);
    }

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
  const warn = message => warnings.push({ sha: commit.sha, message });

  for (const line of commit.outsideFooter.split('\n')) {
    if (/^Player-[A-Za-z]+:/i.test(line)) {
      warn('Misplaced player trailer outside footer');
    }
  }

  for (const trailer of commit.trailers) {
    if (!/^player-/i.test(trailer.key)) {
      continue;
    }

    if (commit.excluded) {
      warn('Player trailer on automation commit');
    }

    if (!playerKeys[trailer.key]) {
      warn(unknownTrailerMessage(trailer.key));
      continue;
    }

    if (!validText(trailer.value)) {
      warn('Player note must contain 1–240 characters');
      continue;
    }

    entries.push({
      category: playerKeys[trailer.key],
      text: normalize(trailer.value),
    });
  }

  return entries;
}

function appendUniqueNotes(entries, commit, notes, publishedNotes) {
  entries.forEach((entry, index) => {
    const key = `${entry.category}:${entry.text.toLowerCase()}`;
    if (publishedNotes.has(key)) {
      return;
    }

    publishedNotes.add(key);
    notes.push({
      ...entry,
      id: `${commit.sha.slice(0, 12)}-${index + 1}`,
      sha: commit.sha,
    });
  });
}

export function playerVersions(versions, overrides) {
  const warnings = [];
  const publishedNotes = new Set();

  const output = versions.map(version => {
    const revertedShas = new Set(version.commits.map(commit => commit.reverts).filter(Boolean));
    const notes = [];

    // Git supplies newest-first topo-order; reverse it without consulting author dates.
    for (const commit of [...version.commits].reverse()) {
      const entries = overrides.has(commit.sha)
        ? overrides.get(commit.sha)
        : trailerNotes(commit, warnings);

      if (commit.excluded || revertedShas.has(commit.sha)) {
        continue;
      }

      appendUniqueNotes(entries, commit, notes, publishedNotes);
    }

    return { ...version, notes };
  });

  return { versions: output, warnings };
}

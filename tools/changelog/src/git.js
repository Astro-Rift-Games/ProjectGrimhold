import { execFileSync } from 'node:child_process';
import { parseCommit } from './commits.js';

export function git(root, args, environment) {
  return execFileSync('git', args, {
    cwd: root,
    encoding: 'utf8',
    maxBuffer: 128 * 1024 * 1024,
    env: environment ? { ...process.env, ...environment } : process.env,
  });
}

export function repositoryRoot() {
  return git(process.cwd(), ['rev-parse', '--show-toplevel']).trim();
}

function parseLogFields(fields, offset) {
  const message = fields[offset + 2].replace(/\r\n/g, '\n');
  const body = message.slice(message.indexOf('\n') + 1).replace(/^\n/, '');

  return parseCommit({
    sha: fields[offset].trim(),
    subject: fields[offset + 1],
    body,
    date: fields[offset + 3],
  });
}

const patchCache = new Map();
const patchRelations = new Map();
const ancestryCache = new Map();

function ancestryRelated(root, first, second) {
  const key = root + ':' + [first, second].sort().join(':');
  if (!ancestryCache.has(key)) {
    const isAncestor = (ancestor, descendant) => {
      try { git(root, ['merge-base', '--is-ancestor', ancestor, descendant]); return true; }
      catch (error) { if (error.status === 1) return false; throw error; }
    };
    ancestryCache.set(key, isAncestor(first, second) || isAncestor(second, first));
  }
  return ancestryCache.get(key);
}

function enrichCommits(root, range, commits) {
  // NUL-separated names preserve spaces, quotes and non-ASCII Git paths.
  const names = git(root, ['log', '--no-merges', '--topo-order', '--format=%x00%H', '--name-only', '-z', range, '--']);
  const files = new Map();
  for (const record of names.split('\0\0')) {
    const fields = record.replace(/^\0/, '').split('\0');
    const sha = fields.shift().trim();
    if (!/^[a-f0-9]{40}$/.test(sha)) continue;
    files.set(sha, fields.filter(Boolean).map((name, index) => index === 0 ? name.replace(/^\n/, '') : name).filter(Boolean));
  }
  // Text is only a candidate lookup, never equivalence evidence. Verify repeated
  // subjects with Git's stable patch ID; empty patches deliberately have no ID.
  const candidates = new Map();
  for (const commit of commits.filter(commit => !commit.excluded)) {
    const subject = commit.subject.toLowerCase().replace(/\s+/g, ' ').trim();
    const group = candidates.get(subject) || []; group.push(commit); candidates.set(subject, group);
  }
  for (const group of candidates.values()) {
    if (group.length < 2) continue;
    for (const commit of group) {
      const key = root + ':' + commit.sha;
      if (!patchCache.has(key)) {
        const patch = git(root, ['show', '--format=', '--no-ext-diff', '--no-textconv', '--no-renames', '--binary', commit.sha, '--']);
        const result = execFileSync('git', ['patch-id', '--stable'], { cwd: root, input: patch, encoding: 'utf8', maxBuffer: 128 * 1024 * 1024 });
        patchCache.set(key, /^([a-f0-9]{40})\b/.exec(result)?.[1]);
      }
    }
  }
  const patchGroups = new Map();
  for (const commit of commits) {
    const patch = patchCache.get(root + ':' + commit.sha);
    if (!patch) continue;
    const group = patchGroups.get(patch) || new Set(); group.add(commit.sha); patchGroups.set(patch, group);
  }
  for (const group of patchGroups.values()) {
    const shas = [...group];
    for (let first = 0; first < shas.length; first += 1) {
      for (let second = first + 1; second < shas.length; second += 1) {
        if (!ancestryRelated(root, shas[first], shas[second])) continue;
        // An identical patch executed again in one ancestry is not proof of one
        // change: an ordinary edit may have manually inverted the earlier patch.
        for (const [sha, related] of [[shas[first], shas[second]], [shas[second], shas[first]]]) {
          const key = root + ':' + sha;
          if (!patchRelations.has(key)) patchRelations.set(key, new Set());
          patchRelations.get(key).add(related);
        }
      }
    }
  }
  return commits.map(commit => ({ ...commit, files: files.get(commit.sha) || [],
    patchId: patchCache.get(root + ':' + commit.sha),
    patchRelatedShas: [...(patchRelations.get(root + ':' + commit.sha) || [])].sort() }));
}

export function readCommits(root, range) {
  const output = git(root, [
    'log',
    '--no-merges',
    '--topo-order',
    '--format=%H%x00%s%x00%B%x00%aI%x00',
    range,
    '--',
  ]);
  const fields = output.split('\0');
  const commits = [];

  for (let offset = 0; offset + 3 < fields.length; offset += 4) {
    commits.push(parseLogFields(fields, offset));
  }

  return enrichCommits(root, range, commits);
}

export function rangeShas(root, range) {
  const output = git(root, ['rev-list', '--no-merges', range, '--']);

  return new Set(output.trim().split('\n').filter(Boolean));
}

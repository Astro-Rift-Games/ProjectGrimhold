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

  return commits;
}

export function rangeShas(root, range) {
  const output = git(root, ['rev-list', '--no-merges', range, '--']);

  return new Set(output.trim().split('\n').filter(Boolean));
}

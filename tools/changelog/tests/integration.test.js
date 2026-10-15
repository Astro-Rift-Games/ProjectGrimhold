import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  mkdtempSync,
  mkdirSync,
  writeFileSync,
  readFileSync,
  statSync,
  rmSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { git, readCommits } from '../src/git.js';
import { readVersions } from '../src/versions.js';

const cliPath = fileURLToPath(new URL('../src/cli.js', import.meta.url));

function createRepository(context) {
  const root = mkdtempSync(join(tmpdir(), 'grimhold-changelog-'));
  context.after(() => rmSync(root, { recursive: true, force: true }));

  git(root, ['init', '--initial-branch=New-Testing']);
  git(root, ['config', 'user.name', 'Changelog Test']);
  git(root, ['config', 'user.email', 'changelog@example.invalid']);
  git(root, ['config', 'core.autocrlf', 'false']);

  mkdirSync(join(root, 'tools/changelog'), { recursive: true });
  writeFileSync(
    join(root, 'tools/changelog/player-overrides.json'),
    JSON.stringify({ schemaVersion: 1, overrides: [] }),
  );

  return root;
}

function createCommit(root, subject, body = '', date = '2026-01-01T00:00:00Z') {
  git(root, [
    '-c',
    'commit.gpgsign=false',
    'commit',
    '--allow-empty',
    '-m',
    subject,
    '-m',
    body,
  ], {
    GIT_AUTHOR_DATE: date,
    GIT_COMMITTER_DATE: date,
  });

  return git(root, ['rev-parse', 'HEAD']).trim();
}

function runCli(root, ...args) {
  const result = spawnSync(process.execPath, [cliPath, ...args, '--root', root], {
    encoding: 'utf8',
  });

  assert.ifError(result.error);

  return { status: result.status, stdout: result.stdout, stderr: result.stderr };
}

function generate(root) {
  const result = runCli(root, 'generate');
  assert.equal(result.status, 0, result.stderr);

  return result;
}

function readPlayers(root) {
  return JSON.parse(readFileSync(join(root, 'changelog-players.json'), 'utf8'));
}

function taggedRepository(context) {
  const root = createRepository(context);
  createCommit(root, 'feat: Adventure', 'Player-Added: First adventure');
  git(root, ['tag', 'v1.0.0']);

  return root;
}

function repositoryWithWarning(context) {
  const root = taggedRepository(context);
  createCommit(root, 'fix: Door', 'Player-Fixed: Door opens', '2026-01-02T00:00:00Z');
  git(root, ['tag', 'v1.1.0']);
  createCommit(root, 'fix: Warning', 'Player-Fix: Invalid key', '2026-01-03T00:00:00Z');

  return root;
}

test('second generate reports up to date without rewriting files', context => {
  const root = taggedRepository(context);
  generate(root);
  const outputPath = join(root, 'CHANGELOG_DEV.md');
  const initialModifiedTime = statSync(outputPath).mtimeMs;

  assert.match(generate(root).stdout, /up to date/);
  assert.equal(statSync(outputPath).mtimeMs, initialModifiedTime);
});

test('tagging moves Unreleased player notes into a version', context => {
  const root = createRepository(context);
  createCommit(root, 'feat: Adventure', 'Player-Added: First adventure');
  generate(root);

  assert.equal(readPlayers(root).releases.length, 0);
  assert.match(runCli(root, 'preview').stdout, /First adventure/);

  git(root, ['tag', 'v1.0.0']);
  generate(root);

  const release = readPlayers(root).releases[0];
  assert.equal(release.categories[0].entries[0].text, 'First adventure');
  assert.match(runCli(root, 'preview').stdout, /_No player-facing notes pending\._/);
});

test('player JSON lists only tagged releases while preview includes pending notes', context => {
  const root = taggedRepository(context);
  createCommit(root, 'feat: Pending', 'Player-Added: Pending adventure');
  generate(root);

  const document = readPlayers(root);
  assert.deepEqual(document.releases.map(release => release.version), ['1.0.0']);
  assert.doesNotMatch(JSON.stringify(document), /Pending adventure/);
  assert.match(runCli(root, 'preview').stdout, /Pending adventure/);
});

test('check detects generated output drift without writing files', context => {
  const root = taggedRepository(context);
  generate(root);
  const outputPath = join(root, 'CHANGELOG.md');
  writeFileSync(outputPath, 'Manual drift\n');

  const result = runCli(root, 'check');

  assert.equal(result.status, 1);
  assert.match(result.stdout, /Differs: CHANGELOG\.md/);
  assert.equal(readFileSync(outputPath, 'utf8'), 'Manual drift\n');
});

test('check passes when all generated outputs are current', context => {
  const root = taggedRepository(context);
  generate(root);

  const result = runCli(root, 'check');

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /up to date/);
});

test('release-notes for unknown tag exits one', context => {
  const root = taggedRepository(context);
  const result = runCli(root, 'release-notes', '--tag', 'v9.0.0');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Unknown version tag/);
});

test('release-notes prints categories and final developer link for a known tag', context => {
  const root = taggedRepository(context);
  const result = runCli(root, 'release-notes', '--tag', 'v1.0.0');

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /### Added\n\n- First adventure/);
  assert.match(result.stdout, /Developer changes: .*CHANGELOG_DEV\.md\n$/);
});

test('strict release-notes only counts warnings from that release', context => {
  const root = repositoryWithWarning(context);
  assert.equal(runCli(root, 'release-notes', '--tag', 'v1.1.0', '--strict').status, 0);

  git(root, ['tag', 'v1.2.0']);
  const result = runCli(root, 'release-notes', '--tag', 'v1.2.0', '--strict');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Unknown player trailer/);
});

test('strict preview exits one on player warnings', context => {
  const root = repositoryWithWarning(context);
  const result = runCli(root, 'preview', '--strict');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Unknown player trailer/);
});

test('strict preview with range only counts warnings from range commits', context => {
  const root = repositoryWithWarning(context);
  const result = runCli(root, 'preview', '--strict', '--range', 'v1.0.0..v1.1.0');

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stderr, '');
  assert.match(result.stdout, /Door opens/);
});

test('strict check with range only counts warnings from range commits', context => {
  const root = repositoryWithWarning(context);
  generate(root);

  const result = runCli(root, 'check', '--strict', '--range', 'v1.0.0..v1.1.0');

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stderr, '');
  assert.equal(runCli(root, 'check', '--strict').status, 1);
});

test('strict range still rejects warnings inside the selected range', context => {
  const root = repositoryWithWarning(context);
  const result = runCli(root, 'preview', '--strict', '--range', 'v1.1.0..HEAD');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Unknown player trailer/);
});

test('unknown CLI commands exit two with usage', context => {
  const root = taggedRepository(context);
  const result = runCli(root, 'unknown');

  assert.equal(result.status, 2);
  assert.match(result.stderr, /Usage:/);
});

test('unknown CLI flags exit two with usage', context => {
  const root = taggedRepository(context);
  const result = runCli(root, 'generate', '--bad');

  assert.equal(result.status, 2);
  assert.match(result.stderr, /Usage:/);
});

test('version tags sort by semver rather than lexicographically', context => {
  const root = taggedRepository(context);
  createCommit(root, 'fix: Second', '', '2026-01-02T00:00:00Z');
  git(root, ['tag', 'v1.2.0']);
  createCommit(root, 'fix: Third', '', '2026-01-03T00:00:00Z');
  git(root, ['tag', 'v1.10.0']);
  generate(root);

  assert.deepEqual(
    readPlayers(root).releases.map(release => release.version),
    ['1.10.0', '1.2.0', '1.0.0'],
  );
});

test('only exact semver tags merged into the target ref are versions', context => {
  const root = taggedRepository(context);
  git(root, ['tag', 'v2.0.0-beta']);
  git(root, ['tag', 'release-1']);
  createCommit(root, 'fix: Future', '', '2026-01-02T00:00:00Z');
  git(root, ['tag', 'v2.0.0']);

  const history = readVersions(root, 'v1.0.0');

  assert.deepEqual(history.filter(version => version.tag).map(version => version.tag), [
    'v1.0.0',
  ]);
});

test('annotated tag creator date is formatted as a UTC calendar date', context => {
  const root = createRepository(context);
  createCommit(root, 'feat: Adventure');
  git(root, ['-c', 'tag.gpgsign=false', 'tag', '-a', 'v1.0.0', '-m', 'Release'], {
    GIT_COMMITTER_DATE: '2026-01-02T00:30:00+02:00',
  });

  assert.equal(readVersions(root, 'HEAD')[0].date, '2026-01-01');
});

test('Git log parsing handles subject-only commits and trailers-only bodies', context => {
  const root = createRepository(context);
  createCommit(root, 'feat: Subject only');
  createCommit(root, 'fix: Door', 'Player-Fixed: Door opens');

  const commits = readCommits(root, 'HEAD');

  assert.equal(commits[1].body.trim(), '');
  assert.deepEqual(commits[1].trailers, []);
  assert.deepEqual(commits[0].trailers, [{ key: 'Player-Fixed', value: 'Door opens' }]);
});

test('override for an untagged commit appears in pending preview', context => {
  const root = taggedRepository(context);
  const sha = createCommit(root, 'fix: Door');
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'), JSON.stringify({
    schemaVersion: 1,
    overrides: [{ commit: sha, entries: [{ category: 'fixed', text: 'Door opens' }] }],
  }));

  const result = runCli(root, 'preview');

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Door opens/);
});

test('generate compares CRLF-normalized content without rewriting it', context => {
  const root = taggedRepository(context);
  generate(root);
  const path = join(root, 'CHANGELOG_DEV.md');
  const crlfText = readFileSync(path, 'utf8').replace(/\n/g, '\r\n');
  writeFileSync(path, crlfText);

  assert.match(generate(root).stdout, /up to date/);
  assert.equal(readFileSync(path, 'utf8'), crlfText);
});

test('generate only dev leaves player outputs absent', context => {
  const root = taggedRepository(context);
  const result = runCli(root, 'generate', '--only', 'dev');

  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Changed: CHANGELOG_DEV\.md/);
  assert.throws(() => readFileSync(join(root, 'changelog-players.json')), { code: 'ENOENT' });
});

test('check rejects invalid player JSON schema on disk', context => {
  const root = taggedRepository(context);
  generate(root);
  writeFileSync(join(root, 'changelog-players.json'), '{}\n');

  const result = runCli(root, 'check');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Invalid document metadata/);
});

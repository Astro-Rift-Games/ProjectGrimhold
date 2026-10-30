import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { git } from '../src/git.js';
import { readPublicationVersions } from '../src/versions.js';
import { developerVersions } from '../src/commits.js';
import { developerArtifacts } from '../src/render-dev.js';
import { createReleasePlan, writePlannedRelease } from '../src/release-plan.js';

const cli = fileURLToPath(new URL('../src/cli.js', import.meta.url));
const releaseCli = fileURLToPath(new URL('../src/release-cli.js', import.meta.url));
const outputs = ['CHANGELOG_DEV.md', 'CHANGELOG.md', 'changelog-players.json'];
const scratch = mkdtempSync(join(tmpdir(), 'grimhold-release-plan-'));
console.log(`Release plan test scratch: ${scratch}`);
let sequence = 0;

function repository() {
  const root = join(scratch, String(sequence++));
  mkdirSync(root);
  git(root, ['init', '--initial-branch=main']);
  git(root, ['config', 'user.name', 'Release Test']);
  git(root, ['config', 'user.email', 'release@example.invalid']);
  git(root, ['config', 'core.autocrlf', 'false']);
  mkdirSync(join(root, 'tools/changelog'), { recursive: true });
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'),
    JSON.stringify({ schemaVersion: 1, overrides: [] }));
  return root;
}

function commit(root, subject, body = '') {
  git(root, ['-c', 'commit.gpgsign=false', 'commit', '--allow-empty', '-m', subject, '-m', body], {
    GIT_AUTHOR_DATE: '2026-01-02T03:04:05Z', GIT_COMMITTER_DATE: '2026-01-02T03:04:05Z',
  });
  return git(root, ['rev-parse', 'HEAD']).trim();
}

function published(version = '0.3.2') {
  const root = repository();
  commit(root, 'feat: published feature', 'Player-Added: Published adventure.');
  git(root, ['tag', `v${version}`]);
  return root;
}

for (const [subject, base, version, bump] of [
  ['feat: feature', null, '0.1.0', 'minor'],
  ['fix: repair', null, '0.0.1', 'patch'],
  ['perf: faster', '0.3.2', '0.3.3', 'patch'],
  ['feat!: breaking feature', '0.3.2', '0.4.0', 'minor'],
  ['refactor!: breaking API', '1.3.2', '2.0.0', 'major'],
  ['feat: feature', '1.3.2', '1.4.0', 'minor'],
  ['docs: internal', null, null, null],
  ['test: internal', '0.3.2', null, null],
  ['chore: internal', '0.3.2', null, null],
  ['ci: internal', '0.3.2', null, null],
  ['refactor: internal', '0.3.2', null, null],
  ['chore(changelog)!: release metadata', '1.3.2', null, null],
]) {
  test(`SemVer ${subject} from ${base ?? 'no tags'}`, () => {
    const root = base ? published(base) : repository();
    const sourceCommit = commit(root, subject);
    const before = git(root, ['status', '--porcelain']);
    const plan = createReleasePlan(root);
    assert.equal(plan.version, version);
    assert.equal(plan.bump, bump);
    assert.equal(plan.previousTag, base ? `v${base}` : null);
    assert.equal(plan.previousVersion, base);
    assert.equal(plan.sourceCommit, sourceCommit);
    assert.equal(plan.releaseDate, '2026-01-02');
    assert.deepEqual(createReleasePlan(root), JSON.parse(JSON.stringify(plan)));
    assert.equal(git(root, ['status', '--porcelain']), before);
  });
}

test('highest impact wins and existing breaking footer declares functional impact', () => {
  const root = published('1.2.3');
  commit(root, 'fix: repair');
  commit(root, 'feat: addition');
  commit(root, 'docs: contract', 'BREAKING CHANGE: Existing API removed.');
  assert.equal(createReleasePlan(root).version, '2.0.0');
});

test('no changes do not release but distinct same-message commits do', () => {
  const root = published();
  assert.equal(createReleasePlan(root).version, null);
  commit(root, 'feat: published feature', 'Player-Added: Published adventure.');
  assert.equal(createReleasePlan(root).version, '0.4.0');
});

test('pending feature and its revert cancel while technical history remains', () => {
  const root = published();
  const target = commit(root, 'feat: cancelled feature');
  commit(root, 'revert: cancel feature', `This reverts commit ${target}.`);
  assert.equal(createReleasePlan(root).version, null);
  commit(root, 'fix: unrelated repair');
  const plan = createReleasePlan(root);
  assert.equal(plan.version, '0.3.3');
  assert.throws(() => writePlannedRelease(root, plan), /no visible player notes/);
  // Cancelled originals remain in the developer artifact stream, not public notes.
  const versions = readPublicationVersions(root, 'HEAD');
  const archives = developerArtifacts(developerVersions(versions)).map(file => file.text).join('\n');
  assert.match(archives, /cancelled feature/);
  assert.match(archives, /cancel feature/);
});

test('a revert of published functionality warrants a patch', () => {
  const root = published();
  const target = git(root, ['rev-parse', 'HEAD']).trim();
  commit(root, 'revert: remove published feature', `This reverts commit ${target}.`);
  assert.equal(createReleasePlan(root).version, '0.3.3');
});

test('reverting published internal automation never creates a release', () => {
  const root = repository();
  const target = commit(root, 'chore(changelog)!: generated metadata');
  git(root, ['tag', 'v0.1.0']);
  commit(root, 'revert: remove metadata', `This reverts commit ${target}.`);
  assert.equal(createReleasePlan(root).version, null);
});

test('features without public notes increment version without inventing notes', () => {
  const root = published();
  commit(root, 'feat: private implementation');
  const plan = createReleasePlan(root);
  assert.equal(plan.version, '0.4.0');
  assert.throws(() => writePlannedRelease(root, plan), /no visible player notes/);
  assert.equal(existsSync(join(root, 'changelog-players.json')), false);
});

test('leading-zero tags and prereleases are not stable publication tags', () => {
  const root = repository();
  commit(root, 'docs: setup');
  git(root, ['tag', 'v01.2.3']);
  git(root, ['tag', 'v0.4.0-rc.1']);
  commit(root, 'fix: first fix');
  assert.equal(createReleasePlan(root).version, '0.0.1');
});

test('nonmonotonic tags and multiple stable tags on one commit fail closed', () => {
  const root = published('1.2.0');
  commit(root, 'fix: repair');
  git(root, ['tag', 'v1.1.0']);
  assert.throws(() => createReleasePlan(root), /publication history/i);
  const ambiguous = published();
  git(ambiguous, ['tag', 'v0.4.0']);
  assert.throws(() => createReleasePlan(ambiguous), /publication history/i);
});

test('stable tag on merged side branch fails first-parent publication check', () => {
  const root = published();
  git(root, ['checkout', '-b', 'feature']);
  commit(root, 'feat: side branch');
  git(root, ['tag', 'v0.4.0']);
  git(root, ['checkout', 'main']);
  git(root, ['-c', 'commit.gpgsign=false', 'merge', '--no-ff', 'feature', '-m', 'merge feature']);
  assert.throws(() => createReleasePlan(root), /first-parent/i);
});

test('a version already used on unrelated history cannot be reused', () => {
  const root = published();
  git(root, ['checkout', '-b', 'other']);
  commit(root, 'docs: unrelated');
  git(root, ['tag', 'v0.3.3']);
  git(root, ['checkout', 'main']);
  commit(root, 'fix: repair');
  assert.throws(() => createReleasePlan(root), /already exists/i);
});

test('prepare preserves history, Unreleased, overrides and post-tag renderer equivalence', () => {
  const root = published();
  const sha = commit(root, 'feat: new system');
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'), JSON.stringify({
    schemaVersion: 1, overrides: [{ commit: sha, entries: [{ category: 'added', text: 'New adventure.' }] }],
  }));
  const plan = createReleasePlan(root);
  writePlannedRelease(root, plan);
  const expected = outputs.map(file => readFileSync(join(root, file), 'utf8'));
  assert.match(expected[0], /Unreleased/);
  assert.match(expected[0], /0\.3\.2/);
  const document = JSON.parse(expected[2]);
  assert.equal(document.releases[0].commit, sha);
  assert.equal(document.releases[0].categories[0].entries[0].text, 'New adventure.');
  writePlannedRelease(root, plan);
  assert.deepEqual(outputs.map(file => readFileSync(join(root, file), 'utf8')), expected);
  git(root, ['tag', `v${plan.version}`]);
  const generated = spawnSync(process.execPath, [cli, 'generate', '--root', root, '--strict'], { encoding: 'utf8' });
  assert.equal(generated.status, 0, generated.stderr);
  assert.deepEqual(outputs.map(file => readFileSync(join(root, file), 'utf8')), expected);
});

test('metadata commit changes only release origin references in post-tag artifacts', () => {
  const root = published();
  commit(root, 'fix: repair', 'Player-Fixed: Adventure works again.');
  const plan = createReleasePlan(root);
  writePlannedRelease(root, plan);
  const expected = outputs.map(file => readFileSync(join(root, file), 'utf8'));
  git(root, ['add', ...outputs]);
  const finalSha = commit(root, `chore(changelog): publish v${plan.version}`);
  git(root, ['tag', `v${plan.version}`]);
  const result = spawnSync(process.execPath, [cli, 'generate', '--root', root, '--strict'], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const actual = outputs.map(file => readFileSync(join(root, file), 'utf8'));
  assert.equal(actual[0], expected[0].replace(
    `Commit: [${plan.sourceCommit.slice(0, 7)}](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/${plan.sourceCommit})`,
    `Commit: [${finalSha.slice(0, 7)}](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/${finalSha})`,
  ));
  assert.equal(actual[1], expected[1]);
  const document = JSON.parse(expected[2]);
  document.releases[0].commit = finalSha;
  assert.deepEqual(JSON.parse(actual[2]), document);
});

for (const body of ['Player-Fix: Invalid key', 'Player-Added: ', 'Player-Added: misplaced\n\nNot a footer']) {
  test(`invalid public notes block prepare before writes: ${body}`, () => {
    const root = published();
    commit(root, 'feat: invalid note', body);
    assert.throws(() => writePlannedRelease(root, createReleasePlan(root)), /player|trailer|note/i);
    assert.equal(existsSync(join(root, 'CHANGELOG_DEV.md')), false);
  });
}

test('prepare rejects invalid overrides, modified plans and no-release plans without writes', () => {
  const root = published();
  assert.throws(() => writePlannedRelease(root, createReleasePlan(root)), /no release/i);
  commit(root, 'fix: repair');
  const plan = createReleasePlan(root);
  assert.throws(() => writePlannedRelease(root, { ...plan, version: '99.0.0' }), /plan/i);
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'), '{}');
  assert.throws(() => writePlannedRelease(root, plan), /overrides/i);
  assert.equal(existsSync(join(root, 'CHANGELOG_DEV.md')), false);
});

test('additive release CLI prints JSON and rejects unknown, duplicate and missing arguments', () => {
  const root = published();
  commit(root, 'fix: repair', 'Player-Fixed: Repairs remain available.');
  for (const command of ['plan', 'prepare']) {
    const result = spawnSync(process.execPath, [releaseCli, command, '--root', root, '--ref', 'HEAD'], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(JSON.parse(result.stdout).version, '0.3.3');
  }
  for (const args of [['wat'], ['plan', '--foo', 'x'], ['plan', '--ref'], ['plan', '--ref', 'HEAD', '--ref', 'HEAD'], ['plan', 'extra']]) {
    const result = spawnSync(process.execPath, [releaseCli, ...args, '--root', root], { encoding: 'utf8' });
    assert.equal(result.status, 2, result.stderr);
    assert.match(result.stderr, /Usage:/);
  }
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { git } from '../src/git.js';
import { prepareCandidate, publishCandidate, assertSnapshots } from '../scripts/weekly-release.js';

// Retained under system temp for parent-owned cleanup; never touch the official remote.
function fixture() {
  const folder = mkdtempSync(join(tmpdir(), 'grimhold-weekly-'));
  const root = join(folder, 'work');
  const remote = join(folder, 'remote.git');
  mkdirSync(root);
  git(root, ['init', '--initial-branch=main']);
  git(root, ['config', 'user.name', 'Release Test']);
  git(root, ['config', 'user.email', 'release@example.invalid']);
  git(root, ['config', 'core.autocrlf', 'false']);
  mkdirSync(join(root, 'tools/changelog'), { recursive: true });
  writeFileSync(join(root, 'tools/changelog/player-overrides.json'), '{"schemaVersion":1,"overrides":[]}\n');
  mkdirSync(join(root, 'Project Grimhold/ProjectSettings'), { recursive: true });
  writeFileSync(join(root, 'Project Grimhold/ProjectSettings/ProjectSettings.asset'), 'PlayerSettings:\n  bundleVersion: 0.0.0\n');
  git(root, ['add', '.']);
  commit(root, 'chore: initialize');
  git(root, ['tag', 'v0.0.0']);
  git(root, ['branch', 'New-Testing']);
  git(root, ['init', '--bare', remote]);
  git(root, ['remote', 'add', 'origin', remote]);
  git(root, ['push', '--tags', 'origin', 'main', 'New-Testing']);
  git(root, ['checkout', 'New-Testing']);
  return { root, folder, remote, output: join(folder, 'output') };
}
function commit(root, message) {
  git(root, ['-c', 'commit.gpgsign=false', 'commit', '--allow-empty', '-am', message], {
    GIT_AUTHOR_DATE: '2026-10-19T12:00:00Z', GIT_COMMITTER_DATE: '2026-10-19T12:00:00Z',
  });
}
function change(f, message = 'feat: adventure\n\nPlayer-Added: Explore a new adventure.') {
  writeFileSync(join(f.root, 'feature.txt'), message);
  git(f.root, ['add', 'feature.txt']);
  commit(f.root, message);
  git(f.root, ['push', 'origin', 'New-Testing']);
}
function heads(f) { return git(f.root, ['ls-remote', '--heads', 'origin']); }
function api() {
  const releases = new Map();
  return {
    releases,
    async find(tag) { return releases.get(tag) ?? null; },
    async publish(tag, sha, notes, bytes) {
      const previous = releases.get(tag);
      if (previous) {
        assert.equal(previous.sha, sha);
        assert.equal(previous.notes, notes);
        assert.equal(previous.bytes, bytes);
      }
      releases.set(tag, { sha, notes, bytes, draft: false });
    },
  };
}
const validated = async () => {}; // Explicit simulation, never a real Unity verdict.

test('candidate is a real merge, deterministic, and preparation leaves both remotes intact', async () => {
  const f = fixture(); change(f); const before = heads(f);
  const a = await prepareCandidate(f.root, f.output);
  const b = await prepareCandidate(f.root, join(f.folder, 'repeat'));
  assert.equal(a.target, b.target);
  assert.equal(a.plan.version, '0.1.0');
  assert.equal(heads(f), before);
  assert.equal(git(a.candidate, ['show', '-s', '--format=%P', a.plan.sourceCommit]).trim().split(' ').length, 2);
});
test('no new commits is a no-op', async () => {
  const f = fixture(); const s = await prepareCandidate(f.root, f.output);
  assert.equal(s.phase, 'no-changes');
});
test('internal changes integrate without a new release or version change', async () => {
  const f = fixture(); change(f, 'docs: clarify controls');
  const s = await prepareCandidate(f.root, f.output);
  assert.equal(s.plan.version, null);
  await publishCandidate(s, { validate: validated, api: api() });
  assert.equal(git(s.candidate, ['show', 'origin/main:Project Grimhold/ProjectSettings/ProjectSettings.asset']).includes('0.0.0'), true);
  assert.equal(git(s.candidate, ['tag', '--list', 'v0.0.1']).trim(), '');
});
test('merge conflicts abort before version metadata and preserve both remote heads', async () => {
  const f = fixture();
  writeFileSync(join(f.root, 'conflict.txt'), 'base'); git(f.root, ['add', '.']); commit(f.root, 'docs: base');
  git(f.root, ['push', 'origin', 'HEAD:main', 'HEAD:New-Testing']);
  git(f.root, ['checkout', 'main']); git(f.root, ['reset', '--hard', 'New-Testing']);
  writeFileSync(join(f.root, 'conflict.txt'), 'main'); commit(f.root, 'docs: main'); git(f.root, ['push', 'origin', 'main']);
  git(f.root, ['checkout', 'New-Testing']); writeFileSync(join(f.root, 'conflict.txt'), 'dev'); commit(f.root, 'feat: dev'); git(f.root, ['push', 'origin', 'New-Testing']);
  const before = heads(f);
  await assert.rejects(prepareCandidate(f.root, f.output), /conflict.*conflict.txt/is);
  assert.equal(heads(f), before);
});
for (const branch of ['main', 'New-Testing']) test(`concurrent ${branch} change rejects publication before first push`, async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output);
  git(f.root, ['checkout', branch]); commit(f.root, 'docs: concurrent'); git(f.root, ['push', 'origin', branch]);
  const before = heads(f);
  await assert.rejects(publishCandidate(s, { validate: validated, api: api() }), /changed/i);
  assert.equal(heads(f), before);
});
test('mandatory validation failure blocks integration, tags and releases', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const before = heads(f); const remote = api();
  await assert.rejects(publishCandidate(s, { validate: async () => { throw Error('Unity failed'); }, api: remote }), /Unity failed/);
  assert.equal(heads(f), before); assert.equal(remote.releases.size, 0);
});
test('dry-run validates and renders without changing either remote', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const before = heads(f); const remote = api();
  await publishCandidate(s, { dryRun: true, validate: validated, api: remote });
  assert.equal(s.phase, 'dry-run'); assert.equal(heads(f), before); assert.equal(remote.releases.size, 0);
});
test('successful publication pins tag and JSON to final commit and syncs main back', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const remote = api();
  await publishCandidate(s, { validate: validated, api: remote });
  assert.equal(s.phase, 'complete');
  assert.equal(git(s.candidate, ['rev-parse', 'v0.1.0^{commit}']).trim(), s.target);
  assert.equal(JSON.parse(remote.releases.get('v0.1.0').bytes).releases[0].commit, s.target);
  git(s.candidate, ['merge-base', '--is-ancestor', 'origin/main', 'origin/New-Testing']);
  assert.equal(git(s.candidate, ['show', `${s.target}:Project Grimhold/ProjectSettings/ProjectSettings.asset`]).includes('0.1.0'), true);
});
test('release API failure after integration is recoverable on the exact existing target', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const remote = api();
  await assert.rejects(publishCandidate(s, { validate: validated, api: { ...remote, async publish() { throw Error('network lost'); } } }), /network lost/);
  const resumed = await prepareCandidate(f.root, join(f.folder, 'resume'), { api: remote });
  assert.equal(resumed.target, s.target); assert.equal(resumed.recovery, true);
  await publishCandidate(resumed, { validate: validated, api: remote });
  assert.equal(resumed.phase, 'complete'); assert.equal(remote.releases.size, 1);
});
test('repeated execution after completion does not generate another version', async () => {
  const f = fixture(); change(f); const remote = api(); const first = await prepareCandidate(f.root, f.output);
  await publishCandidate(first, { validate: validated, api: remote }); const before = heads(f);
  const again = await prepareCandidate(f.root, join(f.folder, 'again'), { api: remote });
  assert.equal(again.phase, 'no-changes'); assert.equal(heads(f), before); assert.equal(remote.releases.size, 1);
});
test('wrong existing tag is rejected without modifying branches', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const before = heads(f);
  git(f.root, ['tag', 'v0.1.0', 'main']); git(f.root, ['push', 'origin', 'v0.1.0']);
  await assert.rejects(publishCandidate(s, { validate: validated, api: api() }), /tag.*(exists|different|match)/i);
  assert.equal(heads(f), before);
});
test('rejected main push never creates tags or releases', async () => {
  const f = fixture(); change(f); const s = await prepareCandidate(f.root, f.output); const before = heads(f); const remote = api();
  // Bare receiver denies every update; this is a local simulation of permission/protection rejection.
  git(f.remote, ['config', 'receive.denyCurrentBranch', 'refuse']);
  git(f.remote, ['symbolic-ref', 'HEAD', 'refs/heads/main']);
  git(f.remote, ['config', 'core.bare', 'false']);
  await assert.rejects(publishCandidate(s, { validate: validated, api: remote }));
  assert.equal(heads(f), before); assert.equal(remote.releases.size, 0);
});
test('strict invalid public notes fail preparation before changing remote refs', async () => {
  const f = fixture(); change(f, 'feat: adventure\n\nPlayer-Added:'); const before = heads(f);
  await assert.rejects(prepareCandidate(f.root, f.output), /warning|invalid|empty|footer/i);
  assert.equal(heads(f), before);
});
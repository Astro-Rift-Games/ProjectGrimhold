import test from 'node:test';
import assert from 'node:assert/strict';
import { parseCommit, developerVersions } from '../src/commits.js';
import { developerArtifacts, renderDev } from '../src/render-dev.js';
const sha = n => n.toString(16).padStart(40, '0');
const source = ['Assets/Scripts/Combat/AimFrameOrbit.cs'];
const c = (subject, n, extra = {}) => parseCommit({ sha: sha(n), subject, body: '', date: '2026-01-02T12:00:00Z', files: source, ...extra });
const versions = commits => developerVersions([{ version: 'Unreleased', commits }]);
const bullets = text => text.split('\n').filter(line => line.startsWith('- '));
const revert = (n, target, extra = {}) => c('revert(combat): undo previous approach', n, { body: `This reverts commit ${sha(target)}.`, ...extra });
const hasRefs = (text, numbers) => numbers.forEach(n => assert.ok(text.includes(`/commit/${sha(n)}`), `missing reference ${n}`));

test('related changes yield one concise synthesis, not concatenated descriptions or types', () => {
  const records = [c('fix(combat): prevent repeated hit feedback (TASK-220)', 2), c('feat(combat): add hit feedback (TASK-220)', 1)];
  const output = developerArtifacts(versions(records));
  const line = bullets(output[0].text)[0];
  assert.equal(bullets(output[0].text).length, 1);
  assert.match(line, /hit feedback/i); assert.match(line, /fixes/i);
  assert.doesNotMatch(line, /prevent repeated hit feedback|\*\*Features:|\*\*Bug Fixes:/);
  hasRefs(line, [1, 2]);
  const archive = output.filter(f => f.name.endsWith('.md') && f.name !== 'CHANGELOG_DEV.md').map(f => f.text).join('\n');
  for (const record of records) assert.ok(archive.includes(record.subject));
  assert.match(archive, /Classification: Features|Classification: Bug Fixes/);
});

test('same-period explicit revert never advertises the implementation as surviving', () => {
  const text = renderDev(versions([revert(2, 1), c('feat(combat): implement aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Reverted/i);
  assert.doesNotMatch(bullets(text)[0], /Implemented|Added/i); hasRefs(text, [1, 2]);
});

test('revert of revert restores the effect and preserves the full supporting chain', () => {
  const text = renderDev(versions([revert(3, 2), revert(2, 1), c('feat(combat): add aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Restored|Reintroduced/i);
  hasRefs(text, [1, 2, 3]);
});

test('positive removal wins over an earlier implementation even with a misleading author date', () => {
  const text = renderDev(versions([
    c('refactor(combat): drop aim-frame orbit', 2, { date: '2026-01-01T00:00:00Z' }),
    c('feat(combat): implement aim-frame orbit', 1, { date: '2026-01-20T00:00:00Z' }),
  ]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Removed aim-frame orbit/i);
  assert.doesNotMatch(bullets(text)[0], /Implemented/i); hasRefs(text, [1, 2]);
});

test('explicit named source removal resolves the real free-aim contradiction without removing all FreeAim', () => {
  const files = ['Assets/Scripts/Player/Presentation/FreeAimAnchor.cs'];
  const text = renderDev(versions([
    c('refactor(presentation): drop procedural hand pinning and the aim-frame orbit', 2, { files, body: 'Removes FreeAimAnchor. FreeAim and per-weapon spawn distance stay.' }),
    c('feat(presentation): orbit free-aim weapons around the body in the aim frame', 1, { files }),
  ]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Removed procedural hand pinning and the aim-frame orbit/i);
  assert.doesNotMatch(bullets(text)[0], /orbit free-aim weapons|Removed FreeAim(?:\s|\.)/i); hasRefs(text, [1, 2]);
});

test('replacement chains summarize the final approach rather than the obsolete implementations', () => {
  const text = renderDev(versions([
    c('refactor(combat): replace lazy snapshot cache with bounded snapshot cache', 3),
    c('refactor(combat): replace eager snapshot cache with lazy snapshot cache', 2),
    c('feat(combat): add eager snapshot cache', 1),
  ]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Replaced lazy snapshot cache with bounded snapshot cache/i);
  assert.doesNotMatch(bullets(text)[0], /Added eager snapshot cache/i); hasRefs(text, [1, 2, 3]);
});

test('a later reintroduction supersedes an earlier removal within the period', () => {
  const text = renderDev(versions([c('feat(combat): add aim-frame orbit', 3), c('refactor(combat): remove aim-frame orbit', 2), c('feat(combat): implement aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Restored|Reintroduced/i); hasRefs(text, [1, 2, 3]);
});

test('shared source, scope or task alone does not turn unrelated removal into feature cancellation', () => {
  const text = renderDev(versions([c('refactor(combat): remove camera shake (TASK-220)', 2), c('feat(combat): add shield blocking (TASK-220)', 1)]));
  assert.equal(bullets(text).length, 2); assert.match(text, /shield blocking/i); assert.match(text, /Removed camera shake/i);
  assert.doesNotMatch(text, /Reverted shield|Removed shield/i);
});

test('negative, deferred and retained mechanisms do not count as positive removal evidence', () => {
  for (const body of ['Do not remove AimFrameOrbit.', 'Future: remove AimFrameOrbit.', 'Removes camera shake while retaining AimFrameOrbit.']) {
    const text = renderDev(versions([c('fix(combat): correct camera offsets', 2, { body }), c('feat(combat): implement aim-frame orbit', 1)]));
    assert.match(text, /Implemented aim-frame orbit/i); assert.doesNotMatch(text, /Removed aim-frame orbit|Reverted aim-frame orbit/i);
  }
});

test('a source-changing maintenance removal is lifecycle evidence even when it is not an ordinary headline', () => {
  const text = renderDev(versions([c('chore(combat): remove aim-frame orbit', 2), c('feat(combat): add aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Removed aim-frame orbit/i); hasRefs(text, [1, 2]);
});

test('unknown revert targets cannot cancel unrelated records', () => {
  const text = renderDev(versions([revert(2, 99), c('feat(combat): implement shield blocking', 1)]));
  assert.match(text, /Implemented shield blocking/i); assert.doesNotMatch(text, /Reverted shield/i);
});

test('canonical alias references are respected when a revert names a different supporting SHA', () => {
  const records = [revert(2, 11), c('feat(combat): implement aim-frame orbit', 1), c('feat(combat): implement aim-frame orbit', 11)];
  const input = versions(records);
  input[0].entries = [input[0].entries[0], { ...input[0].entries[1], refs: [sha(1), sha(11)] }];
  const text = renderDev(input); assert.match(text, /Reverted/i); assert.doesNotMatch(text, /Implemented aim-frame orbit/i); hasRefs(text, [1, 2, 11]);
});

test('initial tagged aggregation preserves topological order rather than sorting author months', () => {
  const input = developerVersions([{ version: '1.0.0', tag: 'v1.0.0', commits: [revert(2, 1, { date: '2026-01-01T00:00:00Z' }), c('feat(combat): implement aim-frame orbit', 1, { date: '2026-02-01T00:00:00Z' })] }]);
  assert.match(renderDev(input), /Reverted/i); assert.doesNotMatch(renderDev(input), /Implemented aim-frame orbit/i);
});

test('lifecycle projection is period-local, pure and leaves historical evidence unchanged', () => {
  const input = versions([revert(2, 1, { date: '2026-02-01T00:00:00Z' }), c('feat(combat): implement aim-frame orbit', 1)]);
  const snapshot = JSON.stringify(input); const first = developerArtifacts(input);
  assert.match(first[0].text, /Implemented aim-frame orbit/i);
  assert.deepEqual(first, developerArtifacts(input)); assert.equal(JSON.stringify(input), snapshot);
  assert.ok(first.find(f => f.name === 'docs/changelog/history-2026-01.md').text.includes('feat(combat): implement aim-frame orbit'));
});

test('a component mentioned as context or a modifier is not evidence that the whole component was removed', () => {
  for (const description of ['remove debug offsets from AimFrameOrbit', 'remove aim-frame orbit debug overlay']) {
    const text = renderDev(versions([c(`refactor(combat): ${description}`, 2), c('feat(combat): implement aim-frame orbit', 1)]));
    assert.equal(bullets(text).length, 2); assert.match(text, /Implemented aim-frame orbit/i);
  }
});

test('a new implementation after an explicit revert is synthesized as a reintroduction, not contradictory headlines', () => {
  const text = renderDev(versions([c('feat(combat): add aim-frame orbit', 3), revert(2, 1), c('feat(combat): implement aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.match(bullets(text)[0], /Restored|Reintroduced/i); hasRefs(text, [1, 2, 3]);
});
test('parallel migrations synthesize their shared target and subjects in one sentence', () => {
  const text = renderDev(versions([
    c('feat(combat): migrate Compound Bow to weapon-driven directional attack', 3),
    c('feat(combat): migrate Long Bow to weapon-driven directional attack', 2),
    c('feat(combat): migrate Magic Staff to generic directional attack', 1),
  ]));
  const line = bullets(text)[0]; assert.equal(bullets(text).length, 1);
  for (const name of ['Compound Bow', 'Long Bow', 'Magic Staff']) assert.ok(line.includes(name));
  assert.equal((line.match(/Migrated/g) || []).length, 1); assert.match(line, /directional attack/);
  hasRefs(line, [1, 2, 3]);
});
test('a verbose lifecycle statement is bounded without reviving the invalidated feature claim', () => {
  const text = renderDev(versions([c('chore(combat): remove aim-frame orbit, ' + 'additional implementation detail '.repeat(20), 2), c('feat(combat): implement aim-frame orbit', 1)]));
  assert.equal(bullets(text).length, 1); assert.ok(bullets(text)[0].split(' ([')[0].length <= 300);
  assert.doesNotMatch(bullets(text)[0], /Implemented aim-frame orbit/i); hasRefs(text, [1, 2]);
});
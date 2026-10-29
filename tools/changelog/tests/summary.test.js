import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { parseCommit, developerVersions } from '../src/commits.js';
import { developerArtifacts, renderDev } from '../src/render-dev.js';

const sha = n => n.toString(16).padStart(40, '0');
const commit = (subject, n, extra = {}) => parseCommit({ sha: sha(n), subject, body: '', date: '2026-01-02T12:00:00Z', files: [], ...extra });
const history = commits => developerVersions([{ version: 'Unreleased', commits }]);
const lines = text => text.split('\n').filter(line => line.startsWith('- '));

test('concrete functional outcomes outrank recent routine tooling, docs and tests', () => {
  const text = renderDev(history([
    commit('feat(tooling): add audit report', 1), commit('feat(tooling): add editor toolbar', 2),
    commit('docs: explain combat', 3), commit('test: cover inventory rules', 4),
    commit('feat(combat): implement shield blocking', 5), commit('feat(loot): collect partial pickups', 6),
    commit('refactor(tooling): decouple publication state from transport', 7),
  ]));
  assert.match(text, /Implemented shield blocking/i); assert.match(text, /Enabled collection of partial pickups/i);
  assert.match(text, /Decoupled publication state from transport/i);
  assert.doesNotMatch(text, /add audit report|add editor toolbar|explain combat|cover inventory/);
  assert.doesNotMatch(text, /\| System \|/); assert.match(text, /Classification report/);
});

test('system diversity prevents three recent same-system features from monopolizing the period', () => {
  const text = renderDev(history([
    commit('feat(combat): implement shield blocking', 1), commit('feat(combat): implement dagger attacks', 2),
    commit('feat(combat): implement bow attacks', 3), commit('feat(movement): implement collision sliding', 4),
    commit('feat(loot): implement container transfer', 5),
  ]));
  assert.match(text, /Implemented shield blocking/i); assert.match(text, /Implemented collision sliding/i);
  assert.match(text, /Implemented container transfer/i); assert.doesNotMatch(text, /dagger attacks|bow attacks/);
  assert.equal(lines(text).length, 3);
});

test('change kind contributes to relevance rather than always taking the latest record', () => {
  const text = renderDev(history([
    commit('refactor(combat): simplify hit calculation', 1), commit('refactor(combat): rename attack adapter', 2),
    commit('fix(combat): prevent duplicate damage', 3), commit('feat(combat): implement shield blocking', 4),
  ]));
  assert.ok(text.indexOf('Implemented shield blocking') < text.indexOf('Simplified hit calculation'));
  assert.match(text, /Prevented duplicate damage/i);
});

test('explicit same-task grouping synthesizes the advance and preserves all canonical commit references', () => {
  const versions = history([
    commit('feat(combat): add hit feedback (TASK-220)', 1), commit('fix(combat): prevent repeated hit feedback (TASK-220)', 2),
  ]);
  versions[0].entries[0].refs = [sha(1), sha(11), sha(12), sha(13)];
  const text = renderDev(versions); const advances = lines(text);
  assert.equal(advances.length, 1);
  assert.match(advances[0], /Added hit feedback/i); assert.match(advances[0], /related fixes/); assert.doesNotMatch(advances[0], /prevent repeated hit feedback/);
  for (const n of [1, 2, 11, 12, 13]) assert.ok(advances[0].includes(`/commit/${sha(n)}`));
  assert.ok(advances[0].length < 800); assert.doesNotMatch(text, /completed|fully implemented/i);
});

test('same scope plus shared source and outcome terms groups related records without a task id', () => {
  const extra = { files: ['Assets/Scripts/Progression/AttributeAssignmentRules.cs'] };
  const text = renderDev(history([
    commit('feat(progression): add attribute point assignment', 1, extra),
    commit('fix(progression): validate attribute point assignment', 2, extra),
  ]));
  assert.equal(lines(text).length, 1);
  assert.match(text, /Added attribute point assignment/i); assert.match(text, /related fixes/i); assert.doesNotMatch(text, /validate attribute point assignment/);
});

test('system/scope, common test/meta paths or deferred task references alone never justify grouping', () => {
  for (const files of [[], ['Assets/Prefabs/NetworkPlayer.prefab.meta'], ['Assets/Tests/Combat/HitTests.cs']]) {
    const text = renderDev(history([
      commit('feat(combat): add shield stamina rules', 1, { files, body: 'Future follow-up: TASK-999.' }),
      commit('feat(combat): add sword stamina rules', 2, { files, body: 'Not part of TASK-999.' }),
    ]));
    if (files.some(f => f.endsWith('.meta') || f.includes('/Tests/'))) { assert.equal(lines(text).length, 0); continue; }
    assert.equal(lines(text).length, 2); assert.doesNotMatch(text, /_Related records:/);
  }
});

test('grouping is pairwise, not a transitive chain, and each group has at most three records', () => {
  const a = 'Assets/Scripts/Combat/ShieldRules.cs'; const b = 'Assets/Scripts/Combat/ShieldState.cs';
  const text = renderDev(history([
    commit('feat(combat): add shield stamina rules', 1, { files: [a] }),
    commit('fix(combat): validate shield stamina rules', 2, { files: [a, b] }),
    commit('refactor(combat): simplify shield stamina rules', 3, { files: [b] }),
  ]));
  assert.equal(lines(text).length, 2);
  const grouped = renderDev(history(Array.from({ length: 8 }, (_, i) => commit(`feat(combat): add shield variant ${i} (TASK-220)`, i + 1))));
  assert.equal(lines(grouped).length, 3);
  assert.ok(lines(grouped).every(line => (line.match(/\/commit\//g) || []).length <= 3));
});

test('initial untagged history is explicitly separate from a pending release range', () => {
  const text = renderDev(history([
    commit('feat(combat): add shield blocking', 1), commit('feat(loot): add container transfer', 2, { date: '2025-12-01T12:00:00Z' }),
  ]));
  assert.match(text, /## Unreleased/); assert.match(text, /No release baseline exists/);
  assert.match(text, /## Initial development history/); assert.doesNotMatch(text, /Unreleased history/);
  assert.match(text, /2026-01/); assert.match(text, /2025-12/);
  assert.doesNotMatch(text, /Unreleased comparison/);
});

test('published releases stay organized by version while genuine Unreleased retains its compare base', () => {
  const versions = developerVersions([
    { version: '1.0.0', tag: 'v1.0.0', date: '2026-02-01', commits: [commit('feat(combat): add shield', 1), commit('feat(loot): add containers', 2, { date: '2025-12-01T12:00:00Z' })] },
    { version: '1.1.0', tag: 'v1.1.0', date: '2026-03-01', commits: [commit('feat(movement): add sliding', 3)] },
    { version: 'Unreleased', commits: [commit('fix(combat): prevent duplicate damage', 4)] },
  ]);
  const text = renderDev(versions);
  assert.match(text, /compare\/v1.1.0\.\.\.New-Testing/);
  assert.match(text, /### \[v1\.0\.0/); assert.match(text, /### \[v1\.1\.0/);
  assert.equal((text.match(/### \[v1\.0\.0/g) || []).length, 1);
  assert.doesNotMatch(text, /initial history through/); assert.match(text, /### \[Unreleased/);
});

test('empty and secondary-only periods never imply delivered gameplay', () => {
  const empty = renderDev(history([])); assert.match(empty, /No release baseline exists/);
  const secondary = renderDev(history([commit('docs: explain combat', 1)]));
  assert.doesNotMatch(secondary, /No unreleased changes/); assert.match(secondary, /No eligible concrete advances/);
});

test('summary selection is pure and deterministic and preserves all non-summary artifact bytes', () => {
  const versions = history([commit('feat(combat): add hit confirmation', 1, { body: 'Original detail.', files: ['Assets/Scripts/Combat/Hit.cs'] })]);
  const snapshot = JSON.stringify(versions);
  const first = developerArtifacts(versions);
  assert.deepEqual(first, developerArtifacts(versions)); assert.equal(JSON.stringify(versions), snapshot);
  const hashes = Object.fromEntries(first.slice(1).map(f => [f.name, createHash('sha256').update(f.text).digest('hex')]));
  assert.deepEqual(hashes, BASELINE_ARTIFACT_HASHES);
});

const BASELINE_ARTIFACT_HASHES = {
  "docs/changelog/README.md": "b2f172ebdd293b625fa8b45d36fc40e42da74516ce9c76a7990d38c45ae9745d",
  "docs/changelog/history-2026-01.md": "c718db146b6f5ed8be931eedb21f85ce31de3b5a8bbdfaccad8916d546a62a1d",
  "docs/changelog/report.json": "c71cabe20c297f33bab7386801485e76113fe426f2b31ca406ef2a3d6069c3e9",
  "docs/changelog/generated-files.json": "66925814cc2fbce06c6e4ef686a53ca09a46c942422d68725ade7aa2dd566714"
};

test('routine configuration classified as infrastructure cannot outrank functional or architectural advances', () => {
  const text = renderDev(history([
    commit('build(combat): configure slime animator overrides', 1),
    commit('build(backend): prepare server for cloud deployment', 2),
    commit('feat(combat): implement shield blocking', 3),
    commit('feat(loot): implement container transfer', 4),
    commit('refactor(tooling): decouple publication state from transport', 5),
  ]));
  assert.match(text, /Implemented shield blocking/i); assert.match(text, /Implemented container transfer/i);
  assert.match(text, /Decoupled publication state from transport/i);
  assert.doesNotMatch(text, /configure slime|prepare server/);
});
test('summary system heading prefers scope or strongest path evidence rather than alphabetical labels', () => {
  const files = ['Assets/Scripts/Combat/EquipmentViewModel.cs', 'Assets/Scripts/UI/PreparationCard.cs', 'Assets/Scripts/UI/PreparationPanel.cs'];
  const text = renderDev(history([commit('feat(preparation): inspect member equipment', 1, { files })]));
  assert.match(lines(text)[0], /^- \*\*UI\*\*/);
});
test('feature-typed test/document/editor paths do not crowd out runtime advances', () => {
  const text = renderDev(history([
    commit('feat(combat): add shield test harness', 1, { files: ['Assets/Tests/Combat/ShieldTests.cs'] }),
    commit('feat(combat): add shield guide', 2, { files: ['Docs/Combat/Shield.md'] }),
    commit('feat(combat): add animation inspector tool', 3, { files: ['Assets/Editor/Combat/AnimationInspector.cs'] }),
    commit('feat(movement): implement collision sliding', 4), commit('feat(loot): implement container transfer', 5),
    commit('feat(ui): implement equipment inspection', 6),
  ]));
  assert.doesNotMatch(text, /shield test harness|shield guide|animation inspector tool/);
  for (const description of ['Implemented collision sliding', 'Implemented container transfer', 'Implemented equipment inspection']) assert.ok(text.includes(description));
});
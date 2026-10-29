import test from 'node:test';
import assert from 'node:assert/strict';
import { parseCommit, developerVersions } from '../src/commits.js';
import { classifyCommit } from '../src/classify.js';
import { developerArtifacts, renderDev } from '../src/render-dev.js';

const sha = index => index.toString(16).padStart(40, '0');
function commit(subject, index = 1, extra = {}) {
  return parseCommit({ sha: sha(index), subject, body: '', date: '2026-01-02T12:00:00Z', files: [], ...extra });
}
function versions(commits, extra = []) {
  return developerVersions([{ version: '1.0.0', tag: 'v1.0.0', date: '2026-02-01', commit: sha(999), commits }, ...extra]);
}
const report = artifacts => JSON.parse(artifacts.find(file => file.name.endsWith('/report.json')).text);

test('explicit Conventional Commit type has precedence over historical wording', () => {
  const result = classifyCommit(commit('refactor(combat): Fix damage adapters'));
  assert.equal(result.section, 'Refactoring');
  assert.equal(result.confidence, 'explicit');
  assert.ok(result.systems.includes('Combat'));
});

test('historical classification requires an anchored action with a meaningful object', () => {
  assert.equal(classifyCommit(commit('Added inventory sorting')).section, 'Features');
  assert.equal(classifyCommit(commit('Fixed duplicate loot pickups')).section, 'Bug Fixes');
  assert.equal(classifyCommit(commit('Refactored combat resolvers')).section, 'Refactoring');
  assert.equal(classifyCommit(commit('Maybe we should fix damage later')).section, 'Other Changes');
  assert.equal(classifyCommit(commit('Inventory changes')).section, 'Other Changes');
});

test('paths supply system evidence but never invent historical behavior', () => {
  const result = classifyCommit(commit('Update', 1, { files: ['Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs'] }));
  assert.equal(result.section, 'Other Changes');
  assert.ok(result.systems.includes('Combat'));
  assert.equal(result.generic, true);
  assert.ok(result.diagnostics.some(item => item.code === 'ambiguous-kind'));
  assert.ok(result.evidence.some(item => item.source === 'path'));
});

test('system evidence supports multiple paths without vague prose substring matching', () => {
  const result = classifyCommit(commit('Fix invalid state', 1, {
    body: 'A network of ideas, combativeness and movemental prose.',
    files: ['Assets/Scripts/Combat/Weapon.cs', 'Assets/Scripts/UI/Hud.cs'],
  }));
  assert.deepEqual(result.systems, ['Combat', 'UI']);
  assert.deepEqual(classifyCommit(commit('Fix invalid state', 1, { body: 'network of ideas' })).systems, []);
});

test('secondary documentation, tests and maintenance remain classified independently', () => {
  assert.equal(classifyCommit(commit('docs(combat): Explain damage')).secondary, true);
  assert.equal(classifyCommit(commit('test(ui): Cover menus')).section, 'Tests');
  assert.equal(classifyCommit(commit('chore: Update editor config')).secondary, true);
  assert.equal(classifyCommit(commit('Add documentation for combat')).section, 'Documentation');
});

test('raw detail preserves equivalent commits, exact originals, body fences and paths', () => {
  const first = commit('feat(ui): Add <panel>', 1, { body: 'Original body\n```\n<script>unsafe</script>\n', files: ['Assets/UI/a[b].cs'] });
  const second = { ...first, sha: sha(2) };
  const history = versions([first, second]);
  history[0].entries = [{ ...first, refs: [first.sha, second.sha] }];
  const artifacts = developerArtifacts(history);
  const details = artifacts.filter(file => /^docs\/changelog\/.+\.md$/.test(file.name) && !file.name.endsWith('/README.md')).map(file => file.text).join('\n');
  assert.match(details, new RegExp(`/commit/${sha(1)}`));
  assert.match(details, new RegExp(`/commit/${sha(2)}`));
  assert.ok(details.includes(first.subject));
  assert.ok(details.includes(first.body.trimEnd()));
  assert.ok(details.includes('````text\nOriginal body'));
  assert.ok(details.includes('Assets/UI/a[b].cs'));
  const metrics = report(artifacts);
  assert.equal(metrics.rawCommits, 2);
  assert.equal(metrics.canonicalEntries, 1);
  assert.equal(metrics.commits.length, 2);
});

test('changelog automation cannot change any generated artifact', () => {
  const initial = commit('Added inventory sorting');
  const baseline = developerArtifacts(versions([initial]));
  const withBot = developerArtifacts(versions([initial, commit('chore(changelog): Refresh history', 2)]));
  assert.deepEqual(withBot, baseline);
});

test('initial history uses deterministic UTC months and preserves release identity', () => {
  const artifacts = developerArtifacts(versions([
    commit('Added inventory sorting', 1, { date: '2026-01-31T23:30:00-03:00' }),
    commit('Fixed combat damage', 2, { date: '2025-12-10T10:00:00Z' }),
  ]));
  const index = artifacts.find(file => file.name === 'docs/changelog/README.md').text;
  assert.match(index, /2026-02/);
  assert.match(index, /2025-12/);
  assert.ok(index.indexOf('2026-02') < index.indexOf('2025-12'));
  assert.match(index, /v1\.0\.0/);
  assert.equal(report(artifacts).periods.length, 2);
});

test('summary is bounded and meaningful while archive index retains every release', () => {
  const history = versions([commit('Update', 1, { files: ['Assets/Scripts/Combat/Damage.cs'] })]);
  for (let index = 1; index <= 40; index += 1) {
    const commits = Array.from({ length: 20 }, (_, offset) => commit(`feat(ui): Add panel ${index}-${offset}`, index * 100 + offset));
    history.push(...developerVersions([{ version: `1.${index}.0`, tag: `v1.${index}.0`, commits, date: '2026-03-01', commit: sha(index) }]));
  }
  const artifacts = developerArtifacts(history);
  const main = artifacts.find(file => file.name === 'CHANGELOG_DEV.md').text;
  const index = artifacts.find(file => file.name === 'docs/changelog/README.md').text;
  assert.ok(main.split('\n').length < 150);
  assert.doesNotMatch(main, /- Update\b/);
  assert.doesNotMatch(main, /\| System \| Features \| Fixes \| Refactors/);
  assert.match(index, /v1\.1\.0/);
  assert.match(index, /v1\.40\.0/);
  assert.match(main, /Full archive/);
});

test('report exposes auditable before/after counts, overlapping systems and unknown evidence', () => {
  const artifacts = developerArtifacts(versions([
    commit('Fixed inventory sorting'),
    commit('Update', 2, { files: ['Assets/Scripts/Combat/Damage.cs', 'Assets/Scripts/UI/Hud.cs'] }),
  ]));
  const metrics = report(artifacts);
  assert.equal(metrics.before['Other Changes'], 2);
  assert.equal(metrics.after['Other Changes'], 1);
  assert.equal(metrics.after['Bug Fixes'], 1);
  assert.equal(metrics.systemCountsOverlap, true);
  assert.equal(metrics.unknownEntries.length, 1);
  assert.deepEqual(metrics.unknownEntries[0].files, ['Assets/Scripts/Combat/Damage.cs', 'Assets/Scripts/UI/Hud.cs']);
  assert.equal(metrics.examples[0].subject, 'Fixed inventory sorting');
});

test('rendering is deterministic, pure and honors configured relative paths', () => {
  const history = versions([commit('Added inventory sorting')]);
  const snapshot = JSON.stringify(history);
  const settings = { repoUrl: 'https://example.test/repo', developmentBranch: 'dev', devFile: 'notes/DEV.md', historyDirectory: 'notes/history' };
  const first = developerArtifacts(history, settings);
  assert.deepEqual(first, developerArtifacts(history, settings));
  assert.equal(JSON.stringify(history), snapshot);
  assert.equal(renderDev(history, settings), first.find(file => file.name === 'notes/DEV.md').text);
  assert.match(first[0].text, /\[Full archive\]\(history\/README.md\)/);
  assert.ok(first.every(file => file.type === 'dev' && file.text.endsWith('\n')));
});

test('legacy typed subjects and document-only evidence do not remain Other Changes', () => {
  assert.equal(classifyCommit(commit('Refactor: Decouple prepared equipment')).section, 'Refactoring');
  assert.equal(classifyCommit(commit('Fix: Range enemy can attack')).section, 'Bug Fixes');
  assert.equal(classifyCommit(commit('Document ability targeting contract')).section, 'Documentation');
  assert.equal(classifyCommit(commit('Documentation Update',1,{files:['Docs/Architecture/Combat.md']})).section, 'Documentation');
  assert.equal(classifyCommit(commit('Adjustments',2,{files:['Assets/Scripts/Combat/Attack.cs']})).section, 'Other Changes');
});
test('ability and extraction systems are independent of the change kind', () => {
  const r=classifyCommit(commit('adjustments',1,{files:['Assets/Scripts/Abilities/SpellCast.cs','backend/src/services/ExtractionCommitService.js']}));
  assert.equal(r.section,'Other Changes'); assert.ok(r.systems.includes('Abilities')); assert.ok(r.systems.includes('Extraction'));
});

test('negated or deferred system mentions are not positive affected-system evidence', () => {
  const negative=classifyCommit(commit('docs: clarify contributor responsibilities',1,{body:'No combat, inventory, or networking changes.'}));
  assert.ok(!negative.systems.includes('Combat')); assert.ok(!negative.systems.includes('Inventory & Loot')); assert.ok(!negative.systems.includes('Networking & Sessions'));
  const positive=classifyCommit(commit('fix: repair networking',2,{body:'No combat changes. Repair lobby reconnects.'}));
  assert.ok(positive.systems.includes('Networking & Sessions')); assert.ok(!positive.systems.includes('Combat'));
  const paths=classifyCommit(commit('Update',3,{body:'No new combat mechanics.',files:['Assets/Scripts/Combat/Damage.cs']}));
  assert.ok(paths.systems.includes('Combat'));
});

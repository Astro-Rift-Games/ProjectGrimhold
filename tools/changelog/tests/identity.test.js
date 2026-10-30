import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as commitsApi from '../src/commits.js';
import * as playersApi from '../src/players.js';

const sha = letter => letter.repeat(40);
function commit(letter, subject = 'fix: Repair', body = '', extra = {}) {
  return commitsApi.parseCommit({ sha: sha(letter), subject, body, ...extra });
}
function release(version, commits) {
  return { version, tag: `v${version}`, date: '2026-01-01', commit: sha('f'), commits };
}
function players(history, overrides = new Map()) {
  return playersApi.playerVersions(history, overrides);
}
const note = 'Player-Fixed: Doors open correctly';

test('different commits with the same generic subject remain distinct', () => {
  const first = commit('a');
  const second = commit('b');
  assert.notEqual(first.identity, second.identity);
  assert.equal(commitsApi.developerVersions([release('1.0.0', [second, first])])[0].entries.length, 2);
});

test('attribution differences alone do not prove commit equivalence', () => {
  const first = commit('a', 'fix: Repair', 'Details\n\nCo-authored-by: A <a@b.c>');
  const second = commit('b', 'fix: Repair', 'Details\n\nSigned-off-by: A <a@b.c>');
  assert.equal(commitsApi.developerVersions([release('1.0.0', [second, first])])[0].entries.length, 2);
});

test('repeated SHA deduplicates while full cherry-pick origins merge refs', () => {
  const first = commit('a');
  const second = commit('b', 'fix: Repair', `(cherry picked from commit ${first.sha})`);
  const result = commitsApi.developerVersions([release('1.0.0', [second, first, second])]);
  assert.equal(result[0].entries.length, 1);
  assert.deepEqual(result[0].entries[0].refs, [second.sha, first.sha]);
});

test('cross-release cherry-pick refs stay attached to the canonical entry', () => {
  const first = commit('a');
  const second = commit('b', 'fix: Repair', `(cherry picked from commit ${first.sha})`);
  const history = [release('1.0.0', [first]), release('1.1.0', [second])];
  const result = commitsApi.developerVersions(history);
  assert.equal(result[1].entries.length, 0);
  assert.deepEqual(result[0].entries[0].refs, [first.sha, second.sha]);
  assert.equal(result[1].commits[0], second);
  assert.equal(result[0].commits, history[0].commits);
});

test('full shared origins prove equivalence even when source is outside the input', () => {
  const body = `(cherry picked from commit ${sha('f')})`;
  const result = commitsApi.developerVersions([release('1.0.0', [commit('a', 'fix: First', body), commit('b', 'fix: Second', body)])]);
  assert.equal(result[0].entries.length, 1);
});

test('abbreviated origins never merge ambiguous or absent full identities', () => {
  const body = '(cherry picked from commit abcdef0)';
  const result = commitsApi.developerVersions([release('1.0.0', [commit('a', 'fix: Repair', body), commit('b', 'fix: Repair', body)])]);
  assert.equal(result[0].entries.length, 2);
});

test('matching stable Git patch IDs prove equivalence across changed messages', () => {
  const first = commit('a', 'fix: First', '', { patchId: sha('f') });
  const second = commit('b', 'fix: Second', '', { patchId: sha('f') });
  assert.equal(commitsApi.developerVersions([release('1.0.0', [second, first])])[0].entries.length, 1);
});

test('empty or malformed patch IDs cannot collapse unrelated commits', () => {
  for (const patchId of ['', ' ', 'not-a-git-patch-id']) {
    const history = [release('1.0.0', [commit('a', 'fix: Repair', '', { patchId }), commit('b', 'fix: Repair', '', { patchId })])];
    assert.equal(commitsApi.developerVersions(history)[0].entries.length, 2);
  }
});

test('cherry-pick chains merge transitively without mutation', () => {
  const first = commit('a');
  const second = commit('b', 'fix: Repair', `(cherry picked from commit ${first.sha})`);
  const third = commit('c', 'fix: Repair', `(cherry picked from commit ${second.sha})`);
  const snapshot = JSON.stringify([first, second, third]);
  const result = commitsApi.developerVersions([release('1.0.0', [third, second, first])]);
  assert.deepEqual(result[0].entries[0].refs, [third.sha, second.sha, first.sha]);
  assert.equal(JSON.stringify([first, second, third]), snapshot);
});

test('distinct changes with identical public notes survive across releases', () => {
  const result = players([release('1.0.0', [commit('a', 'fix: Repair', note)]), release('1.1.0', [commit('b', 'fix: Repair', note)])]);
  assert.deepEqual(result.versions.map(version => version.notes.length), [1, 1]);
});

test('proven equivalent public commits deduplicate across releases', () => {
  const first = commit('a', 'fix: Repair', note);
  const second = commit('b', 'fix: Repair', `${note}\n\n(cherry picked from commit ${first.sha})`);
  const result = players([release('1.0.0', [first]), release('1.1.0', [second])]);
  assert.deepEqual(result.versions.map(version => version.notes.length), [1, 0]);
});

test('cherry-pick provenance does not invalidate an explicit public footer', () => {
  const parsed = commit('b', 'fix: Repair', `${note}\n\n(cherry picked from commit ${sha('a')})`);
  assert.equal(players([release('1.0.0', [parsed])]).versions[0].notes[0]?.text, 'Doors open correctly');
});

test('local public text dedup preserves the first chronological note', () => {
  const first = commit('a', 'fix: First', note);
  const second = commit('b', 'fix: Second', 'Player-Fixed: DOORS   open correctly');
  const notes = players([release('1.0.0', [second, first])]).versions[0].notes;
  assert.equal(notes.length, 1);
  assert.equal(notes[0].sha, first.sha);
});

test('same-release reverts suppress original notes and retain explicit revert notes', () => {
  const first = commit('a', 'feat: Doors', note);
  const revert = commit('b', 'revert: Doors', `This reverts commit ${first.sha}.\n\nPlayer-Fixed: Removed broken doors`);
  const notes = players([release('1.0.0', [revert, first])]).versions[0].notes;
  assert.deepEqual(notes.map(entry => entry.text), ['Removed broken doors']);
});

test('revert of revert restores original notes and suppresses reverted revert notes', () => {
  const first = commit('a', 'feat: Doors', note);
  const revert = commit('b', 'revert: Doors', `This reverts commit ${first.sha}.\n\nPlayer-Fixed: Removed doors`);
  const restore = commit('c', 'revert: Restore doors', `This reverts commit ${revert.sha}.`);
  assert.deepEqual(players([release('1.0.0', [restore, revert, first])]).versions[0].notes.map(entry => entry.text), ['Doors open correctly']);
});

test('revert targets resolve full cherry-pick aliases outside the release range', () => {
  const picked = commit('b', 'feat: Doors', `${note}\n\n(cherry picked from commit ${sha('a')})`);
  const revert = commit('c', 'revert: Doors', `This reverts commit ${sha('a')}.`);
  assert.equal(players([release('1.0.0', [revert, picked])]).versions[0].notes.length, 0);
});

test('revert of a picked revert restores the original equivalent change', () => {
  const first = commit('a', 'feat: Doors', note, { patchId: sha('e') });
  const revert = commit('b', 'revert: Doors', `This reverts commit ${first.sha}.`);
  const pickedRevert = commit('c', 'revert: Doors', `This reverts commit ${first.sha}.\n\n(cherry picked from commit ${revert.sha})`);
  const restore = commit('d', 'revert: Restore doors', `This reverts commit ${revert.sha}.`, { patchId: sha('e') });
  const result = players([release('1.0.0', [restore, pickedRevert, first])]);
  assert.equal(result.versions[0].notes.length, 1);
});

test('activeCommits shares alias-aware revert semantics without erasing historical releases', () => {
  const first = commit('a', 'feat: Doors', note);
  const revert = commit('b', 'revert: Doors', `This reverts commit ${first.sha}.`);
  const restore = commit('c', 'revert: Restore doors', `This reverts commit ${revert.sha}.`);
  assert.deepEqual(commitsApi.activeCommits([restore, revert, first]).map(entry => entry.sha), [restore.sha, first.sha]);
  const result = players([release('1.0.0', [first]), release('1.1.0', [revert])]);
  assert.equal(result.versions[0].notes.length, 1);
});

test('equivalent public commits can contribute distinct explicit notes', () => {
  const first = commit('a', 'fix: Doors', note);
  const picked = commit('b', 'fix: Doors', `Player-Improved: Easier door controls\n\n(cherry picked from commit ${first.sha})`);
  const result = players([release('1.0.0', [first]), release('1.1.0', [picked])]);
  assert.deepEqual(result.versions.map(version => version.notes.length), [1, 1]);
});

test('technical changes never become automatic public notes', () => {
  const result = players([release('1.0.0', [commit('a', 'feat: Internal tooling')])]);
  assert.equal(result.versions[0].notes.length, 0);
});

test('malformed public trailers produce actionable validation diagnostics', () => {
  for (const body of ['Player-Fixed:No space', 'Player-Fixed : Door', 'Player-Fixed', 'Player-Unknown: Door']) {
    const result = players([release('1.0.0', [commit('a', 'fix: Door', body)])]);
    const diagnostics = playersApi.validatePublicNotes(result.versions, result.warnings);
    assert.ok(diagnostics.some(item => item.sha === sha('a') && item.severity === 'error'), body);
  }
});

test('published releases without public notes diagnose without changing the JSON contract', () => {
  const result = players([release('1.0.0', []), { version: 'Unreleased', commits: [] }]);
  const diagnostics = playersApi.validatePublicNotes(result.versions, result.warnings);
  assert.equal(diagnostics.length, 1);
  assert.equal(diagnostics[0].code, 'empty-player-release');
  assert.equal(diagnostics[0].version, '1.0.0');
  assert.equal(diagnostics[0].severity, 'warning');
});

test('explicit overrides retain public categories, suppression and malformed-note replacement', () => {
  const first = commit('a', 'feat: Internal tooling', 'Player-Fixed');
  const overrides = playersApi.validateOverrides({ schemaVersion: 1, overrides: [{ commit: first.sha, entries: [{ category: 'added', text: '  New   doors ' }] }] }, [first]);
  const result = players([release('1.0.0', [first])], overrides);
  assert.equal(result.versions[0].notes[0].text, 'New doors');
  assert.deepEqual(result.warnings, []);
  assert.equal(players([release('1.0.0', [first])], new Map([[first.sha, []]])).versions[0].notes.length, 0);
});

function restoredHistory() {
  const first = commit('a', 'feat: Doors', note, { patchId: sha('e') });
  const revert = commit('b', 'revert: Doors', `This reverts commit ${first.sha}.`);
  const reapplied = commit('c', 'feat: Doors', `${note}\n\n(cherry picked from commit ${first.sha})`, { patchId: sha('e') });
  return { first, revert, reapplied };
}

test('reapplied equivalent changes regain developer and public entries after a published revert', () => {
  const { first, revert, reapplied } = restoredHistory();
  const history = [release('1.0.0', [first]), release('1.1.0', [revert]), release('1.2.0', [reapplied])];
  assert.deepEqual(commitsApi.developerVersions(history).map(version => version.entries.length), [1, 1, 1]);
  assert.deepEqual(players(history).versions.map(version => version.notes.length), [1, 0, 1]);
});

test('same-release reapply survives original revert and preserves its canonical refs separately', () => {
  const { first, revert, reapplied } = restoredHistory();
  const history = [release('1.0.0', [reapplied, revert, first])];
  const entries = commitsApi.developerVersions(history)[0].entries;
  assert.equal(entries.length, 3);
  assert.deepEqual(entries.map(entry => entry.refs), [[reapplied.sha], [revert.sha], [first.sha]]);
  const notes = players(history).versions[0].notes;
  assert.equal(notes.length, 1);
  assert.equal(notes[0].sha, reapplied.sha);
});

test('reverting a reapplication leaves the equivalent original effect inactive', () => {
  const { first, revert, reapplied } = restoredHistory();
  const removed = commit('d', 'revert: Doors again', `This reverts commit ${reapplied.sha}.`);
  const history = [release('1.0.0', [removed, reapplied, revert, first])];
  assert.equal(players(history).versions[0].notes.length, 0);
  assert.ok(!commitsApi.activeCommits(history[0].commits).some(entry => entry.type === 'feat'));
});

test('action identity epochs preserve still-active equivalence and reopen only restored effects', () => {
  const { first, revert, reapplied } = restoredHistory();
  const duplicate = commit('d', 'feat: Doors', `${note}\n\n(cherry picked from commit ${first.sha})`);
  const history = [release('1.0.0', [duplicate, first]), release('1.1.0', [revert]), release('1.2.0', [reapplied])];
  const identities = commitsApi.commitActionIdentities(history);
  assert.equal(identities.get(first.sha), identities.get(duplicate.sha));
  assert.notEqual(identities.get(first.sha), identities.get(reapplied.sha));
});

test('revert-of-revert changes the epoch for later equivalent player notes', () => {
  const { first, revert, reapplied } = restoredHistory();
  const restored = commit('d', 'revert: Restore doors', `This reverts commit ${revert.sha}.`);
  const history = [release('1.0.0', [first]), release('1.1.0', [revert]), release('1.2.0', [reapplied, restored])];
  assert.deepEqual(players(history).versions.map(version => version.notes.length), [1, 0, 1]);
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseCommit, developerVersions } from '../src/commits.js';
import { playerVersions, validateOverrides } from '../src/players.js';
import { renderDev } from '../src/render-dev.js';
import {
  playersDocument,
  renderPlayers,
  renderReleaseNotes,
  validatePlayersDocument,
} from '../src/render-players.js';

const firstSha = 'a'.repeat(40);
const secondSha = 'b'.repeat(40);

function commit(subject, body = '', sha = firstSha) {
  return parseCommit({
    sha,
    subject,
    body,
    date: '2026-01-01T00:00:00Z',
  });
}

function versions(commits, pendingCommits = []) {
  return [
    {
      version: '1.0.0',
      tag: 'v1.0.0',
      date: '2026-01-01',
      commit: firstSha,
      commits,
    },
    { version: 'Unreleased', commits: pendingCommits },
  ];
}

function playerResult(commits, overrides = new Map()) {
  return playerVersions(versions(commits), overrides);
}

function overrideDocument(entries, prefix = firstSha) {
  return {
    schemaVersion: 1,
    overrides: [{ commit: prefix, entries }],
  };
}

function documentWithNotes() {
  const commits = [commit('feat: adventure', 'Player-Added: New adventure')];
  return playersDocument(playerResult(commits).versions);
}

test('conventional header parses type and scope', () => {
  const parsed = commit('feat(ui): New panel');

  assert.equal(parsed.type, 'feat');
  assert.equal(parsed.scope, 'ui');
  assert.equal(parsed.description, 'New panel');
});

test('bang marks a conventional commit as breaking', () => {
  assert.equal(commit('feat!: New interface').breaking, true);
});

test('legacy subject is preserved as its description', () => {
  assert.equal(commit('Old subject').description, 'Old subject');
});

test('commit with empty body has no trailers', () => {
  const parsed = commit('fix: Subject only');

  assert.deepEqual(parsed.trailers, []);
  assert.equal(parsed.body, '');
  assert.equal(parsed.breaking, false);
});

test('subject followed by trailers-only body parses player notes', () => {
  const parsed = commit('fix: Repair', 'Player-Fixed: Repaired door');

  assert.deepEqual(parsed.trailers, [{ key: 'Player-Fixed', value: 'Repaired door' }]);
  assert.equal(playerResult([parsed]).versions[0].notes[0].text, 'Repaired door');
});

test('footer continuation unfolds with a single space', () => {
  const parsed = commit('fix: Repair', 'Details\n\nPlayer-Fixed: Repaired\n  the door');

  assert.equal(parsed.trailers[0].value, 'Repaired the door');
});

test('CRLF body is normalized before footer parsing', () => {
  const parsed = commit('fix: Repair', 'Details\r\n\r\nPlayer-Fixed: Door\r\n repaired');

  assert.equal(parsed.trailers[0].value, 'Door repaired');
  assert.doesNotMatch(parsed.body, /\r/);
});

test('non-trailer line in last paragraph invalidates the entire footer', () => {
  const parsed = commit('fix: Repair', 'Player-Fixed: Door\nNot a trailer');

  assert.deepEqual(parsed.trailers, []);
});

test('only the last paragraph is considered a footer', () => {
  const parsed = commit('fix: Repair', 'Player-Fixed: Door\n\nFinal prose');

  assert.deepEqual(parsed.trailers, []);
});

test('BREAKING CHANGE footer marks a commit as breaking', () => {
  assert.equal(commit('feat: Interface', 'BREAKING CHANGE: New protocol').breaking, true);
});

test('BREAKING-CHANGE footer marks a commit as breaking', () => {
  assert.equal(commit('feat: Interface', 'BREAKING-CHANGE: New protocol').breaking, true);
});

test('conventional types map to the fixed developer sections', () => {
  const expected = {
    feat: 'Features',
    fix: 'Bug Fixes',
    perf: 'Performance',
    refactor: 'Refactoring',
    test: 'Tests',
    docs: 'Documentation',
    build: 'Architecture & Infrastructure',
    ci: 'Architecture & Infrastructure',
    chore: 'Architecture & Infrastructure',
    revert: 'Reverts',
  };

  for (const [type, section] of Object.entries(expected)) {
    assert.equal(commit(`${type}: Change`).section, section);
  }
});

test('unknown conventional types retain full subjects in Other Changes', () => {
  const parsed = commit('style(ui): Align panel');

  assert.equal(parsed.section, 'Other Changes');
  assert.equal(parsed.description, 'style(ui): Align panel');
});

test('chore(changelog) commits are excluded from developer and player output', () => {
  const parsed = commit('chore(changelog): Update', 'Player-Added: Automation');

  assert.equal(developerVersions(versions([parsed]))[0].entries.length, 0);
  assert.equal(playerResult([parsed]).versions[0].notes.length, 0);
});

test('Merge subjects are defensively excluded', () => {
  const parsed = commit('Merge branch feature');

  assert.equal(parsed.excluded, true);
  assert.equal(developerVersions(versions([parsed]))[0].entries.length, 0);
});

test('cherry-pick duplicates merge refs newest first without repeated SHAs', () => {
  const original = commit('fix: Door', 'Details');
  const picked = commit(
    'fix: Door',
    `Details\n(cherry picked from commit ${firstSha})`,
    secondSha,
  );
  const result = developerVersions(versions([picked, original, picked]));

  assert.deepEqual(result[0].entries[0].refs, [secondSha, firstSha]);
  assert.equal(result[0].entries.length, 1);
});

test('developer identity normalizes case and internal whitespace', () => {
  const original = commit('fix: Door', 'Some   details  ');
  const duplicate = commit('fix: door', 'some details');

  assert.equal(original.identity, duplicate.identity);
});

test('developer duplicates already emitted in a release are omitted later', () => {
  const parsed = commit('fix: Door');
  const result = developerVersions(versions([parsed], [parsed]));

  assert.equal(result[1].entries.length, 0);
});

test('developer entries include scope, breaking marker and seven-character refs', () => {
  const parsed = commit('feat(ui)!: New   panel');
  const markdown = renderDev(developerVersions(versions([parsed])));

  assert.match(markdown, /- \*\*BREAKING\*\* \*\*ui:\*\* New panel \(\[aaaaaaa\]/);
  assert.match(markdown, new RegExp(`/commit/${firstSha}`));
});

test('developer sections are rendered in fixed order and empty sections omitted', () => {
  const commits = [commit('fix: Door'), commit('feat: Panel', '', secondSha)];
  const markdown = renderDev(developerVersions(versions(commits)));

  assert.ok(markdown.indexOf('### Features') < markdown.indexOf('### Bug Fixes'));
  assert.doesNotMatch(markdown, /### Performance/);
});

test('developer changelog includes pending commits and compare link', () => {
  const pending = commit('fix: Pending door');
  const markdown = renderDev(developerVersions(versions([], [pending])));

  assert.match(markdown, /Pending door/);
  assert.match(markdown, /compare\/v1\.0\.0\.\.\.New-Testing/);
});

test('developer changelog without tags links to branch history', () => {
  const markdown = renderDev(developerVersions([{ version: 'Unreleased', commits: [] }]));

  assert.match(markdown, /commits\/New-Testing/);
  assert.match(markdown, /_No unreleased changes\._/);
});

test('player trailers map to the four categories in note order', () => {
  const body = [
    'Player-Added: Adventure',
    'Player-Improved: Movement',
    'Player-Fixed: Door',
    'Player-Balanced: Sword',
  ].join('\n');
  const notes = playerResult([commit('feat: Update', body)]).versions[0].notes;

  assert.deepEqual(notes.map(note => note.category), ['added', 'improved', 'fixed', 'balanced']);
  assert.deepEqual(notes.map(note => note.id), [1, 2, 3, 4].map(index => `aaaaaaaaaaaa-${index}`));
});

test('feat commit without player trailer is excluded from player notes', () => {
  assert.equal(playerResult([commit('feat: Adventure')]).versions[0].notes.length, 0);
});

test('player text is unfolded, trimmed and whitespace collapsed', () => {
  const parsed = commit('fix: Door', 'Player-Fixed:   Door   now\n  opens  ');

  assert.equal(playerResult([parsed]).versions[0].notes[0].text, 'Door now opens');
});

test('unknown player trailer warns and drops the note', () => {
  const result = playerResult([commit('fix: Door', 'Player-Fix: Door')]);

  assert.equal(result.versions[0].notes.length, 0);
  assert.match(result.warnings[0].message, /Unknown player trailer/);
  assert.equal(result.warnings[0].sha, firstSha);
});

test('wrong-case player trailer warns and drops the note', () => {
  const result = playerResult([commit('fix: Door', 'player-fIxEd: Door')]);

  assert.equal(result.versions[0].notes.length, 0);
  assert.match(result.warnings[0].message, /Wrong case/);
});

test('misplaced player trailer warns', () => {
  const result = playerResult([commit('fix: Door', 'Player-Fixed: Door\n\nProse')]);

  assert.match(result.warnings[0].message, /Misplaced/);
});

test('empty player text warns and is dropped', () => {
  const result = playerResult([commit('fix: Door', 'Player-Fixed: ')]);

  assert.equal(result.versions[0].notes.length, 0);
  assert.match(result.warnings[0].message, /1–240/);
});

test('player text longer than 240 characters warns and is dropped', () => {
  const result = playerResult([commit('fix: Door', `Player-Fixed: ${'x'.repeat(241)}`)]);

  assert.equal(result.versions[0].notes.length, 0);
  assert.match(result.warnings[0].message, /1–240/);
});

test('player trailer on automation commit warns', () => {
  const result = playerResult([commit('chore(changelog): Update', 'Player-Fixed: Door')]);

  assert.match(result.warnings[0].message, /automation/);
});

test('override replaces all trailer notes', () => {
  const parsed = commit('fix: Door', 'Player-Fixed: Original');
  const overrides = validateOverrides(
    overrideDocument([{ category: 'added', text: 'Replacement' }]),
    [parsed],
  );
  const notes = playerResult([parsed], overrides).versions[0].notes;

  assert.deepEqual(notes.map(note => note.text), ['Replacement']);
  assert.equal(notes[0].id, 'aaaaaaaaaaaa-1');
});

test('empty override hides a commit', () => {
  const parsed = commit('fix: Door', 'Player-Fixed: Original');
  const overrides = validateOverrides(overrideDocument([]), [parsed]);

  assert.equal(playerResult([parsed], overrides).versions[0].notes.length, 0);
});

test('override adds notes to commit without trailers using unique prefix', () => {
  const parsed = commit('feat: Adventure');
  const overrides = validateOverrides(
    overrideDocument([{ category: 'added', text: 'Adventure' }], firstSha.slice(0, 7)),
    [parsed],
  );

  assert.equal(playerResult([parsed], overrides).versions[0].notes[0].text, 'Adventure');
});

test('override suppresses all player warnings including misplaced trailers', () => {
  const body = 'Player-Fixed: Misplaced\n\nProse\n\nPlayer-fIxEd: Wrong\nPlayer-Note: Unknown';
  const parsed = commit('chore(changelog): Update', body);
  const overrides = validateOverrides(overrideDocument([]), [parsed]);

  assert.deepEqual(playerResult([parsed], overrides).warnings, []);
});

test('unsupported override schema is rejected', () => {
  assert.throws(() => validateOverrides({ schemaVersion: 2, overrides: [] }, []));
});

test('unknown override category is rejected', () => {
  const document = overrideDocument([{ category: 'unknown', text: 'Door' }]);

  assert.throws(() => validateOverrides(document, [commit('fix: Door')]));
});

test('empty override text is rejected', () => {
  const document = overrideDocument([{ category: 'fixed', text: ' ' }]);

  assert.throws(() => validateOverrides(document, [commit('fix: Door')]));
});

test('overlong override text is rejected', () => {
  const document = overrideDocument([{ category: 'fixed', text: 'x'.repeat(241) }]);

  assert.throws(() => validateOverrides(document, [commit('fix: Door')]));
});

test('unmatched override prefix is rejected', () => {
  assert.throws(() => validateOverrides(overrideDocument([], 'ccccccc'), [commit('fix: Door')]));
});

test('ambiguous override prefix is rejected', () => {
  const first = commit('fix: Door', '', `abcdef0${'1'.repeat(33)}`);
  const second = commit('fix: Panel', '', `abcdef0${'2'.repeat(33)}`);

  assert.throws(() => validateOverrides(overrideDocument([], 'abcdef0'), [first, second]));
});

test('override prefix shorter than seven characters is rejected', () => {
  assert.throws(() => validateOverrides(overrideDocument([], 'aaaaaa'), [commit('fix: Door')]));
});

test('duplicate overrides resolving to the same commit are rejected', () => {
  const document = overrideDocument([]);
  document.overrides.push({ commit: firstSha.slice(0, 7), entries: [] });

  assert.throws(() => validateOverrides(document, [commit('fix: Door')]));
});

test('revert in the same range drops the original player notes', () => {
  const original = commit('feat: Adventure', 'Player-Added: Adventure');
  const revert = commit('revert: Adventure', `This reverts commit ${firstSha}.`, secondSha);

  assert.equal(playerResult([revert, original]).versions[0].notes.length, 0);
});

test('revert in another range does not erase previously published notes', () => {
  const original = commit('feat: Adventure', 'Player-Added: Adventure');
  const revert = commit('revert: Adventure', `This reverts commit ${firstSha}.`, secondSha);
  const result = playerVersions(versions([original], [revert]), new Map());

  assert.equal(result.versions[0].notes.length, 1);
});

test('player notes deduplicate category and normalized text within a version', () => {
  const first = commit('fix: Door', 'Player-Fixed: Door opens');
  const second = commit('fix: Another door', 'Player-Fixed: DOOR   opens', secondSha);
  const notes = playerResult([second, first]).versions[0].notes;

  assert.equal(notes.length, 1);
  assert.equal(notes[0].sha, firstSha);
});

test('player notes already published in an earlier version are skipped', () => {
  const first = commit('fix: Door', 'Player-Fixed: Door opens');
  const second = commit('fix: Another door', 'Player-Fixed: DOOR opens', secondSha);
  const result = playerVersions(versions([first], [second]), new Map());

  assert.equal(result.versions[1].notes.length, 0);
});

test('identical player text in different categories is retained', () => {
  const parsed = commit('feat: Door', 'Player-Added: Door\nPlayer-Fixed: Door');

  assert.equal(playerResult([parsed]).versions[0].notes.length, 2);
});

test('player notes follow reverse topo-order rather than author date', () => {
  const older = commit('feat: First', 'Player-Added: First');
  older.date = '2026-02-01T00:00:00Z';
  const newer = commit('feat: Second', 'Player-Added: Second', secondSha);
  newer.date = '2026-01-01T00:00:00Z';
  const notes = playerResult([newer, older]).versions[0].notes;

  assert.deepEqual(notes.map(note => note.text), ['First', 'Second']);
});

test('notes within one commit preserve trailer order', () => {
  const parsed = commit('feat: Adventure', 'Player-Added: First\nPlayer-Added: Second');
  const notes = playerResult([parsed]).versions[0].notes;

  assert.deepEqual(notes.map(note => note.text), ['First', 'Second']);
});

test('player outputs exclude Unreleased notes', () => {
  const pending = commit('feat: Adventure', 'Player-Added: Pending adventure');
  const result = playerVersions(versions([], [pending]), new Map());
  const document = playersDocument(result.versions);

  assert.equal(document.releases.length, 1);
  assert.doesNotMatch(JSON.stringify(document), /Pending adventure/);
  assert.doesNotMatch(renderPlayers(document), /Pending adventure/);
});

test('player JSON round-trip passes schema validation', () => {
  const roundTrip = JSON.parse(JSON.stringify(documentWithNotes()));

  assert.deepEqual(validatePlayersDocument(roundTrip), []);
});

test('player JSON always includes all four categories', () => {
  const document = documentWithNotes();

  assert.deepEqual(
    document.releases[0].categories.map(category => category.id),
    ['added', 'improved', 'fixed', 'balanced'],
  );
  assert.deepEqual(document.releases[0].categories[1].entries, []);
});

test('player releases are rendered newest first', () => {
  const history = versions([]);
  history.splice(1, 0, { ...history[0], version: '1.1.0', tag: 'v1.1.0' });
  const document = playersDocument(playerVersions(history, new Map()).versions);

  assert.deepEqual(document.releases.map(release => release.version), ['1.1.0', '1.0.0']);
});

test('empty player release remains listed with explanatory markdown', () => {
  const document = playersDocument(playerResult([]).versions);

  assert.equal(document.releases.length, 1);
  assert.match(renderPlayers(document), /_No player-facing changes in this release\._/);
  assert.doesNotMatch(renderPlayers(document), /### Added/);
});

test('player markdown with no releases explains that no releases exist', () => {
  const document = playersDocument([]);

  assert.match(renderPlayers(document), /_No releases yet\._/);
});

test('release notes include player categories and developer link', () => {
  const markdown = renderReleaseNotes(documentWithNotes().releases[0]);

  assert.match(markdown, /### Added\n\n- New adventure/);
  assert.match(markdown, /Developer changes: .*\/blob\/New-Testing\/CHANGELOG_DEV\.md\n$/);
});

test('schema validation rejects missing required document keys', () => {
  assert.ok(validatePlayersDocument({}).length > 0);
});

test('schema validation rejects incorrect value types', () => {
  const document = documentWithNotes();
  document.releases = 'not an array';

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('schema validation rejects incorrect category order', () => {
  const document = documentWithNotes();
  document.releases[0].categories.reverse();

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('schema validation rejects malformed semver', () => {
  const document = documentWithNotes();
  document.releases[0].version = 'one';

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('schema validation rejects malformed dates', () => {
  const document = documentWithNotes();
  document.releases[0].date = 'yesterday';

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('schema validation rejects malformed commit SHAs', () => {
  const document = documentWithNotes();
  document.releases[0].commit = 'abc';

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('schema validation rejects duplicate entry IDs', () => {
  const document = documentWithNotes();
  const entries = document.releases[0].categories[0].entries;
  entries.push({ ...entries[0] });

  assert.ok(validatePlayersDocument(document).length > 0);
});

test('rendering twice produces byte-identical developer and player output', () => {
  const history = versions([commit('feat(ui): Adventure', 'Player-Added: Adventure')]);
  const developers = developerVersions(history);
  const document = playersDocument(playerVersions(history, new Map()).versions);

  assert.equal(renderDev(developers), renderDev(developers));
  assert.equal(renderPlayers(document), renderPlayers(document));
  assert.equal(JSON.stringify(document), JSON.stringify(playersDocument(
    playerVersions(history, new Map()).versions,
  )));
  assert.match(renderDev(developers), /[^\n]\n$/);
  assert.doesNotMatch(renderDev(developers), /[ \t]+\n|\r/);
});

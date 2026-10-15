# Changelog generator

Builds the Project Grimhold changelogs from Git history. Zero dependencies, Node 20+.

| File | Audience | Content | Updated |
|---|---|---|---|
| `CHANGELOG_DEV.md` | Developers | Every commit by version, plus `Unreleased` | Every push to `New-Testing` |
| `CHANGELOG.md` | Players | Explicit player notes of published releases | When a release is published |
| `changelog-players.json` | Game / tools | Same as `CHANGELOG.md`, machine-readable | When a release is published |

All three files are generated. Do not edit them by hand; the next run overwrites them.

## Writing commits

Use Conventional Commits (`type(scope): summary`). The developer changelog groups commits by type:

| Type | Section |
|---|---|
| `feat` | Features |
| `fix` | Bug Fixes |
| `perf` | Performance |
| `refactor` | Refactoring |
| `test` | Tests |
| `docs` | Documentation |
| `build`, `ci`, `chore` | Architecture & Infrastructure |
| `revert` | Reverts |
| anything else, legacy subjects | Other Changes |

`type!:` or a `BREAKING CHANGE:` footer marks an entry as **BREAKING**. Merge commits and `chore(changelog)` commits (reserved for the bot) are skipped. Commits with the same message appear once with every commit link. This covers cherry-picks and rewrites that only add or drop `Co-authored-by`/`Signed-off-by`. A message already listed in an earlier version is not repeated in later ones.

## Adding player notes

A commit reaches the player changelog only when it carries an explicit note. The type alone never implies one. Add one trailer per player-visible change in the **last paragraph** of the body:

```text
fix(stash): enable Take All and Leave All button clicks

Enable both authored bulk-transfer buttons in the Stash prefab.

Player-Fixed: Take All and Leave All buttons in the Stash work again.
Refs: TASK-212
```

| Trailer | Use for |
|---|---|
| `Player-Added` | New features and content |
| `Player-Improved` | Better experience or gameplay |
| `Player-Fixed` | Bugs a player could notice |
| `Player-Balanced` | Balance adjustments |

Write one plain-English sentence of up to 240 characters that describes what players observe. Avoid implementation terms. Do not announce unfinished or hidden work. The `grimhold-commit-message` skill drafts these trailers.

Unknown or misspelled `Player-*` keys, empty or overlong notes, and `Player-*` lines outside the last paragraph produce warnings. Pull requests to `New-Testing` fail on these warnings.

## Reviewing and correcting notes

Pushed commit messages cannot change, so corrections live in `tools/changelog/player-overrides.json`. An override replaces every note of one commit. Empty `entries` hides the commit. An override on a commit without trailers adds notes to it.

```json
{
  "schemaVersion": 1,
  "overrides": [
    {
      "commit": "3764314b",
      "entries": [{ "category": "fixed", "text": "Stash bulk-transfer buttons respond to clicks again." }]
    },
    { "commit": "d3673b76", "entries": [] }
  ]
}
```

`commit` accepts a full SHA or a unique prefix of at least 7 characters. Categories are `added`, `improved`, `fixed` and `balanced`. An invalid file fails every command.

Notes of a commit that is reverted in the same release are dropped. A note with the same category and text appears only once.

## Previewing

Every push run shows the pending player notes in its job summary. Locally, from the repository root:

```bash
node tools/changelog/src/cli.js preview                    # pending notes since the last release
node tools/changelog/src/cli.js preview --range v0.1.0..HEAD --strict
node tools/changelog/src/cli.js check                      # exit 1 if generated files are stale
node tools/changelog/src/cli.js generate [--only dev|players|all]
node tools/changelog/src/cli.js release-notes --tag v0.1.0 [--strict]
```

Run the tests with `npm test` inside `tools/changelog`.

## Publishing a release

1. Make sure `New-Testing` contains everything for the release, then review the pending notes in the latest **Changelog** run summary or with `preview`.
2. Fix wording through `player-overrides.json` in a normal commit to `New-Testing`.
3. Create a GitHub Release with a new tag `vX.Y.Z` targeting `New-Testing`. Leave **Set as a pre-release** unchecked. The body can stay empty.
4. Publish it. The **Changelog release** workflow then:
   - checks the tag format, that the tag is on `New-Testing`, and that the notes of that release have no warnings;
   - regenerates all three files and commits them to `New-Testing` as `chore(changelog): publish changelogs for vX.Y.Z`;
   - replaces the release body with the player notes and attaches `changelog-players.json`.

If the workflow fails on warnings, add overrides on `New-Testing` and re-run the workflow. It is idempotent.

Only exact `vX.Y.Z` tags reachable from `New-Testing` count as versions. Commits after the last tag stay in `Unreleased` in the developer changelog and never appear in player files. The release date is the tag date in UTC.

## `changelog-players.json` schema

Version 1. Plain arrays of objects with no maps or nulls, so the file deserializes directly with Unity `JsonUtility` or any JSON library.

```json
{
  "schemaVersion": 1,
  "game": "Project Grimhold",
  "locale": "en",
  "releases": [
    {
      "version": "1.2.0",
      "tag": "v1.2.0",
      "date": "2026-10-20",
      "commit": "<full commit SHA>",
      "url": "https://github.com/Astro-Rift-Games/ProjectGrimhold/releases/tag/v1.2.0",
      "categories": [
        { "id": "added", "title": "Added", "entries": [{ "id": "3764314b1a2c-1", "text": "..." }] },
        { "id": "improved", "title": "Improved", "entries": [] },
        { "id": "fixed", "title": "Fixed", "entries": [] },
        { "id": "balanced", "title": "Balanced", "entries": [] }
      ]
    }
  ]
}
```

- `releases` are newest first. Each release always has the four categories in this order.
- Entry `id` is `<first 12 SHA characters>-<index>`. It is stable and can key future translations; `locale` says which language `text` uses.
- Breaking schema changes increment `schemaVersion`.

## Limitations

- The workflows push to `New-Testing` with `GITHUB_TOKEN`. If branch protection blocks that, allow GitHub Actions to push or switch the workflows to a token that can.
- Release workflows run from the workflow file in the tagged commit. Tags on commits older than this tool do not trigger publication.
- `workflow_dispatch` appears in the Actions UI only once the workflow exists on the default branch (`main`). Until then, run it with `gh workflow run changelog.yml --ref New-Testing`.

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

## Automatic version planning

The weekly publisher plans against the candidate integration of `New-Testing`
into `main`. Public tags belong to `main`; development still keeps `Unreleased`.
Version impact and public-note selection are independent: `feat` without a
`Player-*` trailer increments the version but does not invent a player note.

```bash
node tools/changelog/src/release-cli.js plan --ref HEAD
node tools/changelog/src/release-cli.js prepare --ref HEAD
```

Both commands accept `--root R` and print one JSON plan. `plan` is read-only;
`prepare` writes only the three generated root changelogs when a release is due.
Unknown, repeated or missing options fail with exit 2; validation failures use
exit 1. The existing `cli.js` commands and output remain unchanged.

| Effective change since the last public tag | Increment |
|---|---|
| Breaking marker (`!`, `BREAKING CHANGE`, `BREAKING-CHANGE`) | Minor during `0.x`, otherwise major |
| `feat` | Minor |
| `fix`, `perf` | Patch |
| Internal-only changes without a breaking marker | No release |

The highest increment wins. The existing developer parser supplies commit
types, breaking markers, identity deduplication and reserved `chore(changelog)`
exclusion. Functional impact of an otherwise internal type must use those
existing breaking markers, not an additional trailer convention. A feature
and its revert in the pending period cancel for version impact while remaining
in technical history. Reverting previously published functionality warrants a
patch. Published duplicate messages cannot increment the version again.

Only strict stable `vX.Y.Z` tags without leading zeroes define publication
boundaries. Reachable stable tags must occur on distinct commits in the
candidate's first-parent history, in increasing SemVer order. Ambiguous,
side-branch or nonmonotonic publication histories fail closed. Prerelease and
malformed version names do not define stable boundaries. A computed version
that already exists anywhere in the repository is refused, never reused or
moved. With no stable tags, the base is `0.0.0`: the first feature produces
`0.1.0`, the first fix `0.0.1`, and internal-only history produces no release.

### Publisher API and commit metadata

`createReleasePlan(root, ref = 'HEAD')` returns a JSON-safe object with
`previousTag`, `previousVersion`, `version`, `bump`, `sourceCommit` and
`releaseDate`. Missing previous values are `null`; `version` and `bump` are
`null` when no release is due. `sourceCommit` is the full candidate integration
SHA. `releaseDate` is its committer date in UTC, not the machine's current date,
so repeated planning against the same history is deterministic.

`writePlannedRelease(root, plan)` validates the plan again, validates overrides
against that history, rejects all player-note warnings and validates the player
JSON before writing. It returns the three output filenames. A no-release,
stale or modified plan is refused. The outputs retain prior release boundaries,
explicit trailers, overrides, commit references and an empty `Unreleased`.

The versioned files use the **content-origin integration SHA**, not the SHA of
the later metadata commit containing those very files; embedding that latter
SHA would be circular. The final tag points to the metadata commit. Generate
the GitHub Release asset with the existing generator **after tagging** so its
`commit` field identifies the actual tagged commit. Given a tag date matching
`releaseDate`, planned and post-tag outputs are identical apart from the new
release's commit references. Use that deterministic UTC date when creating an
annotated tag; lightweight tags derive their date from the tagged commit.

## Publishing a release

Public versions now originate only from `main`, through the single weekly orchestrator. Do not create releases targeting `New-Testing` or rely on a bot-created Release to trigger another workflow.

Start with [BOOTSTRAP.md](BOOTSTRAP.md). It defines disabled-by-default activation, mandatory licensed Unity validation, read-only manual dry-run, artifact lineage, publication recovery and development back-synchronization. The existing development workflow still maintains technical `Unreleased` history; it never decides a public version.
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

- GitHub branch protections and Actions token eligibility must be checked during bootstrap; user admin access is not proof.
- Scheduled and manual workflows must first be installed on the default branch (`main`).
- Full simulation requires a matching licensed Unity runner and actual MainMenu wiring. Missing proof blocks publication.
- No automatic game builds or executable distribution are implemented.
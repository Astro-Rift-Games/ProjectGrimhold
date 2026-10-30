# Changelog generator

Builds the Project Grimhold changelogs from Git history. Zero dependencies, Node 20+.

| File | Audience | Content | Updated |
|---|---|---|---|
| `CHANGELOG_DEV.md` | Developers | Bounded system/type overview and recent release/month highlights | Every push to `New-Testing` |
| `docs/changelog/` | Developers | Complete originals, evidence and classification diagnostics | Alongside the technical summary |
| `CHANGELOG.md` | Players | Explicit player notes of published releases | When a release is published |
| `changelog-players.json` | Game / tools | Same as `CHANGELOG.md`, machine-readable | When a release is published |

The root files and archive directory are generated. Do not edit them by hand.

## Writing commits

Use Conventional Commits (`type(scope): summary`). The developer summary combines systems, periods and these kinds:

| Type | Section |
|---|---|
| `feat` | Features |
| `fix` | Bug Fixes |
| `perf` | Performance |
| `refactor` | Refactoring |
| `test` | Tests |
| `docs` | Documentation |
| `build`, `ci` | Architecture & Infrastructure |
| `chore`, `style` | Maintenance |
| `revert` | Reverts |
| legacy subjects | Evidence-based inference, otherwise Other Changes |

`type!:` or a `BREAKING CHANGE:` footer marks an entry as **BREAKING**. Merge commits and `chore(changelog)` commits (reserved for the bot) are skipped as before. Every other original commit is archived. Canonical entries deduplicate only with SHA/cherry-pick/patch evidence, never message similarity alone. The identity policy below covers restored effects and repeated notes.

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

Notes of a commit reverted in the same release are dropped; a revert-of-revert restores the original effect. Equivalent/cherry-picked aliases share this treatment. Repeated category/text is consolidated within one release, never globally across unrelated changes in different releases. See identity policy below.

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

## Technical history and evidence

The generator reprocesses the entire reachable non-merge history on every run,
not only future Conventional Commits. The main document prioritizes recorded
advances by period and system: at most six periods and three selected advances
per period, with at most three related records per advance. Full metrics live in
the existing classification report and archives, not a primary summary matrix.
All older periods remain navigable through `docs/changelog/README.md`.
The first development history (also after its initial tag) is split by **UTC author
month**, not synthetic releases. Later stable tags retain their release boundaries.
A release may contain commits from several months; author dates never determine
revert ordering or publication order.

### How the main summary chooses advances

- Functional systems outrank routine tooling. Explicit change kind contributes:
  features 12, fixes 10, performance 9, refactoring 7, reverts 6. Recognized functional
  systems use relevance tier 300; unassigned concrete changes 200; supporting-only
  work and routine infrastructure/tooling 100. The historical classification is unchanged.
- Architectural infrastructure needs a structural subject (for example ownership,
  authority, composition, recovery, migration or persistence), not just a configure/
  prepare action. Anchored structural refactors such as decoupling simulation or
  transport can also use the functional tier with kind priority 12 and a 3-point
  architecture bonus. Breaking changes add 3. These are message-based selection
  signals, not proof of current behavior, deployment, validation or completion.
- Greedy selection adds 8 for a new displayed system, 3 for a new kind, and at most
  2 for related records. Original history position, then SHA, breaks ties; recency
  is not the sole selection rule. Scope evidence chooses the displayed system
  first, then the strongest distinct-path evidence among existing functional labels.
- Generic/unknown/secondary records and test/document-only paths stay in the full
  archive, even when a subject uses `feat`. Routine Editor/tooling-only paths do not
  outrank functional advances. Descriptions over 240 characters remain in the archive
  rather than being cut in a way that could remove a qualification.
- Display grouping requires an exact shared `TASK-<number>` in a subject or positive
  `Refs`/`Fixes`/`Closes` line, **or** the same non-empty scope, an actual shared
  implementation source file, and at least two meaningful shared subject terms.
  Deferred/negative task lines, same-system labels, docs/tests/meta paths or scope
  alone are insufficient. Every pair must have evidence; no transitive chaining.
- Grouping is presentation only, never deduplication. Each highlight is one brief
  evidence-backed synthesis, not a concatenation of descriptions or type labels.
  Every supporting canonical reference remains linked; originals and types stay in
  the unchanged historical records and classification report.
  No generated prose claims that the combined feature is complete or still active.

### Same-period outcome reconciliation

Highlights are conservative deterministic templates, not AI-generated prose. A
representative functional result may incorporate related fixes or refactoring;
parallel migrations can share one target description. This is a lossy reading aid,
not a replacement for the linked original records or proof of runtime acceptance.

Only the highlight projection reconciles lifecycle changes, using the original
newest-first Git topological order, never author dates. Initial tagged aggregation
recovers that order from its source version rather than monthly concatenation.
The existing `activeCommits` helper resolves exact revert chains and canonical
aliases read-only; neither canonical identity nor public-note behavior changes.

Explicit positive removal/replacement objects can supersede an earlier claim
when they identify the same concrete topic in a shared scope/source, or directly
name the affected implementation file's mechanism. Shared paths, task IDs or
systems alone are not cancellation evidence. Negated, deferred, retained and
context-only mentions are ignored. Partial removals do not automatically remove
the whole feature. Replacement chains select the final recorded approach;
revert-of-revert and later reintroduction retain their supporting history.

A canceled approach is summarized as reverted/removed/replaced, not as a current
addition. All supporting links remain, even when the contradiction spans more
than three records; the synthesis stays short. Reconciliation is period-local:
later periods do not rewrite earlier historical outcomes. No filename-difference
or source-code deletion inference is made from the existing path-only metadata;
ambiguous edits cannot establish lifecycle state. The historical archive/report,
raw subjects/bodies/types, classifiers, deduplication, SemVer and publisher remain
unchanged.

### Initial history and releases

Before the first stable tag there is no trustworthy post-release pending range.
The main summary explicitly distinguishes **Unreleased** (no release baseline yet)
from monthly **Initial development history** instead of labeling all months as one
pending release. Once tags exist, the main summary presents each release once by
version and the actual latest `Unreleased` range separately. Initial tagged history
may still span several monthly archive files; those files, archive headings, report
metadata, full records and classifications remain byte-for-byte unchanged by the
summary-only temporal projection. No release, SemVer or publication rule changes.

Classification is independent from public notes and SemVer:

- Explicit Conventional Commit types take precedence. Existing scopes remain valid.
- Historical anchored actions (add, implement, fix, refactor, document, configure,
  and supported Spanish equivalents) justify a kind only with a meaningful object.
- Documentation-only/test-only paths establish an artifact category before generic historical action inference, never new gameplay. Explicit Conventional Commit types retain precedence.
- Exact scope/path tokens and bounded message/body terms establish overlapping
  affected systems (including combat, abilities, extraction, inventory, networking,
  UI, tooling and testing). Negated, excluded and deferred prose is not positive evidence.
  These labels do not prove a new feature or a fixed behavior.
- Generic subjects and unknown kinds stay out of primary headlines. Documentation,
  tests, maintenance, explicitly minor adjustments and metadata-only changes stay
  in the archive rather than saturating headlines. Unknown kinds retain **Other Changes**.
- Every eligible original SHA, subject, full body, changed paths and classification
  evidence/diagnostics remains in a generated archive and `report.json`.
  Canonical summaries may consolidate equivalent changes but never erase originals.

`report.json` records original and new category counts over the **same raw commits**,
canonical counts, overlapping system counts, historical examples and every unknown
entry. Do not add system totals: a single commit can affect several systems.
Actionable diagnostics identify the SHA, evidence gap, and how future messages
should use an action/object and a Conventional Commit type/scope. These are technical
metadata, not public player notes and not permission to rewrite old messages.

### Identity, reverts and automation

A SHA identifies a commit. Full cherry-pick origin hashes prove lineage, and repeated
subjects are candidates for comparison using **Git stable patch IDs**. Attribution,
case, whitespace or identical generic messages never suffice. Matching patches on
ancestor/descendant commits are separate executions unless explicit cherry-pick
provenance proves equivalence; ordinary inverse edits need not contain revert trailers. Empty commits have no
patch equivalence. Bodies and metadata remain original in detailed records. Reverts
are separate actions even when a revert-of-revert repeats an earlier patch.
A later legitimate reapplication after a revert is not an already-published effect.

Reserved `chore(changelog)` bot commits remain excluded exactly as before; including
their own generated output would create a feedback loop. Non-merge commits named
`Merge ...` are not discarded. No original Git history is rewritten.

### Validation and regeneration

`generate --strict` validates public notes before writing any output. `check`
compares the main files **and the complete archive inventory**, read-only.
The generated ownership manifest allows regeneration to retire obsolete archive
files only after their replacements exist. Unmanaged files or an invalid manifest
are rejected before writes; arbitrary files are never deleted. Empty tagged releases
produce an actionable diagnostic; `--strict` treats it as a failure. Release preparation
always uses strict public validation. A technical-only version may be planned but
cannot be published until explicitly reviewed player notes exist. No notes are invented.
The player JSON remains schema v1, with the same four categories and SHA-based IDs.

Generated historical files are not edited manually. Regenerate locally, then run
`check`; a second identical generation reports `up to date` without rewriting files.
The `New-Testing` workflow still updates only developer outputs, now including
`docs/changelog/`; the weekly publisher stages the same archives with metadata.
No workflow is activated by this refactor, and no local regeneration publishes anything.

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
`prepare` writes the root changelogs and generated developer archives when a release is due.
Unknown, repeated or missing options fail with exit 2; validation failures use
exit 1. Existing CLI options and exit codes are preserved; technical output now includes the archives.

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
patch. Already-published equivalent changes cannot increment the version again while their effect remains active. Reusing a generic message alone never proves equivalence.

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
JSON before writing. It returns all generated output filenames, including developer archive files. Releases without visible public notes are diagnosed and refused before publication; no technical change is converted automatically into a player announcement. A no-release,
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
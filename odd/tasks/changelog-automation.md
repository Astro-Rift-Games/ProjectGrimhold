# Changelog automation (players + developers)

Objective: generate `CHANGELOG.md` + `changelog-players.json` (players, released versions only) and `CHANGELOG_DEV.md` (developers, per version + Unreleased) from Git history, kept up to date by GitHub Actions. Branch: `New-Testing` (local work-unit commits, no push). No gameplay, scene or prefab changes.
Engram mirror topic: `odd/changelog-automation/tasks`.

## Specs
- S1 Tool: zero-dependency Node 22 ESM under `tools/changelog/` (`node --test`), no new npm dependencies.
- S2 Player metadata: Conventional Commits footer trailers in the last body paragraph: `Player-Added`, `Player-Improved`, `Player-Fixed`, `Player-Balanced`. Commits without them never reach the public changelog (no `feat` inference). Unknown `Player-*` keys, empty text or misplaced trailers are warnings (errors in `--strict`).
- S3 Human review: `tools/changelog/player-overrides.json` replaces, hides or adds notes per commit before release. Preview via CLI, PR and push job summaries.
- S4 Versions: strict `vX.Y.Z` tags merged into the generated ref. Player files list only tagged releases, newest first; JSON carries version, tag, date (tag creator date), commit sha, fixed category list with stable entry ids. Reverted commits inside the same release lose their notes.
- S5 Dev changelog: sections Features, Bug Fixes, Performance, Refactoring, Tests, Documentation, Architecture & Infrastructure, Reverts, Other Changes (legacy/non-conventional); Unreleased section; commit links; merges and `chore(changelog)` automation commits excluded; duplicate messages merged into one entry with all refs, owned by the oldest version.
- S6 Workflows: push to `New-Testing` regenerates `CHANGELOG_DEV.md` and commits it with `[skip ci]` only when changed; PRs lint trailers + preview; `release: published` regenerates all files, commits them, sets the release body and uploads the JSON.
- S7 Out of scope: Unity UI, in-game download, news backend, notifications, localization, build pipeline, creating a real release.

## Tasks
- [x] T1 (S1-S5) Generator library, CLI and automated tests | route: gentle-ai-worker (two passes: functional, then readability rewrite) | evidence: `npm test` 86/86 | commit: see T2 entry
- [ ] T2 (S6) GitHub Actions workflows | route: inline after T1
- [ ] T3 (S2, S3) Commit-message skill adaptation + usage docs | route: inline
- [ ] T4 Generate initial changelog files, idempotency and history checks | route: gentle-ai-verify

## Log
- L1 (2026-10-14) Repo state: `New-Testing` clean, in sync with origin; no `.github/`, no tags; default branch is `main`; 70 merges, ~690 non-merge commits, ~350 Conventional.

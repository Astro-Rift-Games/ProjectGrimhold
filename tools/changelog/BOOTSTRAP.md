# Weekly release: bootstrap and recovery

Status: local implementation, NOT activated. No official merge, push, tag, Release or game build has been performed. MainMenu wiring, compilation and all four focused PlayMode tests passed after restart, but complete EditMode and PlayMode suites failed. Visual/input and operational proof remain pending. Do not set activation variables until these prerequisites are proven.

## Ownership and guarantees

- One orchestrator: `scripts/weekly-release.js`. Development changelog updates share the `grimhold-release-writer` concurrency group. `changelog-release.yml` is now a read-only manual audit, not a release-event publisher.
- Monday 09:15 `America/Argentina/Buenos_Aires` (`15 9 * * 1` with IANA timezone). GitHub schedules can be delayed or dropped; this is not an exact-time SLA.
- `workflow_dispatch` defaults to simulation. Simulation uses a read-only token; no push, tag push or Release mutation is executed. Local candidate commits/tags live only in disposable clones under system temp, never development worktrees.
- Normal operation uses the workflow's scoped `GITHUB_TOKEN` with `contents: write`; repository user admin access does NOT prove this token can bypass protections. Do not bypass branch protections; configure an approved policy or leave publication blocked.
- No force push, rebase, tag movement, automatic conflict resolution or rollback. Ordinary fast-forward pushes and SHA rechecks protect updates. GitHub branches, tags and Releases do not form an atomic transaction.
- Concurrency groups serialize participating workflows only. External pushes remain possible. SHA rechecks fail closed when observed, and normal push rejects a competing main update. A development commit arriving after the final read remains on development for the next integration; the selected snapshot is never silently substituted.

## Required runner and validation

Provide a dedicated, preferably ephemeral self-hosted runner labeled `grimhold-release-unity`, with Node 22, Git, Bash (Git Bash on Windows), and an already licensed Unity Editor matching `ProjectSettings/ProjectVersion.txt` (currently 6000.5.1f1). Do not store unrelated credentials on it. The candidate is trusted development-branch code, not an untrusted fork pull request.

Set `UNITY_EDITOR_PATH` to the absolute executable path and `UNITY_RELEASE_RUNNER_READY=true` only after testing that runner. The program validates Editor version, runs the changelog suite, installs existing backend lockfile dependencies with lifecycle scripts disabled and runs its unit/concurrency/integration suites (including their required MongoDB test infrastructure), invokes `ReleaseValidation.Validate`, and runs EditMode and PlayMode suites. Missing executable, license, scene wiring, test XML, zero/failed tests, changed candidate bytes or process failures must block publication. There is no build/distribution step, and metadata validation never proves multiplayer gameplay or a playable executable.

Before activation, provision the existing backend test dependencies/database requirements and any additional required validation for the first accumulated integration. Missing backend infrastructure is a failing mandatory command, not a skipped suite. The initial difference observed on 2026-10-20 is 814 commits and 7,456 changed files (including vendored assets). Do not treat the focused release-tool suites as proof of this whole accumulated change.

## Exact bootstrap sequence — requires later authorization

1. Keep `WEEKLY_RELEASE_ENABLED` and `WEEKLY_RELEASE_BOOTSTRAPPED` unset/false. Do not enable the scheduler's publication job.
2. Review the implementation and complete the independent verification findings. Investigate the recorded mandatory full-suite failures within separately approved scope, inspect the existing version label on the MainMenu Canvas following `Project Grimhold/Docs/ReleaseVersionPresentation.md`, and observe compilation, focused tests, the relevant full suites and visual/input behavior. Do not invent scene assignments.
3. Install a minimal reviewed bootstrap on `main`, separate from the 814-commit gameplay integration. Include `tools/changelog/` (source, scripts, tests, overrides, package), the three `.github/workflows/` files, and the Unity release helper, presenter, tests and verified menu wiring needed by the candidate. Preserve their existing meta GUIDs. Reconcile the same tooling into `New-Testing`; verify byte compatibility rather than assuming tools on the two branches match.
4. Recheck current branch heads, stable tags, repository rulesets/protections, Actions permissions and the scoped workflow token. Authorize the exact bootstrap commits/pushes separately. This session did not perform them. The current default branch was confirmed as `main`; current branch-policy eligibility was not.
5. Provision the runner above. Set only readiness/path variables, leaving both publication variables false. On `main`, run `weekly-release.yml` manually with `dry-run=true`. Retain `state.json`, logs, notes and JSON; require all validations to pass. Verify both remote heads/tags and Releases are unchanged before/after. No successful full Unity dry-run has occurred yet.
6. Review the first accumulated candidate and required backend/Unity coverage independently. Resolve every missing mandatory proof, including runtime/visual acceptance of the wired MainMenu label, before approving bootstrap. Do not use the initial mass merge to install its own workflow.
7. Only after explicit authorization to operate the first real publication, set `WEEKLY_RELEASE_BOOTSTRAPPED=true` and `WEEKLY_RELEASE_ENABLED=true`. Those settings also enable the scheduled publication job; this session did NOT set them. If a manual-only first real run is required, leave scheduled activation pending and revise the enablement gate explicitly before authorization, not silently.
8. Dispatch `weekly-release.yml` from `main` with `dry-run=false`, then inspect the exact resulting heads, tag target, Release, asset bytes and Unity version. Record the first real outcome; local mocks do not verify GitHub writes.

## CLI and local simulation

From the repository root, use an output directory outside the checkout:

```bash
node tools/changelog/src/release-cli.js plan --ref New-Testing
node tools/changelog/scripts/weekly-release.js --root "$PWD" --output /absolute/temp/weekly-dry-run
```

The second command is always dry-run without `--publish`, but it still requires all mandatory Unity checks. Missing validation is an error, never an optional check. `--publish` additionally requires both activation variables and `GITHUB_REF=refs/heads/main`; do not run it against the official remote without explicit authorization. There is no fake-Unity option in the CLI. Unit tests inject simulated validation only into isolated repositories.

## Artifact lineage — no self-SHA cycle

| Identity | Contents / responsibility |
|---|---|
| M | Real merge of the pinned development SHA onto pinned main; original commits preserved. |
| C | Child of M: Unity version, planned changelogs and `tools/changelog/publication.json`. The new release's versioned `commit` field identifies M, the already-known content origin. |
| `vX.Y.Z` | Immutable annotated tag at C. Its UTC date is derived from M's committer date. |
| Release asset | Generated from the tag at C, with the final C SHA in JSON; uploaded and verified before the draft is published. |
| D | Subsequent bot commit on main records post-tag generated files referring to C; no SHA circularity and no new version. |
| S | Real merge of final main into the pinned development snapshot; fast-forward push of S back to development. |

`chore(changelog)` commits and merge commits never justify a version. No stable tags means base 0.0.0: feat/breaking => 0.1.0, fix/perf => 0.0.1, internal-only => no release. After publication, first-parent strict stable tags define version history. Public trailers/overrides and SemVer impact remain independent decisions. Reverts and deduplication reuse parsed commit evidence, not rendered Markdown.

## Failure and retry procedure

Inspect the artifact `state.json`: `phase`, `failurePhase`, snapshots, target, plan, error and disposable clone path. Keep the failed run's logs. Do not edit its manifest, move tags, force push, delete Releases or re-run with a newly invented version.

| Failure boundary | Expected state | Recovery |
|---|---|---|
| Before first main push | Both remote branch heads unchanged by this run | Resolve conflicts/notes/validation on development; start a new snapshot. |
| Main integrated, no tag | Main contains C with its manifest | Rerun the same orchestrator; it discovers the manifest and resumes C, rather than merging or bumping again. |
| Tag pushed, Release missing/draft | Tag remains C | Reuse the same tag, verify existing notes/assets, finish the draft; never clobber mismatches. |
| Release published, metadata write failed | Existing Release is preserved | Verify exact existing bytes; append only metadata. A mismatch stops for human inspection. |
| Development back-sync conflicts/rejected | Main/Release remain published | Resolve the source conflict without discarding changes; retry the same publication/back-sync. No rollback. |
| Network/permission result uncertain | An operation may have succeeded | Stop mutations in this run. Next run reads current branches/tag/Release and reconciles the same manifest. |

Every remote write rechecks relevant observed heads. Final metadata and source synchronization are separate pushes, so later failures can leave a partial published state. The workflow must report it as such, not as atomic cancellation.

## Current proof boundary

Observed locally: existing generator tests, SemVer tests, temporary Git merge/publication/recovery simulations, mocked HTTP permissions/upload/idempotence checks, structural source checks. No official branch writes, tags, Releases, Actions runs or game builds were executed. After restart, compilation, MainMenu structural validation and the four focused PlayMode tests passed. Complete EditMode (4177/4177 completed) and PlayMode (768/768 completed) suites each failed; terminal MCP omitted exact pass/fail/skip totals and capped diagnostics at 25 per suite. No baseline run establishes causality. General-suite investigation/corrections need a scope decision; do not weaken the gate. Visual/input acceptance, licensed-runner operational dry-run, branch protections and token write eligibility remain unverified. Do not activate while any mandatory validation is missing.

Official scheduling reference: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule
Unity CLI reference: https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/command-line-arguments/editor
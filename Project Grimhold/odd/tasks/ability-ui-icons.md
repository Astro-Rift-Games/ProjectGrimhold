# Configure ability UI icons

## Outcome and scope
Configure the eleven supplied UI PNGs as complete Single sprites and assign the nine existing AbilityDefinition icon fields. User selected V1 for Charge and SeismicStrike. Preserve GUIDs and all gameplay configuration, code, scenes, prefabs and unrelated EditorSettings changes.

## Problem and rationale
All icons currently use Multiple import mode; disconnected art was split into fragments. All nine definition icon references are null. Existing AbilityDefinition.Icon is the presentation contract; no architecture change is required.

## Work unit
- [x] T1: Configure Single sprites with Point filtering, assign all nine icons, and verify imported assets and exact references in Unity.
  - Route: delegated; preparation/mapping required multiple definitions/importers and the writer owns bounded serialized changes.
  - Mapping: ArcaneProjectile -> ArcaneMissile; Empower -> Boost; LifeDrain -> VitalDrainage; Charge -> Lunge-V1; SeismicStrike -> SeismicStrike-V1; ProtectiveOrbs, Purification, Restoration and Trap -> matching names.
  - Checks: one full 32x32 Sprite per PNG; nine non-null exact references; GUID preservation; gameplay fields unchanged; Console errors; full diff and diff --check; no unintended file changes.
  - TDD: not applicable to serialized artwork/reference configuration; no C# behavior change or test-runner addition. Verification runner: read-only Unity execute_code and Console.
  - Functional/UI validation: imported Sprite rectangles and loaded definition references; in-game appearance remains manual.
  - Rollback: only supplied icons/importers and nine _icon references; leave EditorSettings untouched.

## Delivery and evidence
- Authorized: local importer configuration and icon assignment only; ODD work-unit commit. No push, PR, merge or remote operations.
- Initial base: New-Testing, e9239035f52119b31a65a00173af6313baab7dea.
- Current commit boundary: 6db814eae412882c0c1cb265bd2303c036d98f1c (concurrent user commit added the supplied icon assets; retained unchanged).
- Feature branch: codex/ability-ui-icons.
- Strategy: ask-on-risk; forecast under 100 authored changed lines, excluding imported generated metadata. One cohesive work unit, no PR requested.
- RDD: enabled by global preference. Assess the committed work unit against base; follow native transitions if due.
- Verified: Unity read-only inspection returned textures=11, definitions=9, issues=[]; Single/Point and full 32x32 rectangles; exact references include both V1 selections. PNG hashes and all GUIDs unchanged. EditorSettings SHA256 unchanged. Console returned zero errors; Editor idle/not compiling.
- Diff: eleven importer files change only mode, filter and Unity-generated Single spriteID; nine definitions change only _icon. git diff --check passed (line-ending warnings only). No gameplay, scene, prefab or source changes.
- T1 work-unit commit: 4df8ab899b5622919eda87b064d15372702321fa, feat(ui): configure and assign ability icons.
- Committed range: 114 total changed lines (72 additions, 42 deletions); generated importer metadata accounts for 66. Authored total including task record: 48, below delivery budget.
- Native assessment: medium; review_due=false, under_budget against 6db814eae412882c0c1cb265bd2303c036d98f1c. No review receipt claimed; this range stays pending until a later slice reaches its budget.
- Pending manual check: in-game rendered appearance. No automated C# tests run because this unit is serialized-only. No push, PR or merge performed.

## Next step
Optionally check in-game UI appearance. Implementation and Editor reference/import verification are complete.

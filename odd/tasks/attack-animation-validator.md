# Feature: attack-animation-validator

## Objective
Editor window that previews every implemented attack combination (one-hand Main, one-hand Off, two-hand, dual wield, shield in either hand + weapon) in all six facings (N, NE, NW, S, SE, SW), with VFX, on the real raid player prefab (`Assets/Prefabs/NetworkPlayer.prefab`), plus automatic per-frame checks that flag where an animation fails.

## Decisions (user)
- New tool (no prior tool found in repo, history, branches or stashes).
- Edit Mode isolated preview (`PreviewRenderUtility` / preview scene), no Fusion runner, no Play Mode.
- Grid UI: weapon combination x 6 facings, play/scrub/loop, Main clip beside derived Off clip.
- Visual + automatic checks. Priority: weapon pivot/grip must sit on the hand (current visible bug; hand position itself may be wrong). Also hand sprite identity, sorting/depth, VFX origin, return to idle.
- Coverage: every combination legal under current equipment rules.
- Scope: tool only. No animation/clip fixes, no gameplay or network behavior changes.
- Runtime approach: narrow presentation seam so presenters read through an interface; network implementation stays default with identical behavior; the preview supplies deterministic state and drives the real presenter pipeline.
- Branch: work directly on `New-Testing` (pending off hand work preserved). No commits until the user authorizes them (overrides ODD per-task commit).

## Evidence gathered
- Spike (Unity MCP execute_code): prefab instantiated in `EditorSceneManager.NewPreviewScene`, `AnimatorOverrideController` with `ArmingSword_Attack_S`, `Animator.Play("Attack", RightHand, t)` + `Update(0)` moves `MainHandGrip` (-0.406,-0.219) -> (0.168,-0.685) and rotates `RightHand`. Edit Mode evaluation works. Rig origins are 0; hand identity lives in sprite + grip.
- Network-gated reads: `PlayerCombatNetworkController.TryGetAttackPresentationSeconds` (Runner.IsRunning), equipment/shield/downed/movement/consumable controllers (Object.IsValid). Precedent seam: `IEquipmentVisualSource` used by `PlayerArmorPresenter`.

## Tasks
- [x] T1 Presentation seam: `IPlayerPresentationState` (+ network adapter wrapping existing controllers) consumed by `PlayerAnimatorView`; frame entry points callable without runner. Existing suites stay green. — route: delegated writer
- [x] T2 Migrate `PlayerWeaponPresenter`, `PlayerAttackVfxPresenter` (and Armor if needed) to the seam; no behavior change. — route: delegated writer
- [x] T3 Editor preview rig + combination catalog: instantiate prefab in preview scene, bind deterministic state, step a combination/facing/time; enumerate legal combinations from assets. Tests first. — route: delegated writer
- [x] T4 Automatic checks (pivot-on-hand first, hand sprite, sorting, VFX origin, idle return) with per-frame report. Tests first (RED on known-bad fixture). — route: delegated writer
- [x] T5 Editor window UI: grid, play/scrub/loop, filters, issue list, Main vs Off comparison, menu entry. — route: delegated writer
- [x] T6 Docs (architecture seam note + tool README) and verification (EditMode suites, compile, visual check of window). — route: verifier

## Acceptance
- Window lists every legal combination x 6 facings and renders the real prefab with weapon sprites and VFX.
- Scrub/play any cell; Main vs Off comparison available.
- Checks flag per frame where pivot/grip leaves the hand and other listed issues.
- Runtime presentation behavior unchanged (existing EditMode/PlayMode suites unchanged results).
- Unity compiles with no errors.

## Evidence / log
- Routing: worker has no Unity MCP; parent runs compile/tests. T1+T2 merged into one seam work unit.
- T1 RED (job 0eaa517a): `PlayerPresentationStateSeamTests` 4/4 failed on missing adapter/entry-point assertions; compile clean.
- Baseline full EditMode before seam (job bcb22afb): 4414 run, 14 failed = 4 seam RED + 10 pre-existing: DungeonPressureAmbientPresenterTests.InitialPhase_LateJoinInCollapse..., RaidDefeatLifecycleSourceTests.ProgressionAck..., RpcAcceptanceContractTests x3 (PlayerConsumableNetworkController), SessionCompositionConfigurationTests x2, RaidLootOriginWordCountTests x3 (NetworkPlayer/LootContainer/NetworkEnemy prefabs).
- T1+T2 GREEN: seam fixture 4/4 (job 52a635ac); full EditMode 4414, same 10 pre-existing, 0 new (job 8477ee4a).
- Independent verifier: blocker `IsDowned` merged two contracts (weapon controller-only vs VFX `PlayerDownedGate`). Fixed with explicit per-consumer source, no fallback; +2 tests. Re-run: seam 6/6 (job 3aaa6eff); full EditMode 4416, same 10 pre-existing, 0 new (job 24feef43). Armor order 200 attributed to prior off-hand session, not seam.
- PlayMode full after seam (job cd140066): 805 run, 25 failed; no pre-seam PlayMode baseline. Verifier classified all 25 as unrelated to the seam (UI authoring 30 vs 6 slots, invalid prefab ids, balance 48/22 known, provenance composition, store log text).
- Files: new IPlayerPresentationState.cs, NetworkPlayerPresentationState.cs, tests PlayerPresentationStateSeamTests.cs + FakePlayerPresentationState.cs; edited PlayerAnimatorView, CharacterAnimatorView, PlayerWeaponPresenter, PlayerAttackVfxPresenter, PlayerArmorPresenter (internal entry points Bind*/InitializePresentation/BeginFrame/PresentFrame/ReleasePresentation via existing InternalsVisibleTo). No commit (user decision).
- T3 RED (job 27066347): 13/13 NotImplementedException, compile clean. GREEN (job 7c0485c9): 13/13. Catalog with current assets: 882 cells (147 combos x 6): MainAttack 420, OffAttack 378, Defend 84, missing presentation 0. Runtime has no simultaneous starts (PlayerCombatArchitecture.md:36-44, Main-first); dual-wield shown as separate Main/Off cells; per-hand start kept internally for a future overlap mode. Follow-up for T5: Editor code uses global namespace; new files use `Grimhold.Editor.AttackAnimationValidator` -> align.
- T4 RED (job 13f3d2ac): 25 failures (NotImplemented); GREEN (job 106683b1) 45/45.
- Real-cell render (contact sheet in %TEMP%/grimhold-attack-preview) exposed: (a) preview bug: TryGetEquippedDefinition fell back to Off -> ghost Main sword in Off-only cells (runtime E:319 main-only); (b) DepthOrder ran on empty hands; (c) DepthOrder encoded presenter internals (Off weapon over hand) and hid the real bug. Visual root cause of user's Off Hand 'pivot' bug: LeftHand sortingOrder 4 under Off weapon 20 -> fist hidden behind hilt; Main RightHand 30 over weapon 20. Grip math is consistent.
- Fix RED (job fe8e0857): 74 run, 24 intended failures. GREEN (job 3f773cbd): 74/74. Preview state audited 1:1 vs runtime; new check HandOccludedByWeapon (>25% warn, >50% error).
- Real ArmingSword results: Off S/SE/SW HandOccludedByWeapon 92-100% whole attack; Off NW DepthOrder whole attack; Off+Main N/NE DepthOrder only in idle tail 0.60-0.70s (attack clips set hand order -2, idle has no sorting curve -> default 30 vs weapon -10; needs Play Mode confirmation); Main NW/S/SE/SW pass.
- T5: namespace aligned to global. RED (job bbe20d86): 96 run, 22 new NotImplemented, 74 existing pass. First GREEN run 94/96 (2 FrameCache tests: fixture state leaked across tests, fixed SetUp; production unchanged). GREEN (job 54a2e01d): 96/96.
- Window opened in Editor (Tools/Animations/Attack Animation Validator); captured via host view GrabPixels (screen capture rejected: Unity not foreground, captured unrelated desktop; file deleted). Validate on 378 OffAttack cells: 378 Fail. HandOccludedByWeapon/Off 189 (S/SE/SW), DepthOrder/Off 189 (N/NE/NW), DepthOrder/Main 112 (idle tail), VfxOrigin/Off warning 54. All 7 one-hand weapons fail in all 6 facings. Detail panel verified: live preview, Main/Off side-by-side, grip markers, per-check range bars, worst checks, ping buttons.
- T6 docs: `Assets/Editor/AttackAnimationValidator/README.md`; `Docs/Architecture/PlayerCombatArchitecture.md` subsection "Read-only presentation seam".
- Full EditMode with window open (job 5ca88a90) exposed tool bug: background Tick re-initialized presenters on a damaged rig -> PlayerAttackVfxPresenter error log contaminated 12 unrelated tests; smoke test closed/reused user window. RED (job a04c719c) reproduced exact log. Fix: rig integrity check + rebuild, lazy rebuild on asset change, pause during Test Runner (TestRunnerApi callbacks), tests own their windows. Exact external trigger not identified (no FindObjectsOfTypeAll cleanup in tests).
- Independent review blockers fixed: sequential (non-resetting) validation so ReturnsToIdle is real; explicit PresentationConfiguration error; plus render-target guard, cache resets, violation-first worst ranking, cached model rows, FailureRange own file. RED (job dade041a) 6/6 intended.
- Sequential validation exposed false positive (animated idle vs frame-0 reference). RED (job 5457a8dd) 5/5; fixed: idle reference sampled at each layer's current Animator phase; specific residue messages. User decision: HandOccludedByWeapon excludes shields.
- Final: validator suite 119/119 (job e172b7b6). Full EditMode with user window open (job 402deaea): 4535 run, same 10 pre-existing failures, 0 new; window survives and ticks without error. PlayMode not re-run after T3-T6 (no runtime changes after seam PlayMode run cd140066).
- Full catalog (882 cells): Main one-hand 24 pass/4 warn/14 fail (DepthOrder/Main N/NE idle tail); every OffAttack cell fails (HandOccludedByWeapon/Off S/SE/SW, DepthOrder/Off N/NE/NW); DualWield all fail; TwoHand WeaponGripOnHand/Off (secondary grip) 18 errors; Defend 21 pass/63 fail.
- Unattributed asset noise present: `Assets/Art/Fonts/alagard/alagard SDF.asset` (TMP dynamic atlas) and `Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset`; same pattern in user stash@{0}; not touched/reverted. Temp renders in %TEMP%/grimhold-attack-preview (outside repo).
- Commits: none (user decision).

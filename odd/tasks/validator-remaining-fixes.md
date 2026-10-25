# Feature: validator-remaining-fixes

## Objective
Fix the remaining failures reported by the Attack Animation Validator after offhand-attack-fixes.

## Decisions (user)
- Fix everything the validator detected; user will report their own findings afterwards.
- NW back facing: Off hand behind the body (-2) like N/NE, with its weapon (-10).
- Two-handed HoldsSecondaryGrip weapons (LongSword, Zweihander, GreatHammer): left hand holds the handle at SecondaryGripPoint always (idle, walk, attack), not only during the attack.
- Branch New-Testing, no commits until authorized.

## Diagnosis (read-only explorer, source evidence)
- Off N/NE: derived attack clip keys LeftHand -2, but idle/walk body clips have no LeftHand sorting key; Character.controller WriteDefaults reverts to prefab 4 (above Body 2) while Off weapon is -10. Fix: back-facing LeftHand keys in locomotion generation.
- Off NW: offensive resolver falls through to 4 (DirectionalAnimationGenerator.cs ~264-269). Fix: NW -> -2, regenerate Off attacks.
- Main N/NE: RightHand_Idle_NW.anim keys -2; RightHand_Idle_N/_NE have no sorting curve -> prefab 30 vs weapon -10. Fix: key -2 (generator-owned if possible).
- Defend (Shield Main + 1H Off, S/SE): ResolveShieldHandSortingOrder S/SE = 4 drops weapon-holding Off hand under weapon 20. Fix: 22; Off shield stays above via max(item, hand+2) = 24.
- 2H HoldsSecondaryGrip (LongSwordCombat, Zweihander, GreatHammer) WeaponGripOnHand/Off 18 + VfxOrigin/Main 24: unresolved, measure in Unity (validator vs data).
- Main shield Defend: Defend clips animate only LeftHand; presenter copies LeftHand local pose onto RightHand (PlayerWeaponPresenter.cs ~449-453) without compensating drawn-hand anchor difference. Needs per-frame trace.

## Tasks
- [x] V1 Back-facing depth (N/NE/NW Off and Main idle) + Defend S/SE Off hand order; regenerate; tests. — route: delegated writer
- [x] V2 Two-handed secondary grip + VFX origin: measure, classify, fix. — route: parent measure + delegated writer
- [x] V3 Main-hand shield Defend hand separation. — route: parent trace + delegated writer

## Blocker (resolved)
- Unity MCP tools vanished after server restart; user restarted server on 127.0.0.1:8081. Parent built HTTP bridge outside repo (~/.pi-unity-bridge/call.sh, inline JSON, no file writes by callers); workers now run Unity themselves.

## Evidence / log
- V1 worker: RED job 8cadc34d (211 run, 24 failed: missing LeftHand/RightHand back curves, NW 4 vs -2, Defend front 4/32 vs 22, catalog DepthOrder N/NE/NW, Defend HandOccludedByWeapon S/SE). Impl: locomotion LeftHand keys all facings (back -2, front 22), offensive NW -2, shared Defend -2 back / 22 front; RightHand Idle N/NE + Walk N/NE/NW constant -2 added via AnimationUtility; regenerated Locomotion, Shield Defend, Off Hand attacks; validate complete. GREEN job 45383781: 504/504. Scope approvals: generator-owned *_OffHand sets + 6 one-hand definitions.
- Parent re-validation of full catalog (bridge): OffAttack and Defend 0 violations all facings; MainAttack one-hand/dual/shield 0 violations. Remaining only TwoHand: WeaponGripOnHand/Off GreatHammer/LongSword/Zweihander x6 facings; DepthOrder/Off CompoundBow/LightCrossbow/LongBow N/NE/NW; VfxOrigin/Main warnings bows + Spellbook x6.
- V2 diagnosis (worker, measurements via bridge, no edits): A 2H grip error ~0 during attack, 4.5-8.5 px only after return to idle (no sprite swap; generator bake correct) -> becomes real change per user decision. B bows N/NE/NW LeftHand 4 / weapon -10 / body 2: string hand, not a weapon holder -> validator misapplication. C VfxOrigin: release/cast flashes use authored per-facing poses, blade-reach radius rule misapplied -> validator fix.
- V2 B corrected: WeaponDriven LeftHand IS the bow holder (RightHand = string). Bow attached to MainHandGrip in preview was preview-only: PrefabUtility.InstantiatePrefab created a connected instance that rejects reparenting; rig now clones normally (regression: sword->bow under WeaponPose, bow->sword back). Runtime-visible fix: northern bow holder LeftHand -2 (was prefab 4).
- V2 A: presenter post-Animator reversible secondary-grip pin for HoldsSecondaryGrip 2H (idle/walk/attack/transitions), anchored on animated OffHandGrip; locomotion -2 north / 25 front; also fixes GreatHammer non-planar hand separation. No clip regeneration.
- V2 C: VfxOrigin for release/cast uses resolved authored/live-anchor poses; slash/thrust keep blade radius.
- V2 RED f9087a2b (3/3 intended), GREEN d7e91522 3/3, triangulate a36d06d7 6/6, focused 617/617 (job d27c6877). TwoHand catalog 48 cells 0 violations. Renders %TEMP%/grimhold-attack-preview/holding-contract-contact.png inspected by parent: 2H support on hilt S idle/walk, bow held by holder hand.
- V3 root cause: Main shield Defend copied OffHandGrip local coords onto MainHandGrip, losing the right hand's anatomical anchor (0.75 u N/S, ~0.65 u diagonals, already at frame 0). Fix: transfer LeftHand animated translation/rotation, keep MainHandGrip anchor. Validator gap closed: grip checks now run for shields during Defend (occlusion exclusion unchanged). RED 5ae505c1 (12/12 no measurement) and b8637ba6 (6 Main-shield fail, 6 Off-shield pass); GREEN a68cd644 523/523. Defend catalog 72 cells 0 violations. Parent render v3-main-shield-defend.png: shield stays on right hand through Defend; Off sword hand visible over hilt. MagicWand *_OffHand clips rewritten by regeneration tests are the existing Wand_OffHand derived clips (metas unchanged).
- Final regression (parent via bridge): EditMode job 1548fbe5 4633 run, only the 10 known pre-existing failures; PlayMode job 2dcb4d8c 805 run, the same 25 known unrelated failures. Commits: none.

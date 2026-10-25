# Feature: offhand-attack-fixes

## Objective
Fix the three Off Hand presentation errors found with the Attack Animation Validator.

## Decisions (user)
- Spellbook is a two-handed weapon (Main Hand only). Second hand = `FollowsAuthoredMotion` (keep the authored LeftHand motion, like Magic Staff). Delete the now-unused generated `Spellbook_*_OffHand.anim` clips and `Spellbook_OffHand.asset`.
- Front facings S/SE/SW: Off hand draws above its own weapon (hand 22, glove 23, Off weapon 20), below the Main hand (30). SW no longer jumps to 32 during the attack.
- Off Hand slash attacks (ArmingSword, MagicSword) stroke toward the opposite side (mirrored), VFX included. Thrust (Rapier, daggers) and cast (Wand, Spellbook) unchanged.
- Branch New-Testing, no commits until authorized.

## Evidence gathered
- GD-12 §4.2–4.5: MVP one-hand families are Espada/Daga; Spellbook not listed, so 2H does not contradict the GD. MagicWand also not listed as one-hand (reported, unchanged).
- Spellbook asset: `_handedness 0`, `_secondHand 0`, `_rig 0`, `_aimMode 0`; 2H validation passes with handedness flip (WeaponDefinition.cs:242-266).
- Sorting: `PlayerWeaponPresentationMath.cs:68-70` forces Off weapon >= hand+2 (weapon 20 over hand 4); `DirectionalAnimationGenerator.cs:25, 252-257` sets Off hand order (S/SE 4, SW 32, N/NE -2, NW falls to 4).
- Off clip derivation `OffHandAnimationGenerator.CreateClip`: offGrip(t)=offNeutral+(mainGrip(t)-mainNeutral), rotation delta copied, no mirror. VFX: `PlayerWeaponPresenter.RefreshOffHandAttackPose` uses pose + GetGripOriginOffset.

## Tasks
- [x] F1 Spellbook two-handed: definition, regenerate Spellbook Main clips keeping LeftHand motion, remove Off set + delete derived assets, update tests/docs/validator expectations. — route: delegated writer
- [x] F2 Off hand sorting S/SE/SW above its weapon; regenerate Off clips; tests; validator HandOccludedByWeapon passes in S/SE/SW. — route: delegated writer
- [x] F3 Mirrored Off Hand slash stroke + VFX for slash sets; regenerate; tests; validator + visual check. — route: delegated writer

## Acceptance
- Spellbook only legal in Main Hand; catalog shows it as TwoHand; no Off Hand Spellbook assets remain.
- Validator: no HandOccludedByWeapon on Off weapons in S/SE/SW.
- Off ArmingSword/MagicSword swings and VFX mirror the Main stroke; thrust/cast unchanged.
- Full EditMode: only the 10 known pre-existing failures.

## Evidence / log
- F1 RED (job 51b41602): 266 run, 4 intended failures (Spellbook OneHanded, catalog 7 one-hand, preview lists Spellbook Off).
- F1 assets (parent, Unity): SpellbookWeaponDefinition `_handedness` TwoHanded, `_presentation._secondHand` FollowsAuthoredMotion, `_offHandAttackAnimationSet` cleared; TryValidate true. Ran `Tools/Animations/Generate Spellbook Directional Attacks` (6 Spellbook_Attack_* clips +377 lines each: LeftHand curves kept). Deleted via AssetDatabase: Spellbook_OffHand.asset + 6 Spellbook_Attack_*_OffHand.anim (untracked, no external refs).
- GD conflict: live GD-09 §5 lists "Libro de hechizos | 1M"; user confirmed two-handed (2026-10-24). `WeaponCatalogTests.Weapons_MatchApprovedCombatConfiguration` updated with note; GD-09 owner update pending (not edited by us).
- F1 GREEN (jobs 1792e7b0 + 9347e527): 493 affected tests pass after test update; OffHand catalog valid; preview catalog 696 cells, Spellbook only Main (6 cells), never with Off.
- Risk reported: saved profiles with Spellbook in an Off Hand slot are rejected on load (PreparedEquipmentLoadout.cs:103,133; LocalProfileSaveCodec.cs:393,443; LocalProfileRepository.cs:71-114 backup fallback); no migration.
- F2 RED (job 240a344f): 104 run, failures capped at 25 (math 24/34 vs 20; hand 4/32 vs 22; HandOccludedByWeapon all front cells).
- F2 impl: PlayerWeaponPresentationMath front Off weapon fixed at 20; generator Off hand S/SE/SW 22 (SW no 32), N/NE -2 and NW 4 untouched; front-only LeftHand sorting keys in locomotion. Scope guard: shields keep item above hand (max(item, hand+2)) in either hand; Shield Defend generator output unchanged. Stacks: Off weapon 20 < VFX 21 < hand 22 < glove 23; Off shield idle hand 22 < shield 24; Main shield hand 30 < shield 32.
- Regenerated (Unity menus): Generate Weapon Pose Locomotion, Generate Off Hand Attacks; Validate Off Hand Attacks = complete and up to date.
- First GREEN run (job 110ea45f) 437/455: 18 glove failures = fixture issue (preview equips no gloves -> glove disabled keeps prefab order; production rule hand+1 correct). Test fixed. GREEN (job 62679f01): 455/455.
- Visual (f2-after.png in %TEMP%/grimhold-attack-preview): Off fist visible over hilt S/SE/SW whole swing; shields still cover holding hand. Pre-existing separate finding: Main-hand shield Defend S, right hand separates from shield after frame 1 (position, not sorting) - not addressed.
- F3 RED (job b488088e): 103 run, 19 intended failures (no `_offHandStroke`).
- F3 impl: `OffHandStroke {Reanchored, Mirrored}` on DirectionalAttackAnimationSet (copied to derived set, in source signature, format off-hand-retarget/3); generator reflects grip displacement in VisualRoot X and planar Euler rotation keys/tangents (theta_off = -theta_main + neutral offset), position bake 240 Hz; presenter mirrors blade alignment and VFX position/rotation/flip; Mirrored+FreeAim rejected inside the FreeAim validation block (parent moved it after `WeaponAimModeValidationTests.FreeAim_OnMeleeWeapon_IsRejected` regressed on message order).
- Assets (parent, Unity): Sword1H and MagicSword sets Mirrored; Rapier, Dagger, Wand Reanchored. Regenerated Off Hand attacks; validate = complete and up to date. Unity serializes the empty `_retargetSourceSignature:` field with trailing space in the five main set assets (git diff --check warning, Unity-generated).
- First GREEN run (job 80254c0f) 473/483: between-key rotation 0.34 deg and blade 0.007 u (interpolation, root cause: cubic Euler source re-baked as 60 Hz linear quaternion). Fixed. GREEN (job 793c3134) 484/484.
- Seam test regression (2 cases, 1.25 u = 2 x ArmingSword grip 0.625 mirrored vs unmirrored): test reference stale for Mirrored sets; fixture switched to Reanchored Rapier; mirrored coverage stays in OffHandMirroredStrokeTests.
- Visual (f3-mirror.png): Off slash arcs and VFX mirror the Main stroke in all six facings.
- Final regression: EditMode (job c472a01d) 4591 run, only the 10 known pre-existing failures. PlayMode (job 66bea738) 805 run, the same 25 known unrelated failures as after the seam run, 0 new.
- Catalog after fixes (696 cells): HandOccludedByWeapon gone from all attack cells. OffAttack OneHandOff 18 pass (S/SE/SW) / 18 fail DepthOrder/Off (N/NE/NW, out of scope). Remaining: back-facing DepthOrder (Main N/NE idle tail, Off N/NE/NW), TwoHand WeaponGripOnHand/Off 18 + VfxOrigin warnings 24, Defend HandOccludedByWeapon/Off 12, Main-shield Defend hand separation.
- Commits: none (user decision).

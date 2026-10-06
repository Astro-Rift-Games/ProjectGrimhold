# Town Abilities tab

Branch: `feat/town-abilities-tab` (from `New-Testing` @ `7c6c613c`). Follows `odd/tasks/town-tabbed-menu.md`.
Engram mirror topic: `odd/town-abilities-tab/tasks`.

## Objective
Add an Abilities tab to the Town player menu: unlocked abilities list (filter by resource), ability details, the two universal slots with equip/clear, requirement check and preparation rules, following the user's reference image.

## Problem
The ability domain (unlocks, prepared slots, requirement validation, Raid runtime) is done; there is no UI. `AbilityDefinition` has no display data, the catalog cannot be enumerated, and there is no Ready-gated equip endpoint for Town.

## Scope
- In: display metadata on `AbilityDefinition` (+ 9 authored assets), catalog enumerator, pure presentation builder, Ready-gated mutation endpoint, Abilities tab view/presenter/prefab, tab id, wiring in `SocialPlayer`, docs.
- Out: Abilities hotkey (no input action change), Raid HUD for abilities, ability art (icon field is optional, art is authored by the user), acquisition sources (merchant / dungeon / missions), Mana execution.

## Sources
- GD doc 11 (sections 5, 6, 7, 14): unlock is independent of attribute requirements; 2 universal slots, 0/1/2 equipped, same ability not in both; equip and use need all attribute requirements; respec clears invalid slots; changes only in Town, Preparation, player Not Ready.
- `AbilitySystemArchitecture.md`: UI must not mutate the repertoire directly; everything goes through `LocalProfileStore` (`TrySetPreparedAbility`, `TryClearPreparedAbility`); `ProfileCommitted` is the refresh signal.

## Constraints
- Raid admission/runtime consumers of `AbilityDefinition` must not break; new fields must not be required by `TryValidate` (`MvpAbilityCatalogAssetTests` expects 9 valid definitions).
- Only data the model defines is shown: requirement, resource + cost, cooldown, description. Cast time, targeting and max-simultaneous from the mock-up are not modeled and are omitted.
- Slot key labels are read from the real bindings (Q / R), not the mock-up's Q / E (E is Interact).
- UI text in English (matches existing UI and asset ids). Names live on the assets, so a later localization pass is one field.
- Planning heuristic ~400 authored changed lines per task (advisory only).

## Resolved test configuration
- TDD: enabled (Strict TDD Mode, source: user CLAUDE.md).
- Runner: Unity Test Runner via UnityMCP (`run_tests`, EditMode/PlayMode); bridge connected on port 6401.

## Tasks
- [x] A1 `AbilityDefinition` display fields (name, description, icon), catalog `Definitions` enumerator, author the 9 assets (`7b505724`)
- [x] A2 Pure `TownAbilitiesBuilder` presentation model (entries, state, requirement checks, resource filter) + EditMode tests (`8822d7d3`)
- [x] A3 `TownAbilityMutationEndpoint` (Ready-gated wrapper over `LocalProfileStore`) + EditMode tests (`7740cb37`)
- [x] I1 (user request) Interact moves from E to F; AbilitySlot2 moves from R to E, so Q and E are the ability slots (`038e730f`)
- [x] A4 `TownMenuTabIds.Abilities`, view + presenter + `TownAbilities.prefab`, wiring in `SocialPlayer.prefab` / binder, PlayMode tests (`bcff0c02`)
- [x] A5 Docs (`TownPlayerMenuArchitecture.md`, `AbilitySystemArchitecture.md`), Play Mode visual check, full test runs

## Route declaration
- A1-A3: delegated writer (reading 4+ files prepares the write; 2+ non-trivial files).
- A4: parent builds prefab through UnityMCP; presenter/view code by the writer or inline per trigger evidence.

## Progress / evidence
- Mapping (delegated explorer, trigger = reading 4+ files): no production unlock source exists (`TryUnlockAbility` only in tests), so a fresh profile has an empty repertoire and the tab will show an empty state; there is no ability art; `LocalProfileStore._abilityCatalog` is private and not exposed by `ApplicationStashContext`, so the presenter gets a serialized catalog reference (same pattern as the attributes presenter `_lootCatalog`).
- A1-A3 (delegated writer, route: delegated direct; trigger = reading 4+ files to write, 2+ non-trivial files). RED observed by the writer (CS1061 on `DisplayName`/`Description`/`Icon`/`Definitions`, then failing `charge has no authored display name`; CS0246 on the builder and endpoint). GREEN verified by the parent: 57/57 across `AbilityDefinitionTests`, `AbilityDefinitionCatalogTests`, `MvpAbilityCatalogAssetTests`, `TownAbilitiesBuilderTests` (20), `TownAbilityMutationEndpointTests` (9); the nine MVP definitions still validate.
- Full EditMode: 2377 run, 42 failed with the changes; on the base `7c6c613c` (changes stashed) 2342 run, 42 failed, i.e. the same count and the same families (Merchant, SessionComposition, NetworkPlayerMelee/Ranged prefabs missing, ArmorSetCatalog, RaidAdmissionDataCodec ThirtyEntryInventory, ...). All pre-existing and unrelated; the +35 tests are the new ones.
- Unity re-saves about 40 `Assets/Animations/Weapons/Directional/**/*.anim` and `alagard SDF.asset` during full test runs; reverted with `git checkout`, never committed.
- Deviation: the endpoint also takes the `AbilityDefinitionCatalog` (needed by the non-mutating `CanEquip`).

- I1 (inline, mechanical edits): RED observed (6 failures: asset still had Interact on E and slot 2 on R, and the Abilities key-label test expected E) after fixing a typo of mine (a blanket `Key.E` replace turned `Key.Escape` into `Key.Fscape`, which broke project compilation until corrected). GREEN: EditMode `PlayerInputReaderTests` + `TownAbilitySlotKeyLabelsTests` 16/16 (includes the regenerated wrapper); PlayMode `PlayerInputReaderPlayModeTests` + `RaidInventoryPresenterInteractionTests` 61/61. The regenerated `PlayerInputActions.cs` differs by exactly two path lines. Updated every on-screen hint that named E for Interact: `InteractionHudPresenter`, `TownRaidPreparationView`, `RaidMenuView` controls list, `LocalGameplayHud`/`SocialPlayer` prefab hints, `DialogueUI` ("Press (F) to continue", the dialogue advances with the Interact action). `RaidMenuPlayModeTests` has 2 failures that are identical with the text change reverted and do not use keys (pre-existing Raid menu failures). The three hint strings are still hardcoded in code (not derived from the binding).
- Advisory from the A1-A3 native review (approved, non-blocking): a Blocked `TownAbilityMutationResult` carries a default `Preparation` equal to `Success`; the UI must decide from `Outcome`.

- A4 (delegated writer, resumed after the input blocker; route: delegated direct; trigger = 2+ non-trivial files plus prefab). RED observed: CS0103 on the new helpers, then 3 of 18 PlayMode tests failed (selection highlight; unregister assert because `Destroy` is deferred) and were fixed (Shown/Hidden callbacks now call `view.Open()`/`Close()`; the unregister test is a `[UnityTest]`). The Ready gate is shared by passing the binder's `CanMutateEquipment` method group, no extraction. Luck short code is "LUCK" (decision), the other codes VIT RES STR DEX INT.
- A4 GREEN verified by the parent: EditMode `TownAbilit`, `TownMenuTabIds`, `TownPlayerMenu`, `TownOptions`, `TownInventory`, `MvpAbilityCatalogAsset`, `PlayerInputReaderTests` 95/95; PlayMode `TownAbilit`, `TownPlayerMenu`, `TownOptions`, `TownInventory`, `TownAttribute` 47/47.
- A5 full runs: EditMode 2415 run, 42 failed and the failing set is identical to the base `7c6c613c` (compared by test name, none new, none fixed); PlayMode `Tests.PlayMode.Presentation` + `PlayerInputReaderPlayModeTests` 146 run, 12 failed, the known Raid HUD / Raid menu / loot / Fusion 'Invalid prefab id' families.
- Visual check by the parent (Play Mode, 1920x1080, temporary harness with the real prefab and catalog, 9 unlocked abilities, Seismic Strike equipped in slot 1): three columns fit the 1350x650 content area; filters, card grid with state badges, details, both slot panels with Q / E, the requirement check with a failing requirement ("X DEX (Trap) 5 / 10"), the rules and the combat bar preview render. Not seen: the "Locked while Ready" state (covered by a PlayMode test), the real Town scene with a Fusion session and the real tab bar. Minor: "Stamina | CD: 10s" wraps to a second line in the slot panel; icon frames are empty placeholders (no ability art); the details column has unused space under the description.
- Harness artifacts (`Assets/Screenshots`, the temporary scene objects) deleted; Unity noise (`alagard SDF.asset`, `EditorSettings.asset`) reverted, not committed.

## Next step
Manual check in the Town scene with a Fusion session, a way to unlock abilities (no production source exists), ability icons, and an optional Abilities hotkey.

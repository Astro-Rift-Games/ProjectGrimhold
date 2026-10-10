# Raid Main HUD Architecture

## Context

The main HUD provides an always-available Raid summary for the local player and connects its quota, Sanctuary and ritual/extraction sections to the existing confirmed extraction queries. The HUD remains presentation-only: it does not own health, combat, loot, equipment, or extraction state, and it introduces no replicated fields.

The HUD is composed in the productive Raid avatar `NetworkPlayer.prefab` under the existing `LocalGameplayHud` Canvas.

## Decision and component flow

```text
Input Authority NetworkPlayer
  -> LocalPlayerHudBinder
      -> RaidHudPresenter
          -> RaidHudView
      -> RaidTeammateHudPresenter
          -> RaidTeammateHudView
      -> DungeonPressureHudPresenter
          -> DungeonPressureHudView
```

- `LocalPlayerHudBinder` remains the only local HUD binding boundary. A player without Input Authority keeps `LocalGameplayHud` inactive.
- `RaidHudPresenter` is a local `MonoBehaviour`. It caches gameplay references, reads them without side effects, performs section-level dirty checking, and owns no clock.
- `RaidHudView` contains only uGUI/TMP references and explicit presentation or clearing operations.
- `RaidTeammateHudPresenter` resolves the one frozen teammate by stable `ProfileId`, observes
  replicated participant/avatar state and performs section-local dirty checking. Its view owns only
  visibility, text and fill rendering.
- `RaidMainHud` is a non-interactive visual root and a sibling of `RaidInventoryScreen`. It now frames only the vitals block (Health, Mana, Stamina; the defeated indicator sits just above the frame). The presenter and view remain on `LocalGameplayHud`, outside the visual root they control; the view's quota, Sanctuary, ritual/extraction and expedition-progress references point into `RaidRightPanel` (see Layout).
- `RaidDuoHud` (the teammate HUD root, shown and hidden by `RaidTeammateHudView`) is authored inactive, so it is never visible before a teammate is presented.
- `RaidCooldownHud` is a bottom-centered visual root on the same Canvas. `RaidHudPresenter` resolves the active weapon's `LootDefinition` from `PlayerWeaponEquipmentNetworkController` and uses its `Icon` (falling back to `WorldSprite`); a dark radial image and a compact decimal-seconds label render replicated cooldown progress. The seconds text is formatted with `CultureInfo.InvariantCulture`, so the machine locale never changes the decimal separator. `RaidHudView` also owns a distinct, empty `AttackText` label instead of sharing the cooldown seconds label.

No additional Canvas, HUD prefab, global manager, service locator, event bus, or per-frame component search is used.

## Sources of truth

| HUD section | Source |
| --- | --- |
| Current health | `CharacterBase.Health` |
| Maximum health | `CharacterBase.MaxHealth` |
| Defeat | `!CharacterBase.IsAlive` |
| Teammate identity | immutable `RaidInitialAffiliationSnapshot` from the frozen `RaidLaunchParticipant` roster |
| Teammate current health | current teammate avatar `PlayerCharacter.Health` |
| Teammate maximum health | current teammate avatar `PlayerCharacter.MaxHealth` |
| Teammate defeat | teammate `NetworkRaidParticipant.State == Defeated` |
| Current Stamina and Exhaustion | `PlayerStaminaNetworkController` replicated state |
| Maximum Stamina | `PlayerStaminaNetworkController.TryGetMaximumStamina` from the admitted attributes |
| Current and maximum Mana | `PlayerManaNetworkController.CurrentMana` / `TryGetMaximumMana`, read only while `IsInitialized` |
| Attack availability and cooldown | `PlayerCombatNetworkController.TryGetPrimaryAttackStatus` |
| Active weapon icon | `PlayerWeaponEquipmentNetworkController` -> `LootDefinition.Icon` / `WorldSprite` |
| Loot value inside the inventory screen | `LootInventoryValueCalculator.TryCalculate` over the inventory source's loot content, in `RaidInventoryPresenter` |
| Extraction countdown | local `PlayerExtractionController.TryGetProgress` |
| Individual quota and expedition progress | local `PlayerExtractionProgressController.TryGetSnapshot`; bar fraction through `ExpeditionProgressMath` |
| Sanctuary assignment and ritual | runner `ExtractionSanctuaryAssignmentService`, `EntityRegistry`, `IExtractionSanctuary` |
| Dungeon Pressure timer and phase | `DungeonPressureController` on the match controller resolved through `NetworkSpawnManager.MatchController` |

`CharacterBase.MaxHealth` exposes the effective maximum as read-only, locally derived data; it is
not a second networked health value. The base implementation used by NPCs returns the maximum
authored on their prefab. `PlayerCharacter` overrides that projection and derives the value from
the `CharacterAttributeState` frozen on its linked `NetworkRaidParticipant`. A temporarily
unresolved participant link uses the authored player-prefab value only as a non-cached fallback,
so Host Migration reference fixup can later resolve and cache the admitted value. The HUD consumes
this same effective maximum and never derives or stores another health cap.

## Duo teammate projection

`RaidInitialAffiliationSnapshot` remains an immutable projection of the frozen launch roster. It
resolves the local profile's only teammate by `RaidTeamId` equality and a distinct `ProfileId`;
the numeric team value has no ordering or indexing meaning. `ProfileId` is retained as the logical
identity used to re-resolve a runtime participant. `RaidParticipantId` remains Raid-generation
identity and is not promoted to durable identity.

`NetworkSpawnManager.TryGetRaidInitialAffiliations` is the current public boundary for projecting
its private `RaidLaunchContext`. `LocalPlayerHudBinder` obtains a fresh snapshot once per bind and
Raid generation, then passes the snapshot to `RaidTeammateHudPresenter`; the presenter has no
dependency on the spawning service and creates no participant registry.

The presenter resolves the matching `NetworkRaidParticipant` from the runner's existing
PlayerObjects and replicated objects. A reusable buffer is used only while a reference is absent or
invalid. The cached avatar is invalidated whenever `CurrentAvatarId` changes, the object becomes
invalid, or the runner/generation changes. An unresolved known Duo member displays
`Compañero: — / —` and is retried during the normal presentation update; no RPC, scene search,
network polling or event-based state copy is introduced.

While an avatar is current, the presenter reads its replicated `Health` and derived `MaxHealth`
directly. A `Defeated` participant displays zero Health from that terminal participant state; the
lootable corpse is never a Health source. If a participant is `Extracted` after its avatar becomes
unavailable, the last displayed Health pair may remain as a local presentation cache. That cache is
not authoritative or replicated and is cleared by unbind, runner replacement or generation change.
`Aborted` and other unavailable-avatar cases display the placeholder.

## Combat evaluation and authority

`PrimaryAttackStatus` is a small immutable presentation value containing availability, total cooldown duration, and remaining cooldown time.

`PlayerCombatNetworkController` owns one private, side-effect-free evaluation of the stable primary-attack prerequisites: valid runner and dependencies, attack enabled, living character, and expired or unstarted cooldown. Both authoritative execution in `FixedUpdateNetwork` and `TryGetPrimaryAttackStatus` use that evaluation. Input edges and aim direction stay in the execution flow and are not reported as persistent availability.

The State Authority check remains in the execution flow. It is deliberately absent from the read query so the Input Authority player can inspect replicated state. The query neither executes an attack nor changes `TickTimer`, and no new `[Networked]` state exists for the HUD.

The presenter obtains normalized cooldown fill from the reported duration and remaining time. Duration at or below zero, negative inputs, `NaN`, and infinity produce zero; otherwise the ratio is clamped to `[0, 1]`. The presenter never advances a local cooldown clock. The cooldown image uses uGUI radial fill and remains at its authored scale. An authoritative cooldown rejection pulses the local icon once; it does not modify the timer or show global text.

## Active weapon resolution

The HUD receives the same `PlayerWeaponEquipmentNetworkController` already bound for the
local avatar. It observes the replicated active slot, resolves that slot's catalog identity and
then the local static `LootDefinition`; no RPC or HUD-specific networked value is involved. No
active weapon or an unresolved identity clears the icon. A changed
active slot replaces the icon without changing combat or equipment state.

## Inventory summary and value recovery

The always-visible HUD shows no inventory summary. The former `Inventario: n / m` text was removed by product decision: `RaidHudView` has no inventory label, `RaidHudPresenter` no longer reads `PlayerLootReceiver` (its `Bind` overloads lost that parameter), and neither `RaidMainHud` nor `LocalGameplayHud` contains an `InventoryText` object. Inventory occupancy and the complete loot value belong to the `RaidInventoryScreen`, and the pickup toast (`LootHudPresenter`) keeps its own economic text. Both use `SellValuePerUnit`; `ExtractionValuePerUnit` is never shown as currency.

`RaidInventoryPresenter` computes the complete value with `LootInventoryValueCalculator.TryCalculate` over the inventory source's loot content when it refreshes the player panel. A failed complete-value read displays `Valor: —`, keeps only the value refresh pending, and retries on subsequent presentation updates without rebuilding slots, using a timer, coroutine, or inventory subtotal. One diagnostic is emitted per failed episode. Once a complete read succeeds, the presenter stops recalculating until the inventory source `Revision` changes.

## Dirty checking

Health/defeat, Stamina/Exhaustion, Mana, attack/cooldown, quota, expedition progress, assigned Sanctuary and ritual/extraction status maintain independent observed state. The presenter writes a section only when its visible state changes. The Stamina section reads the networked owner without advancing regeneration or consumption; an unresolved participant source clears only that section. The Mana section follows the same rule through `RefreshManaSection`; an invalid or uninitialized Mana controller clears only Mana. The view additionally avoids assigning identical TMP text, fill, scale, active-state, or root-state values.

The teammate section independently dirty-checks its visible mode, Health and maximum. It follows
the existing presentation `Update` pattern and never advances simulation state.

Attack status may be queried each presentation frame so enablement, defeat, and timer expiry are observed. Loot value is not recalculated each frame after a successful read.

## Lifecycle

`OnEnable` before binding is valid and has no effect. Bind starts from an idempotent unbind, clears placeholders, caches the current sources, and requests initial reads.

`Unbind`, Fusion despawn, destroy, and session replacement remove the `LocalInputContext.ReaderChanged` listener, clear cached dependencies, feedback and late-resolution state, clear the presenter/view, and deactivate `LocalGameplayHud`. Disabling `RaidHudPresenter` itself clears visual observations and baselines but keeps its sources so re-enable can rebuild without a second binder subscription. Re-enable performs a fresh bind only for a valid player with Input Authority. Defeat keeps the persistent HUD visible but immediately clears transient combat feedback.

The teammate presenter participates in the same binder lifecycle. Its `Unbind` clears participant,
avatar, logical identity, generation and extracted-Health cache so no projection can survive into a
second Raid. Host Migration and rebind reuse the existing participant/avatar lifecycle; this HUD
does not implement reconnection or recovery.

## Extraction presentation

The former single extraction label is split into three independent text sections plus one progress indicator, each with its own dirty check and its own clear operation:

| Section | `RaidHudView` member | Content |
| --- | --- | --- |
| Quota | `QuotaText` | `Progreso: n / m`, or the persistent `Cuota completada` |
| Expedition progress | `ProgressRoot`, `ProgressFill`, `ProgressPercentText` | Individual progress bar and whole percentage |
| Sanctuary | `SanctuaryText` | `Santuario asignado` while a valid assignment resolves |
| Ritual/extraction status | `ExtractionText` | Terminal, countdown, cancellation and ritual status (priority below) |

A missing or invalid source clears only its own section. `Cuota completada` is persistent: it is the quota section's state whenever `IsQuotaComplete` is true, with no transient confirmation duration and no competition with the Sanctuary text. The only remaining presentation-time feedback is the extraction cancellation message (`_cancellationFeedbackDuration`, unscaled).

The ritual/extraction status keeps one explicit priority: terminal `Extracted`, active extraction countdown, existing cancellation feedback, completed ritual, in-progress ritual, cancelled ritual, and finally the unavailable placeholder. The expedition progress fraction is clamped and floored by `ExpeditionProgressMath`, so 199/200 never shows 100%; a non-positive quota hides the indicator. Progress is individual (MVP): no text or projection speaks of team progress.

`LocalPlayerHudBinder` passes the local `PlayerExtractionController` into `RaidHudPresenter`. The presenter baseline observes the first valid `ExtractionCountdownSnapshot`, so joining during an active countdown or after completion does not emit a false transition. This countdown contract was renamed from `ExtractionProgressSnapshot` when individual quota progress was introduced, so the latter can exclusively describe that progress. A valid `InProgress` snapshot displays the sanitized remaining duration, `InProgress -> None` displays one cancellation message for the configured presentation duration, and `Extracted` displays a persistent terminal label. An invalid or unavailable read clears the observation baseline and shows the unavailable placeholder without fabricating a cancellation or completion.

The HUD adds no team progress projection. `ExtractionProgressSnapshot` is presented as the local individual quota text and expedition progress bar, while assignment and ritual text are derived from the runner-scoped assignment service, registry and Sanctuary snapshot. Pickups and inventory economic text use `SellValuePerUnit`; `ExtractionValuePerUnit` is never displayed as currency.

The extraction HUD section never writes player state, calls an extraction command, or uses a parallel local countdown. The local HUD remains available after `Extracted`; authoritative interaction, damage and loot protocols continue to enforce the existing terminal restrictions.

The extraction binding includes nullable presentation sources for individual progress,
Sanctuary assignment and the runner registry. `LocalPlayerHudBinder` resolves the assignment
service and registry once per binding, but their absence is section-local and cannot disable
the health, combat, inventory, interaction or menu HUD. `RaidHudPresenter.Bind` accepts all
three extraction sources as nullable and evaluates them independently.

`RaidHudPresenter.OnDisable` clears visual output, feedback and observation baselines while
retaining bound references. `OnEnable` rebuilds from the current confirmed state and starts
fresh baselines. `Unbind` and `OnDestroy` remove references and presentation state. A source
that becomes invalid clears only its own baseline, so recovery cannot replay historical quota
feedback or cancellation transitions.

The world Sanctuary presentation is not local assignment state. Its presenter resolves
`Runner.LocalPlayer` and the current `PlayerObject` on every update, derives the current
`EntityId`, and discards private ownership immediately if that context disappears or changes.
No Sanctuary uses `HasInputAuthority`, static identity, scene lookup or a parallel assignment
cache. `ExtractionSanctuaryPresenter` is the exclusive visual owner of the Sanctuary renderer;
the co-located `ExtractionZone` contributes only interaction geometry and contains no renderer
or fallback logic. The presenter preserves the renderer's authored alpha and only changes RGB
for state presentation.

The local minimap is a separate `RaidMinimapPresenter`/`RaidMinimapView` section
bound by `LocalPlayerHudBinder` and composed as the first child of `RaidRightPanel`; `RaidHudPresenter` retains responsibility only for textual HUD
content and the expedition progress bar. `MinimapLayoutGenerator` derives the immutable `MinimapLayout` asset from the serialized
`Floor`, `Walls` and `Obstacles` Tilemaps of `Dungeon_Graybox.prefab`, including its world pivot,
cell size, occupancy and a source hash. `RaidMinimapGraphic` renders that north-up layout as uGUI
geometry inside a `RectMask2D`, with a centered local marker and a private Sanctuary marker. It
adds no Canvas, camera, RenderTexture, networked property, RPC, event bus or gameplay timer. A
changed graybox is regenerated through the permanent editor generator; no hand-drawn or static PNG
representation is retained. The generated pivot is prefab-local; the presenter applies the
serialized `Dungeon_Graybox` scene-instance world offset before projecting the background, keeping
the fixed player marker aligned with the actual Gameplay placement without any scene lookup.

The map uses `_uiUnitsPerWorldUnit` in local RectTransform units at the Canvas reference
resolution. `MinimapProjection` computes the inverse background translation and the edge
intersection for external Sanctuary positions. Its result uses mathematical angles (`0` east,
`90` north, `180` west, `-90` south); `RaidMinimapView` alone applies the serialized arrow
correction. Zero player/Sanctuary displacement is valid and remains centered.

`RaidMinimapPresenter.LateUpdate` validates the current runner and PlayerObject, projects the
background, reads confirmed extraction, resolves or revalidates only the local assignment, reads
the current ritual snapshot and presents either the interior icon or exterior arrow. Extraction
completion hides only the Sanctuary marker; defeat and extraction leave the minimap visible while
the PlayerObject remains valid. Disable, despawn, replacement, shutdown and re-enable clear
visuals and cached references without replaying historical transitions.

## Layout

Positions below are authored in prefabs and guarded by EditMode layout tests; none has been validated visually in the Game view or Play Mode.

- **Right panel.** `RaidRightPanel` in `LocalGameplayHud.prefab` is a top-right column (`VerticalLayoutGroup`, 240 wide) with four children in order: `RaidMinimap`, `ObjectivesBlock` ("Objetivos de expedición": quota text, then the expedition progress bar and percentage), `SanctuaryBlock` ("Santuario") and `RitualBlock` ("Estado del ritual"). Expedition progress is individual, not team.
- **Bottom row.** The weapon cooldown (`RaidCooldownHud`) and the Q and E ability slots (`RaidAbilityHud`) form one even row of 72x72 slots (8 px gaps) at the bottom center, in the order weapon, Q, E. The ability slots reuse the weapon slot frame style. `ActionBarFrame` (a non-interactive sliced `UI_0` image, 256x96, first among the bottom-row siblings so it draws behind them) is a dark container enclosing the three slots with an even 12 px padding. Each slot has a `KeyBadge` (dark plate) with a `Key` label at its top-left corner: the weapon slot shows `RaidWeaponSlotKeyLabel.Label` (`LMB`), the ability slots `TownAbilitySlotKeyLabels` (`Q`, `E`). Both label sources are constants guarded by EditMode tests against the real Input Actions (`Gameplay/PrimaryAttack` = `<Mouse>/leftButton`; `AbilitySlot1/2`); the weapon label is static text in `RaidCooldownHud.prefab`. Cooldown seconds and key labels use the `alagard SDF Outline` material (outline plus underlay shadow) so they stay readable over icons. State visuals (ready, cooldown, preparing, executing, insufficient resource) are unchanged. The bar has only three slots: no Quick Slots, no item counts and no hotbar.
- **Vitals block.** `RaidMainHud` is anchored bottom-left (24 px margin) as three icon + bar rows (Health, Mana, Stamina). Each bar is a dark track with a coloured fill anchored and pivoted at the left edge, and one centered value text (for example `85 / 130`) drawn over the fill, never inside it. There are no static name labels. Icons are 16x16 pixel sprites under `Assets/Art/UI/Hud`. The defeated indicator sits just above the frame and `RaidDuoHud` above it; none of them overlaps the bottom action bar, the Dungeon Pressure timer and phase, or the interaction prompt.
- **Quick Slots are not implemented.** The Game Design defines four Quick Slots and weapon sets A/B; neither is in the HUD and both remain future work. No placeholder slots exist.

## Dungeon Pressure HUD

`DungeonPressureHudPresenter` is bound by `LocalPlayerHudBinder` with `Bind(NetworkRunner, NetworkSpawnManager)` and unbound with the rest of the HUD. It resolves the `DungeonPressureController` through `NetworkSpawnManager.MatchController` and revalidates it against the bound runner; there is no `FindObjectOfType` or scene scan. A missing runner, spawn manager, match controller or controller shows the unavailable timer `--:--` and clears only this section; a runner that stops running unbinds the presenter. It dirty-checks seconds and phase, adds no networked state and owns no clock.

## Interaction prompt

The local interaction prompt uses the format `[F] action`, built by `InteractionPromptText.Format` and shown by `InteractionHudPresenter` (`TownRaidPreparationView` uses the same helper). A blank action falls back to `Interactuar`. `InteractionPromptText.KeyLabel` is a constant, not a runtime read of the binding; an EditMode test (`InteractionPromptTextTests`) compares it with the keyboard path of the real `Gameplay/Interact` action (`<Keyboard>/f`) in `PlayerInputActions.inputactions`, so it cannot drift silently. There is a single Interact action; per-action keys are not implemented because they would need input and design decisions. The prompt (`InteractionPrompt` in `LocalGameplayHud.prefab`) is centered above the action bar and the ability messages.

## Raid Pause & Defeat Overlay (`RaidMenuPresenter` / `RaidMenuView`)

`RaidMenuPresenter` and `RaidMenuView` manage the local pause and defeat UI overlay on `LocalGameplayHud`.
While the participant is `Raiding`, `LocalPlayerHudBinder` owns that composition only when the
linked participant has local Input Authority and identifies this Input Authority avatar through
`CurrentAvatarId`. A link that has not resolved its participant yet leaves the HUD unbound and is
retried during `Render`; only the direct-development composition without a link falls back to the
avatar's Input Authority.

After the local participant becomes `Defeated`, terminal HUD ownership remains with that
`NetworkRaidParticipant`. The avatar can have no Input Authority and the participant can have no
`CurrentAvatarId`, while the same local composition remains bound long enough to present the result
and role-appropriate actions. A remote defeated participant never activates local HUD. This presentation rule
does not restore movement, combat, interaction, loot or consumable input and does not make the body
controllable again. The minimap retains its existing presentation behavior. The spectator controller
may retarget `LocalCameraController`, but never rebinds HUD or authority to the observed avatar.

- **Input Suppression**: Opening the menu acquires a local gameplay input suppression token (`PlayerInputReader.AcquireGameplayInputSuppression`), preventing player movement and attack actions while navigating the menu overlay.
- **Pause State (Living Player)**: Activated by pressing `Escape` / `Cancel` action (`MenuToggleRequested`). Displays basic control bindings and allows resuming gameplay or abandoning the raid.
- **Defeat State (Defeated Player)**: The participant's authoritative `Defeated` state retains input suppression. A Client result offers `Observar` and `Volver al pueblo`; a Host enters spectator automatically and has no Return, Abandon or Cancel Raid action. Character health is only the direct-development fallback when no participant exists.
- **Session Abandonment and Return**: A living player preserves the existing authoritative abandonment contract. A defeated Client sends one `NetworkRaidParticipant.RequestReturn()` intent; State Authority records the generation-scoped Controlled Return before setting `IsReturnAuthorized`. Views never shut down runners or load scenes directly.


## Ability HUD (`RaidAbilityHudPresenter` / `RaidAbilityHudView`)

The two universal ability slots (Q / E) are projected by `RaidAbilityHud`, a bottom-centered root beside `RaidCooldownHud` on `LocalGameplayHud`. It is presentation-only: no `[Networked]` field, no RPC and no simulation input are added, and it reflects **confirmed** state only.

```text
LocalPlayerHudBinder
  -> RaidAbilityHudPresenter.Bind(PlayerAbilityRuntimeNetworkController,
                                  PlayerManaNetworkController, PlayerStaminaNetworkController)
      -> RaidAbilityHudView -> RaidAbilityHudSlotView x2
```

**Sources of truth.**

| Element | Source |
| --- | --- |
| Slot empty / prepared, icon, key label | `TryGetSlot` -> `AbilityDefinition.Icon`; `TownAbilitySlotKeyLabels` |
| Preparing / Executing | `AbilityExecutionSnapshot.Phase` from `TryGetExecutionSnapshot` |
| Cooldown fill and seconds | `IsOnCooldown`, `GetRemainingCooldownSeconds`, total from `AbilityDefinition.CooldownSeconds` |
| Insufficient resource | `PlayerManaNetworkController.CurrentMana` / `PlayerStaminaNetworkController.CurrentStamina` against `AbilityDefinition.Cost`; an unknown balance is never flagged |

`RaidAbilityHudModelBuilder` is the pure, Fusion-free projection from slot facts to `RaidAbilityHudSlotModel` (Empty, Ready, OnCooldown, Preparing, Executing plus the insufficient-resource flag). Cooldown fill is the normalized remaining time and is zero for an invalid total; seconds are rounded up to tenths like the weapon cooldown. The presenter reads through the internal `IRaidAbilityHudSource` seam (adapter `RaidAbilityHudControllerSource`), which exists only so the lifecycle is testable without a runner.

**Binding and baseline rule.** `Bind` always calls `Unbind` first, subscribes `ActivationRejected` / `ExecutionInterrupted` (raised only on the owning Input Authority peer) and owns one `AbilityFeedbackTracker` per slot. On bind and on every re-enable it calls `Baseline` with the *current* confirmed `(Sequence, Phase)`; when the runtime is unconfirmed it resets the tracker so the next sample is adopted silently. Restore, Host Migration, rebind and reactivation therefore never replay a start cue for an execution that was already running. `OnDisable` unsubscribes, clears observed state and empties the view; `OnEnable` resubscribes once.

**Cues.**

- A `Started` edge from a *confirmed* new sequence plays the started cue. This is the only success cue.
- A rejection shows a short Spanish message keyed by `AbilityActivationFailure` (cooldown, insufficient resource, requirements, already executing, behaviour/plan rejected, aim unavailable, generic otherwise) with a brief red flash and shake. It never touches the trackers, so a rejection can never look like a started execution.
- An interruption (every stop except completion and participation teardown, per `AbilityFeedbackPolicy`) shows a short "Interrumpida" message.
- Messages expire on the presenter's unscaled clock; the HUD owns no gameplay clock.

World VFX and audio for abilities are not part of this HUD.

## Known limitations and open decisions

- Enemy health bars are not in the HUD (needs a design decision).
- Level and XP are not shown in Raid.
- No universal Raid timer: the Game Design says it is not universal; only the Dungeon Pressure timer exists.
- Progress is individual (MVP). Whether and how a team progress is shown is undecided.
- Expedition progress is a horizontal bar; the concept image shows a ring, which would need a dedicated sprite.
- Quick Slots and weapon sets A/B are not implemented.
- `RaidMenuView` still carries a static `F — Interactuar` help line, independent of `InteractionPromptText`.
- Layout and visuals of the rework (right panel, bottom row, vitals, prompt position) are pending validation in the Game view and Play Mode.

## Alternatives not selected

- A loot-value calculator or projection object would duplicate existing public behavior and add no variation point.
- A HUD-specific timer would compete with Fusion's replicated `TickTimer`.
- A separate label catalog would duplicate values already exposed by the gameplay sources.
- A second binder, Canvas, HUD prefab, or global presentation manager would duplicate the established local-player composition.

## Validation strategy

EditMode tests cover the Dungeon Pressure binding and `--:--` fallback (`DungeonPressureHudPresenterTests`), the right panel composition (`RaidRightPanelPrefabTests`), the bottom row and vitals geometry (`RaidBottomBarLayoutTests`), removal of dead labels (`RaidMainHudPrefabCleanupTests`), the prompt key label against the real binding (`InteractionPromptTextTests`), equipped-weapon icon resolution and clearing, safe cooldown normalization, extraction snapshot mapping, one-shot cancellation presentation, missing-source placeholders, and duplicate view writes.

PlayMode tests use the existing Single Runner style to cover prefab composition, serialized references, initial and clear values, unresolved participant links, local and remote ownership, equipped-icon changes, combat status during and after cooldown, read-only combat queries, loot-value failure and recovery, bind/disable/re-enable cleanup, listener uniqueness, and local participant defeat without hiding the HUD after avatar authority is removed.

Teammate HUD EditMode coverage includes frozen Solo/Duo resolution, ambiguous membership,
sanitization, terminal/cache projections and dirty writes. Single Runner PlayMode covers prefab
composition, local teammate resolution, Health observation, placeholder/lifecycle behavior and
terminal defeat. It does not prove Host/Client isolation, disconnect handling or Host Migration.

Manual validation remains necessary for:

- real Host/Client isolation and observed replication of health and cooldown;
- teammate placeholder while the participant/avatar is absent and re-resolution when an already
  supported lifecycle such as Host Migration/rebind materializes the same `ProfileId`;
- complete session restart;
- layout, anchors, contrast, radial fill, target resolutions, and coexistence of the right panel, bottom row and interaction prompt (the bottom bar and vitals block were observed in a local Play Mode preview harness with sample data, never in a real match);
- defeat, loot collection/transfer, equipped weapon icons, local extraction countdown/cancellation/completion, and extracted-player presentation in the actual game flow.

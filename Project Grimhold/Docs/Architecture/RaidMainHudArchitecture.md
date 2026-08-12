# Raid Main HUD Architecture

## Context

TASK-39 adds the always-available raid summary for the local player. TASK-29 connects its extraction section to the existing confirmed extraction query. The HUD remains presentation-only: it does not own health, combat, loot, class-selection, or extraction state, and it introduces no replicated fields.

The HUD is composed in `NetworkPlayer.prefab` under the existing `LocalGameplayHud` Canvas. `NetworkPlayerMelee.prefab` and `NetworkPlayerRanged.prefab` inherit that composition from the base prefab.

## Decision and component flow

```text
Input Authority NetworkPlayer
  -> LocalPlayerHudBinder
      -> RaidHudPresenter
          -> RaidHudView
```

- `LocalPlayerHudBinder` remains the only local HUD binding boundary. A player without Input Authority keeps `LocalGameplayHud` inactive.
- `RaidHudPresenter` is a local `MonoBehaviour`. It caches gameplay references, reads them without side effects, performs section-level dirty checking, and owns no clock.
- `RaidHudView` contains only uGUI/TMP references and explicit presentation or clearing operations.
- `RaidMainHud` is a non-interactive visual root and a sibling of `RaidInventoryScreen`. The presenter and view remain on `LocalGameplayHud`, outside the visual root they control.
- `RaidCooldownHud` is a bottom-centered visual root on the same Canvas. Each player variant supplies its existing weapon sprite as icon; a dark radial image and a compact decimal-seconds label render replicated cooldown progress.

No additional Canvas, HUD prefab, global manager, service locator, event bus, or per-frame component search is used.

## Sources of truth

| HUD section | Source |
| --- | --- |
| Current health | `CharacterBase.Health` |
| Maximum health | `CharacterBase.MaxHealth` |
| Defeat | `!CharacterBase.IsAlive` |
| Attack availability and cooldown | `PlayerCombatNetworkController.TryGetPrimaryAttackStatus` |
| Selected class | runner-scoped `LocalPlayerJoinContext.JoinData.ClassId` |
| Occupied slots and capacity | `PlayerLootReceiver.OccupiedSlotCount` and `SlotCapacity` |
| Loot value inside the inventory screen | `PlayerLootReceiver.TryCalculateTotalValue` |
| Extraction | local `PlayerExtractionController.TryGetProgress` |
| Individual extraction progress | local `PlayerExtractionProgressController.TryGetSnapshot` |
| Sanctuary assignment and ritual | runner `ExtractionSanctuaryAssignmentService`, `EntityRegistry`, `IExtractionSanctuary` |

`CharacterBase.MaxHealth` exposes the configured maximum as read-only data. It is not a second networked health value.

## Combat evaluation and authority

`PrimaryAttackStatus` is a small immutable presentation value containing availability, total cooldown duration, and remaining cooldown time.

`PlayerCombatNetworkController` owns one private, side-effect-free evaluation of the stable primary-attack prerequisites: valid runner and dependencies, attack enabled, living character, and expired or unstarted cooldown. Both authoritative execution in `FixedUpdateNetwork` and `TryGetPrimaryAttackStatus` use that evaluation. Input edges and aim direction stay in the execution flow and are not reported as persistent availability.

The State Authority check remains in the execution flow. It is deliberately absent from the read query so the Input Authority player can inspect replicated state. The query neither executes an attack nor changes `TickTimer`, and no new `[Networked]` state exists for the HUD.

The presenter obtains normalized cooldown fill from the reported duration and remaining time. Duration at or below zero, negative inputs, `NaN`, and infinity produce zero; otherwise the ratio is clamped to `[0, 1]`. The presenter never advances a local cooldown clock. The cooldown image uses uGUI radial fill and remains at its authored scale. An authoritative cooldown rejection pulses the local icon once; it does not modify the timer or show global text.

## Class resolution

During binding, `LocalPlayerHudBinder` caches its current runner and resolves `LocalPlayerJoinContext` through `runner.GetComponent`. It immediately reads `JoinData.ClassId`.

- `Melee` is presented as `Caballero`.
- `Ranged` is presented as `Mago`.
- `None` and unknown values remain `Clase: —`.

If the cached context initially contains `None`, the binder rereads only `JoinData.ClassId` during `Render` until a supported class appears, then stops. `RaidHudPresenter.SetPlayerClass` is idempotent and resolves only the first supported class in a binding.

If the runner lacks the context component, the binder reports that configuration once and performs no per-frame search. A later existing lifecycle notification may retry caching. Unbind resets the cached runner, context, resolution flags, and displayed class so a new session cannot inherit the previous selection.

## Inventory summary and value recovery

`RaidHudPresenter` displays only occupied slots and capacity in the always-visible summary. The complete loot value belongs to the existing personal panel inside `RaidInventoryScreen`; no duplicate value is rendered in `RaidMainHud`.

`RaidInventoryPresenter` calls `PlayerLootReceiver.TryCalculateTotalValue` when it refreshes the player panel. A failed complete-value read displays `Valor: —`, keeps only the value refresh pending, and retries on subsequent presentation updates without rebuilding slots, using a timer, coroutine, or inventory subtotal. One diagnostic is emitted per failed episode. Once a complete read succeeds, the presenter stops recalculating until `LootChangeSequence` changes.

## Dirty checking

Health/defeat, attack/cooldown, and inventory capacity maintain independent observed state. The presenter writes a section only when its visible state changes. The view additionally avoids assigning identical TMP text, fill, scale, active-state, or root-state values.

Attack status may be queried each presentation frame so enablement, defeat, and timer expiry are observed. Loot value is not recalculated each frame after a successful read.

## Lifecycle

`OnEnable` before binding is valid and has no effect. Bind starts from an idempotent unbind, clears placeholders, caches the current sources, and requests initial reads.

`Unbind`, Fusion despawn, destroy, and session replacement remove the `LocalInputContext.ReaderChanged` listener, clear cached dependencies, feedback and late-resolution state, clear the presenter/view, and deactivate `LocalGameplayHud`. Disabling `RaidHudPresenter` itself clears visual observations and baselines but keeps its sources so re-enable can rebuild without a second binder subscription. Re-enable performs a fresh bind only for a valid player with Input Authority. Defeat keeps the persistent HUD visible but immediately clears transient combat feedback.

## Extraction presentation

`LocalPlayerHudBinder` passes the local `PlayerExtractionController` into `RaidHudPresenter`. The presenter baseline observes the first valid `ExtractionCountdownSnapshot`, so joining during an active countdown or after completion does not emit a false transition. This countdown contract was renamed from `ExtractionProgressSnapshot` in US-13 so the latter can exclusively describe individual quota progress. A valid `InProgress` snapshot displays the sanitized remaining duration, `InProgress -> None` displays one cancellation message for the configured presentation duration, and `Extracted` displays a persistent terminal label. An invalid or unavailable read clears the observation baseline and shows the unavailable placeholder without fabricating a cancellation or completion.

TASK-56 adds no team progress projection. `ExtractionProgressSnapshot` is presented as the local individual quota text, while assignment and ritual text are derived from the runner-scoped assignment service, registry and Sanctuary snapshot. Pickups and inventory economic text use `SellValuePerUnit`; `ExtractionValuePerUnit` is never displayed as currency.

The extraction HUD section never writes player state, calls an extraction command, or uses a parallel local countdown. The local HUD remains available after `Extracted`; authoritative interaction, damage and loot protocols continue to enforce the existing terminal restrictions.

TASK-56 extends the binding with nullable presentation sources for individual progress,
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

TASK-68 adds the local minimap as a separate `RaidMinimapPresenter`/`RaidMinimapView` section
bound by `LocalPlayerHudBinder`; `RaidHudPresenter` retains responsibility only for textual HUD
content. `MinimapLayoutGenerator` derives the immutable `MinimapLayout` asset from the serialized
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


## Alternatives not selected

- A loot-value calculator or projection object would duplicate existing public behavior and add no variation point.
- A HUD-specific timer would compete with Fusion's replicated `TickTimer`.
- A class catalog created only for two labels would add unnecessary configuration.
- A second binder, Canvas, HUD prefab, or global presentation manager would duplicate the established local-player composition.

## Validation strategy

EditMode tests cover class mapping and late resolution through presenter behavior, clearing between bindings, safe cooldown normalization, extraction snapshot mapping, one-shot cancellation presentation, missing-source placeholders, and duplicate view writes.

PlayMode tests use the existing Single Runner style to cover prefab composition, serialized references, initial and clear values, unresolved participant links, local and remote ownership, late class resolution, combat status during and after cooldown, read-only combat queries, loot-value failure and recovery, bind/disable/re-enable cleanup, listener uniqueness, and local participant defeat without hiding the HUD after avatar authority is removed.

Manual validation remains necessary for:

- real Host/Client isolation and observed replication of health and cooldown;
- complete session restart;
- layout, anchors, contrast, radial fill, target resolutions, and coexistence with inventory and the interaction prompt;
- defeat, loot collection/transfer, visible class labels, local extraction countdown/cancellation/completion, and extracted-player presentation in the actual game flow.

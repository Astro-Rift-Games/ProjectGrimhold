# Treasure Passive Modifiers and Utility Cooldown Persistence (US-76)

Objective: Complete runtime and presentation integration for MVP passive treasures (`TRE_PILGRIM_LIGHT`, `TRE_VOID_BELL`, `TRE_WANDERER_EYE`) and preserve active expedition utility cooldowns (`UTL_RUNE_COMPASS`, `UTL_ARCANE_DETECTOR`) across container deposits, withdrawals and corpse generation so cooldowns cannot be bypassed.

Baseline: `New-Testing` `b3d1765a`, branch `feat/us-76-treasure-passives-cooldowns` (3 pre-existing unrelated modified assets left untouched: `alagard SDF.asset`, `DungeonGrayboxMinimapLayout.asset`, `LiberationSans SDF - Fallback.asset`).

Sources:
- HacknPlan `US-76` (`#656`): *Integración de Efectos Pasivos de Tesoros y Retención de Cooldowns*
  - `TASK-264` (`#657`): *Integración de modificadores pasivos de Tesoros (Luz del Peregrino, Campana del Vacío, Ojo del Errante)*
  - `TASK-265` (`#658`): *Persistencia de Cooldowns de Utilidades al depositar y retirar en Contenedores*
- Architecture: `Docs/Architecture/VisibilityArchitecture.md`, `Docs/Architecture/LootInteractionArchitecture.md`, `Docs/Architecture/HostMigrationRecoveryArchitecture.md`.
- Game Design note: `grimhold-docs` MCP returned `invalid_grant`; Doc 14 §10/§13 parameters were verified against HacknPlan `#656/#657/#658` and Sprint 15 assets/code (`TRE_PILGRIM_LIGHT` `1.35x` stamina regen, `TRE_VOID_BELL` `1.15x` sprint speed + `2.0s` sprint chime interval + `15m` audible/aggro radius, `TRE_WANDERER_EYE` `0.75x` vision radius + minimap block).

Acceptance criteria (quoted from `#656`, `#657`, `#658`):
- `TRE_PILGRIM_LIGHT`: +35% Stamina regeneration while carrying >= 1 unit; binary non-stacking; reverts to 1.0x immediately on drop, container deposit or death.
- `TRE_VOID_BELL`: +15% sprint speed while carrying >= 1 unit; authoritative tick-driven chime every 2.0s while sprinting (`IsSprinting && IsMoving`) alerting `IAggroReceiver` enemies within 15m and replicating chime sequence so peers within 15m present audio and provisional ring VFX; binary non-stacking; stops immediately on drop, deposit or death.
- `TRE_WANDERER_EYE`: -25% (`0.75x`) local vision radius (`NearRadius`, far fog distances and avatar `Light2D` radii without mutating ScriptableObjects) and minimap blocked via `RaidMinimapPresenter` / `RaidMinimapView`; restores immediately on drop, deposit or death.
- `TASK-265`: Depositing a utility with active `TickTimer` cooldown (`UTL_RUNE_COMPASS` / `UTL_ARCANE_DETECTOR`) into `NetworkLootContainer` (or converting inventory to corpse container on death) transfers the `TickTimer` to the container and clears the player's slot timer; withdrawing while still running restores the remaining `TickTimer` on the receiving player (same player or party/other player) and clears it from the container once empty; expired timers allow immediate activation.

Exclusions:
- Lockpick (`TASK-235`) and Self-Revive (`TASK-236`).
- Non-MVP treasures or base balance changes to Stamina, AI or utility durations.
- Town persistent Stash transfers.

Tasks:
- [x] T1 Authoritative & presentation passive treasure integration (`PlayerInventoryPassiveEffectsController`, `PlayerCharacter`, `PlayerStaminaNetworkController`, `AudioManager`, `VisionQuery`, `LocalVisionSource`, `VisionFogPresenter`).
- [x] T2 Container utility cooldown retention (`NetworkLootContainer`, `LootTransferTransaction`, `PlayerCorpseGenerationController`).
- [x] T3 EditMode & PlayMode tests (`InventoryPassiveEffectsTests`, `UtilityExpeditionRulesTests`, `UtilityExpeditionPlayModeTests`) + architecture contract sync (`VisibilityArchitecture.md`, `LootInteractionArchitecture.md`).
- [x] T4 Runtime catalog validation fix (`LootDefinition.TryValidate` gating `_equipmentVisualDefinition` to armor categories so non-armor `CON_*`/`RES_*`/`TRE_*`/`UTL_*` definitions do not break `LootDefinitionCatalog.TryValidate`, drops and container initialization) + EditMode/PlayMode test stabilization; restored locked `RaidLootOriginWordCountTests` budgets per `LootInteractionArchitecture.md`.

Validation & status:
- `dotnet build "Project Grimhold/Assembly-CSharp.csproj"` -> 0 errors (zero warnings in modified files).
- `dotnet build "Project Grimhold/Assembly-CSharp-Editor.csproj"` -> 0 errors.
- `RaidLootOriginWordCountTests` retains the locked `New-Testing` expectations/ceilings (`LootContainer` and `NetworkEnemy` have a pre-existing documented discrepancy in `LootInteractionArchitecture.md` lines 236–238).
- Unity MCP is not installed in this environment; re-run `InventoryPassiveEffectsTests` and `UtilityExpeditionPlayModeTests` in the Unity Test Runner plus Host/Client runtime validation.

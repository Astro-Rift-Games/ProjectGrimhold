# 2026-11 — Unreleased history

[Archive index](README.md)

Release: Unreleased

7 original commits; 7 canonical entries.

## Canonical classification

6 features; 1 other changes

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Abilities | 0 | 0 | 0 | 0 | 0 | 1 |
| Art & Animation | 3 | 0 | 0 | 0 | 0 | 0 |
| Audio | 2 | 0 | 0 | 0 | 0 | 0 |
| Build & Tooling | 6 | 0 | 0 | 0 | 0 | 1 |
| Combat | 5 | 0 | 0 | 0 | 0 | 0 |
| Documentation | 6 | 0 | 0 | 0 | 0 | 0 |
| Input | 1 | 0 | 0 | 0 | 0 | 0 |
| Inventory & Loot | 2 | 0 | 0 | 0 | 0 | 0 |
| Movement | 1 | 0 | 0 | 0 | 0 | 0 |
| Networking & Sessions | 2 | 0 | 0 | 0 | 0 | 0 |
| Tests | 6 | 0 | 0 | 0 | 0 | 0 |
| UI | 5 | 0 | 0 | 0 | 0 | 0 |
| World & Visibility | 1 | 0 | 0 | 0 | 0 | 0 |

## Original commit records

### f2062e7cc462fb019b9017ef5e8f52366ded3f63

[f2062e7cc462fb019b9017ef5e8f52366ded3f63](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f2062e7cc462fb019b9017ef5e8f52366ded3f63)

Date: 2026-11-02T12:39:44-03:00 · Classification: Other Changes (unknown) · Systems: Abilities, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Align task workflow with ODD execution
```

#### Original body

```text
Update repository guidance to use ODD classification and execution trackers for substantial tasks, while preserving HacknPlan as the scope source of truth. Refresh subagent routing and Unity operator model settings.
```

#### Changed paths

```text
.agents/skills/grimhold-task-execution/SKILL.md
.agents/skills/grimhold-task-execution/references/tool-routing.md
.agents/skills/hacknplan-task-design/references/workflow.md
.pi/subagents.json
AGENTS.md
```

#### Classification evidence

- path → Abilities: .agents/skills/grimhold-task-execution/SKILL.md
- path → Abilities: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Abilities: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/SKILL.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: AGENTS.md

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### cdd4716869d658b437f2c1c738cefbe2b2809e90

[cdd4716869d658b437f2c1c738cefbe2b2809e90](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/cdd4716869d658b437f2c1c738cefbe2b2809e90)

Date: 2026-11-01T21:38:06-03:00 · Classification: Features (explicit) · Systems: Combat, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(combat): credit knockback pushers for hazard-caused Downed
```

#### Original body

```text
Track indirect responsibility when a knockback moves a player into an
environmental hazard (GD 18 6.5, GD 19 8). If the hazard's damage
causes Active -> Downed while the causal chain holds, the enemy pusher
receives the provisional PvP Last Hit; the direct damage source stays
the environment (DownedCauserEntityId = 0). Ally pushes are recorded but
never credited, and recent enemy damage does not inherit the credit.

Causal register contract (GD 19 10 left it to implementation):
- PlayerHazardExposureCausalityNetworkController on NetworkPlayer holds
  a replicated HazardExposureCausalityState mutated on State Authority
  through pure HazardExposureCausalityRules.
- DamageResolver records the player pusher of each Started/Replaced
  knockback; non-player displacements discard the pending cause.
- Hazards report presence every authority tick (BaseTrap.OnAuthorityTick,
  SpikesTrap) and tag damage with DamageRequest.EnvironmentalHazardId.
- The link persists while exposure continues, with one tick of ordering
  grace; it breaks on exit, on a later push into another hazard, and
  while Downed/defeated. Breaks are audited with hazard, pusher,
  knockback sequence, reason and tick.
- TryEnterDowned(attackerId, creditCauserId) separates the direct source
  from the credit causer passed to IDownedCycleObserver.

Tests:
- HazardExposureCausalityRulesTests: 13 EditMode cases for GD 19 T06
  continuity, rupture, voluntary entry, overlapping hazards and resets.
- PvpCombatCreditPlayModeTests: T06-01, T06-04/05 and T06-09 end to end.

Docs: DownedAndReviveArchitecture 11.1, EnvironmentalHazardsArchitecture,
PlayerCombatArchitecture.

Player-Added: Knocking an enemy into a trap now counts as your knockdown when the trap's damage takes them down.
Refs: TASK-247
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs.meta
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs
Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
Project Grimhold/Assets/Scripts/Core/IDownedCycleObserver.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerDownedStateNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerHazardExposureCausalityNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerHazardExposureCausalityNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardCausalityBreakReason.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityRules.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/HazardCausality/HazardExposureCausalityState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- scope → Combat: combat
- message-term → Combat: combat
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/HazardExposureCausalityRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PvpCombatCreditPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 07f13443b31d2faa85f2c3848c926cdb9056a71e

[07f13443b31d2faa85f2c3848c926cdb9056a71e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/07f13443b31d2faa85f2c3848c926cdb9056a71e)

Date: 2026-11-01T02:06:11-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Networking & Sessions, UI, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add lockpicking and locked chest spawns
```

#### Original body

```text
Implement deterministic lockpick channels and replicated chest locks for
TASK-235. Movement and committed damage cancel without consumption;
success spends one lockpick using existing inventory provenance.

Add the static LockedChest prefab at a 10% chance per existing chest
point. Guarantee the first locked chest for Editor/development testing
without changing release probabilities or total chest counts.

Preserve unlocked state across restoration and safely cancel restored
channels whose target identities have not been remapped. Persist the
accepted existing ambient-particle scene cleanup and configuration.

Cover channel interruption, inventory spending, prefab configuration,
spawn selection and legacy behavior; document the interaction flow.
Latest focused checks: 38/38 EditMode and 19/19 PlayMode passed.
Real Host/Client, migration and manual visual checks remain pending.

Refs: TASK-235
Player-Added: Locked chests can be opened with a lockpick after a short uninterrupted interaction.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/LockedChest.png
Project Grimhold/Assets/Art/Props/LockedChest.png.meta
Project Grimhold/Assets/Prefabs/LockedChest.prefab
Project Grimhold/Assets/Prefabs/LockedChest.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs
Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LockpickChannelRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LockedChestSpawnRules.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/LockedChest.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/LockedChest.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Interaction/LockpickChannelRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedChestSpawnRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LockedLootContainerTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockpickInteractionPlayModeTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 15e80657787f2601c3828703ae06c9b189a6a417

[15e80657787f2601c3828703ae06c9b189a6a417](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/15e80657787f2601c3828703ae06c9b189a6a417)

Date: 2026-10-31T22:22:59-03:00 · Classification: Features (explicit) · Systems: Combat, Movement, Inventory & Loot, Networking & Sessions, UI, Input, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(revive): add self-revive consumable and tap-to-hand-off flow
```

#### Original body

```text
Implement TASK-236 (GD 13 sections 7, 7.1, 8, 10; GD 14 section 11.4).
A Downed player carrying CON_SELF_REVIVE presses Interact to start an
authoritative 5 s Self-revive channel that locks voluntary movement and
pauses the Downed drain. Damage, a displacement that still moves the
avatar or definitive Defeat interrupts it without consuming the item;
disconnect does not. Completion consumes one unit (Town origin first),
restores 25 HP and spends the one Self-revive allowed per expedition.

An Active teammate taps Interact on a Downed ally to hand over their
Self-reviver and holds Interact to revive. The press still opens an
assisted session; a release within 0.3 s converts it into a 1 s handoff
when the giver carries the item and the ally can receive and use it.
The handoff does not pause the drain, transfers the unit atomically
without a world drop and starts the Self-revive automatically.

Reviewers should verify:
- RecoveryKind.Handoff and DownedRecoveryInterruptReason.HandoffRejected
  are appended, so existing byte values are unchanged.
- New networked SelfReviveUsed and LastOutcomeSessionKind are reset in
  Spawned except on Host Migration restore.
- Provisional balance values (25 HP, 5 s, 1 s, 0.3 s) are serialized on
  PlayerDownedRecoveryNetworkController.
- CON_SELF_REVIVE is no longer Quick Slot compatible; stored bindings
  are dropped on load by the existing codec rule.
- PlayerInputReader now transports a held Interact from an accepted
  press until release. Previously only the one-tick press reached
  simulation, so a real hold was read as a release.
- The revive panel bar fill is assigned and active in
  NetworkPlayer.prefab and follows the same progress as the ring.

Tests:
- DownedSelfReviveRulesTests, DownedSelfReviveFeedbackTests (EditMode)
- PlayerSelfRevivePlayModeTests (completion, interruption, quota,
  movement lock, tap handoff, hold revive, rejections)
- PlayerInputReaderPlayModeTests: held Interact transport
- ModularHudFillTests: revive panel bar progress and prefab wiring
- MvpItemCatalogTests: CON_SELF_REVIVE is not Quick Slot compatible

Docs: DownedAndReviveArchitecture section 7.1 and status tables;
RaidMainHudArchitecture revive panel bar note.

Player-Added: Carry a Self-reviver to get back up on your own while downed, or tap the interact key on a downed ally to give them yours.
Player-Fixed: Holding the interact key on a downed ally now keeps reviving them instead of stopping right away, and the revive bar now fills up.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_SELF_REVIVE.asset
Project Grimhold/Assets/Scripts/Player/DownedRecoveryFeedbackRules.cs
Project Grimhold/Assets/Scripts/Player/DownedRecoveryInterruptReason.cs
Project Grimhold/Assets/Scripts/Player/DownedRecoveryRules.cs
Project Grimhold/Assets/Scripts/Player/Interaction/DownedReviveInteractable.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerDownedRecoveryNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudView.cs
Project Grimhold/Assets/Scripts/Player/RecoveryKind.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: Damage
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- message-term → Movement: movement
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_SELF\_REVIVE.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/DownedReviveInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- message-term → Networking &amp; Sessions: Host Migration
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidDownedHudView.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveFeedbackTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/DownedSelfReviveRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/ModularHudFillTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerSelfRevivePlayModeTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 0200410152cc382ff8f2fc92db3fc0af77c3c813

[0200410152cc382ff8f2fc92db3fc0af77c3c813](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0200410152cc382ff8f2fc92db3fc0af77c3c813)

Date: 2026-11-01T00:40:00-03:00 · Classification: Features (explicit) · Systems: Combat, UI, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): add peripheral low-health blur and color split
```

#### Original body

```text
Sample the completed scene below the agony health threshold using a camera-local URP RenderGraph fullscreen effect. Preserve the clear center, avatar, cursor and overlay HUD, and release runtime materials when presentation ends.

Add configurable sampling radii, owner-scoped teardown and GPU regression tests for protected pixels, Renderer2D execution and exact baseline restoration. The focused presentation/audio/settings suite passed 104 tests; real multiplayer and visual calibration remain manual.

Player-Added: At critically low health, subtle blur and color separation at the screen edges reinforce the danger without obscuring your character or aim.

Refs: TASK-253
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
Project Grimhold/Assets/Prefabs/Gameplay Camera.prefab
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs.meta
Project Grimhold/Assets/Settings/Renderer2D.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
- path → Combat: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageCameraEffect.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageRendererFeature.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.mat.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Shaders/ScreenDamagePeripheral.shader.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamagePeripheralTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 21c13f917a6eb230ab66ffb438ad439e2be3938d

[21c13f917a6eb230ab66ffb438ad439e2be3938d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/21c13f917a6eb230ab66ffb438ad439e2be3938d)

Date: 2026-11-01T00:23:20-03:00 · Classification: Features (explicit) · Systems: Combat, UI, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): add progressive local screen damage spray
```

#### Original body

```text
Bind translucent screen damage to confirmed local health with smooth severity, heartbeat, hit flash and an accessibility intensity control. Preserve the clear center and cover viewport edges and corners with the v05 pixel spray.

Include serialized HUD/configuration assets and EditMode health, lifecycle, mask and settings tests. Presentation/audio/settings regression suite: 95 passed. Peripheral scene blur and chromatic aberration remain for the next implementation slice.

Refs: TASK-253
```

#### Changed paths

```text
Project Grimhold/Assets/Art/UI/ScreenDamage.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
Project Grimhold/Assets/Prefabs/UI/SettingsPanel.prefab
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset.meta
Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs.meta
Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/ScreenDamageConfig.asset.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/IScreenDamageSource.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerScreenDamagePresenter.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerScreenDamageSource.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageAccessibility.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageConfig.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageFeedbackState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/ScreenDamageView.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
- path → Combat: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- message-term → Combat: damage
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/SettingsPanel.prefab
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs
- path → UI: Project Grimhold/Assets/Scripts/UI/Settings/ScreenDamageOptionsView.cs.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-00.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-01.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-02.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-03.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-04.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-05.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-06.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-07.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-08.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-09.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-10.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-11.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-12.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/ScreenDamageTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c

[735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/735417b34141b2eb9b37e8bb5c5eb0a87cdc3f3c)

Date: 2026-10-31T23:42:18-03:00 · Classification: Features (explicit) · Systems: Combat, UI, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(presentation): fade the local view to black and white while Downed
```

#### Original body

```text
When the local player enters Downed the screen now blends to a
balanced-contrast black and white with a soft vignette, stays that way
while crawling or waiting for revive, and blends back to full colour on
revive or definitive Defeat. Only the Input Authority avatar's HUD
binds it, so proxies and other peers keep their normal camera, and the
Screen Space Overlay HUD is never desaturated.

- LocalPlayerDownedPostProcessPresenter owns a runtime global Volume
  (ColorAdjustments + Vignette, priority above the dungeon ambient
  Volume) with its own profile. It exists only while the effect is
  visible, is a child of the presenter (never DontSave) and is released
  on fade-out, unbind, disable or avatar loss. No renderer feature,
  shader or shared Volume asset is modified.
- DownedPostProcessBlend (pure): fade-in 0.4 s (config range 0.3-0.5),
  fade-out 0.4 s through Volume weight; vignette and dimming deepen as
  the Downed reserve drains (eased so damage chunks never pop); an
  assisted or self revive session gradually recovers part of the
  colour. Reserve and revive values are latched when Downed ends.
- DownedPostProcessConfig ScriptableObject (blend curve, timings,
  saturation -100, contrast, vignette, reserve and revive modulation)
  and its asset, wired on NetworkPlayer RaidDownedHud and bound by
  LocalPlayerHudBinder.
- Reads IsDowned, DownedHealth and TryGetProgress only; no networked
  state is added or written.

Tests: DownedPostProcessBlendTests, LocalPlayerDownedPostProcessPresenterTests
(including a regression that the Volume cannot outlive its presenter).

Docs: DownedAndReviveArchitecture section 9.1 and status table.

Refs: TASK-254
Player-Added: Your screen fades to black and white while you are downed and regains its colour as you are revived.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset
Project Grimhold/Assets/Scriptable Objects/DownedPostProcessConfig.asset.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessBlend.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs
Project Grimhold/Assets/Scripts/Player/Presentation/DownedPostProcessFrame.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/IDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerDownedPostProcessPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDownedPostProcessSource.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: damage
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: HUD
- message-term → World &amp; Visibility: dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/DownedAndReviveArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/DownedPostProcessBlendTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Presentation/LocalPlayerDownedPostProcessPresenterTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.


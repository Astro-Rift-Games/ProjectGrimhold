# 2026-07 — Unreleased history

[Archive index](README.md)

Release: Unreleased

83 original commits; 83 canonical entries.

## Canonical classification

47 features; 2 refactoring; 1 architecture & infrastructure; 5 documentation; 3 tests; 25 other changes

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Abilities | 3 | 0 | 0 | 0 | 0 | 0 |
| Art & Animation | 14 | 0 | 0 | 0 | 0 | 4 |
| Audio | 0 | 0 | 0 | 0 | 0 | 1 |
| Build & Tooling | 25 | 0 | 0 | 1 | 1 | 10 |
| Combat | 25 | 0 | 0 | 0 | 5 | 7 |
| Documentation | 24 | 0 | 0 | 0 | 5 | 6 |
| Extraction | 3 | 0 | 0 | 0 | 0 | 1 |
| Input | 8 | 0 | 0 | 1 | 1 | 4 |
| Inventory & Loot | 18 | 0 | 1 | 0 | 3 | 4 |
| Movement | 11 | 0 | 1 | 0 | 1 | 5 |
| Networking & Sessions | 17 | 0 | 1 | 0 | 3 | 9 |
| Persistence & Backend | 3 | 0 | 0 | 1 | 0 | 2 |
| Tests | 20 | 0 | 0 | 0 | 3 | 8 |
| UI | 11 | 0 | 1 | 0 | 0 | 9 |
| Unassigned | 1 | 0 | 0 | 0 | 0 | 3 |
| World & Visibility | 5 | 0 | 0 | 0 | 0 | 3 |

## Original commit records

### 0cb931f5e9c4fc631a1a53982c223000f912dc94

[0cb931f5e9c4fc631a1a53982c223000f912dc94](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0cb931f5e9c4fc631a1a53982c223000f912dc94)

Date: 2026-07-30T22:04:26-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
add full-body damage hitboxes to characters
```

#### Original body

```text
Add dedicated trigger hitboxes covering the full body of player and enemy characters while preserving foot-level movement colliders.

Register both colliders under the same entity to support melee and projectile detection without duplicate damage. Add prefab composition and target-query tests, stabilize damage feedback testing, and document the collider responsibility split.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/Slime.meta
Project Grimhold/Assets/Placeholder/Slime/Attack.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Death/Death/Slime-Death-UpRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Idle/Idle/Slime-Idle-UpRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Slime/Walk/Walk/Slime-Walk-UpRight-Sheet.png.meta
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- subject-action → Features: add full-body damage hitboxes to characters
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Down-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Down-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownLeft-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownLeft-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownRight-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-DownRight-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Up-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-Up-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpLeft-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpLeft-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpRight-Sheet.png
- path → Combat: Project Grimhold/Assets/Placeholder/Slime/Attack/Attack/Slime-Attack-UpRight-Sheet.png.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: damage
- message-term → Movement: movement
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 2064394624b83128ca758479771a40fcd3396ad1

[2064394624b83128ca758479771a40fcd3396ad1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2064394624b83128ca758479771a40fcd3396ad1)

Date: 2026-07-30T21:40:53-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
add diegetic player feedback
```

#### Original body

```text
- show pooled damage numbers only for confirmed applied hits
- add compact weapon cooldown HUD with radial progress and rejection pulse
- animate pickup visuals locally when inventory is full
- show contextual capacity and interaction rejection feedback
- deduplicate presentation across resimulation and session lifecycle
- preserve existing damage, attack, defeat, health and loot presentation
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/LootPickup.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Core/CombatFeedbackKind.cs
Project Grimhold/Assets/Scripts/Core/CombatFeedbackKind.cs.meta
Project Grimhold/Assets/Scripts/Core/CombatPresentationEvent.cs
Project Grimhold/Assets/Scripts/Core/CombatPresentationEvent.cs.meta
Project Grimhold/Assets/Scripts/Core/IResolvedDamageFeedbackSink.cs
Project Grimhold/Assets/Scripts/Core/IResolvedDamageFeedbackSink.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/CombatFeedbackPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/CombatFeedbackPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootPickupRejectionPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootPickupRejectionPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- subject-action → Features: add diegetic player feedback
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/CombatFeedbackKind.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/CombatFeedbackKind.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/CombatPresentationEvent.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/CombatPresentationEvent.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/IResolvedDamageFeedbackSink.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/IResolvedDamageFeedbackSink.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/CombatFeedbackPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/CombatFeedbackPresenter.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: damage
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootPickupRejectionPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootPickupRejectionPresenter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: pickup
- message-term → Networking &amp; Sessions: session lifecycle
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/DamageResolverFeedbackTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootPickupRejectionPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### d42ba8c14c09d894de331885ef82dec0c486100a

[d42ba8c14c09d894de331885ef82dec0c486100a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d42ba8c14c09d894de331885ef82dec0c486100a)

Date: 2026-07-29T17:43:12-03:00 · Classification: Features (inferred) · Systems: Extraction, Inventory & Loot, UI, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Implement authoritative inventory world drops
```

#### Original body

```text
- add a provider-driven context menu for personal inventory slots
- support single-unit and full-stack authoritative drop requests
- publish provisional loot pickups only after inventory extraction commits
- add deterministic drop placement with WorldCollision validation
- preserve existing container transfer interactions
- wire the drop controller and context menu into player prefabs
- add EditMode and PlayMode coverage for drop and menu behavior
- document the inventory world drop architecture
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs
Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs.meta
Project Grimhold/Assets/Scripts/Core/LootDropResult.cs
Project Grimhold/Assets/Scripts/Core/LootDropResult.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropPlacementMath.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropPlacementMath.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestState.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ILootContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/ILootContextActionProvider.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionContext.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionContext.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionDescriptor.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionDescriptor.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionId.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionId.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextActionButton.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextActionButton.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSlotInteractionMode.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSlotInteractionMode.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- subject-action → Features: Implement authoritative inventory world drops
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootDropResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootDropResult.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropPlacementMath.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropPlacementMath.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionContext.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionContext.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionDescriptor.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionDescriptor.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionId.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootContextActionId.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextActionButton.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextActionButton.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSlotInteractionMode.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSlotInteractionMode.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: inventory
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootContextMenuView.cs.meta
- message-term → UI: menu
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropPlacementMathTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropRequestStateTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 34ef4fc4777fd22fb272fe1fd046bd4e1fa7decf

[34ef4fc4777fd22fb272fe1fd046bd4e1fa7decf](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/34ef4fc4777fd22fb272fe1fd046bd4e1fa7decf)

Date: 2026-07-29T17:01:59-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Add independent take-all loot transfers
```

#### Original body

```text
Add a local ordered take-all sequence that reuses the existing authoritative full-stack transfer pipeline.

Process visible container stacks independently, continue after individual failures, preserve successful transfers, refresh both panels between requests, and cancel unsent operations when the loot screen closes.

Add the serialized Take All button, contextual interaction states, architecture documentation, and coverage for ordering, inventory capacity, rejection continuation, and cancellation.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add independent take-all loot transfers
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootTakeAllStateTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### c9775e873bf9a9468cc0380bfb78e2e71ada1032

[c9775e873bf9a9468cc0380bfb78e2e71ada1032](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c9775e873bf9a9468cc0380bfb78e2e71ada1032)

Date: 2026-07-29T16:13:43-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, Networking & Sessions, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Add mouse-based partial loot transfers
```

#### Original body

```text
Add single-unit transfers on left click and authoritative full-stack transfers on right click for both withdrawals and deposits. Extend transfer identity, RPC validation, tests, debug tooling, and architecture documentation to support both quantity modes.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs.meta
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add mouse-based partial loot transfers
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferQuantityResolverTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### c34fcc3ef46e8c54b6433dd5640bfef85b72724b

[c34fcc3ef46e8c54b6433dd5640bfef85b72724b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c34fcc3ef46e8c54b6433dd5640bfef85b72724b)

Date: 2026-07-29T15:37:39-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, Networking & Sessions, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Add authoritative full-stack deposits to loot containers
```

#### Original body

```text
Support full-stack transfers from player inventories to open loot containers.
Validate authority, endpoint identity, range, availability, quantity, and capacity.
Preserve atomic transfers and duplicate-request protection in both directions.
Enable player inventory slot interactions with direction-aware rejection feedback.
Cover chests, defeated enemies, and defeated players with focused tests.
Update loot transfer architecture documentation.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add authoritative full-stack deposits to loot containers
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 274e5717fd29e80833643e5e1b8fe8d6089c7a2a

[274e5717fd29e80833643e5e1b8fe8d6089c7a2a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/274e5717fd29e80833643e5e1b8fe8d6089c7a2a)

Date: 2026-07-29T15:17:07-03:00 · Classification: Other Changes (unknown) · Systems: Inventory & Loot, Networking & Sessions, UI, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Restore authoritative full-stack loot transfers
```

#### Original body

```text
- Route inventory slot clicks through the authoritative full-stack transfer flow.
- Prevent duplicate requests and restore slot interactivity after confirmations or transport rejections.
- identify local Host RPC calls as the owning Input Authority player.
- Present contextual transfer failure feedback in the inventory screen.
- Cover chest, defeated enemy, and defeated player transfers with PlayMode tests.
- Document replicated loot sequences as the definitive source of inventory content.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 44724ca2ff8bc892370d5f78d71f83c6519886a6

[44724ca2ff8bc892370d5f78d71f83c6519886a6](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/44724ca2ff8bc892370d5f78d71f83c6519886a6)

Date: 2026-07-28T23:51:48-03:00 · Classification: Features (inferred) · Systems: Combat, Abilities, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
add shared player weapon presentation
```

#### Original body

```text
- Compose shared hand and weapon visuals from a torso-mounted orbit anchor.
- Reuse the same presenter and hierarchy across melee and ranged player variants.
- Keep weapon grip, stance, and angular corrections as per-variant serialized configuration.
- Preserve continuous facing, reflection, sorting, and pre-spawn Fusion safety.
- Keep hand and weapon visuals visible outside the defeat presentation.
- Add deterministic EditMode coverage and document the visual authoring flow.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/Character/Template-Hand.png
Project Grimhold/Assets/Placeholder/Character/Template-Hand.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/staves-16x16.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/swords-16x16.png.meta
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresentationMath.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresentationMath.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresenter.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs.meta
Project Grimhold/Docs/Architecture/AGENTS.md
Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- subject-action → Features: add shared player weapon presentation
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresentationMath.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresentationMath.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerWeaponPresenter.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: weapon
- path → Abilities: Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Docs/Architecture/AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/EnvironmentalHazardsArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/PlayerWeaponPresentationMathTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### b49debe2a52a5436cbca0511fcbad0831c025ea7

[b49debe2a52a5436cbca0511fcbad0831c025ea7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b49debe2a52a5436cbca0511fcbad0831c025ea7)

Date: 2026-07-28T20:32:28-03:00 · Classification: Other Changes (unknown) · Systems: Movement, UI, World & Visibility, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Update player animations and scene presentation assets
```

#### Original body

```text
- Add six-direction idle and movement sprite sheets and animation clips.
- Remove unused east and west animations from the player blend trees.
- Update player visuals, sorting, collision bounds, and HUD positioning.
- Extract gameplay and main menu cameras and the menu canvas into prefabs.
- Update scene references and the dungeon graybox tilemap data.
```

#### Changed paths

```text
Project Grimhold/Assets/Animations/Player/Idle/Idle_E.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_N.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_NE.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_NW.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_S.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_SE.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_SW.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_W.anim
Project Grimhold/Assets/Animations/Player/Player.controller
Project Grimhold/Assets/Animations/Player/Walk/Walk_E.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_N.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_NE.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_NW.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_S.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_SE.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_SW.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_W.anim
Project Grimhold/Assets/Placeholder/Character.meta
Project Grimhold/Assets/Placeholder/Character/Idle.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Idle/TemplateBody-Idle-UpRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Down-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Down-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownRight-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownRight-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Up-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Up-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpLeft-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpLeft-Sheet.png.meta
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpRight-Sheet.png
Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpRight-Sheet.png.meta
Project Grimhold/Assets/Prefabs/Dungeon_Graybox.prefab
Project Grimhold/Assets/Prefabs/Gameplay Camera.prefab
Project Grimhold/Assets/Prefabs/Gameplay Camera.prefab.meta
Project Grimhold/Assets/Prefabs/MainMenu Camera.prefab
Project Grimhold/Assets/Prefabs/MainMenu Camera.prefab.meta
Project Grimhold/Assets/Prefabs/MainMenu Canvas.prefab
Project Grimhold/Assets/Prefabs/MainMenu Canvas.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
```

#### Classification evidence

- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Down-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Down-Sheet.png.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownLeft-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownLeft-Sheet.png.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownRight-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-DownRight-Sheet.png.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Up-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-Up-Sheet.png.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpLeft-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpLeft-Sheet.png.meta
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpRight-Sheet.png
- path → Movement: Project Grimhold/Assets/Placeholder/Character/Movement/TemplateBody-Run-UpRight-Sheet.png.meta
- message-term → Movement: movement
- path → UI: Project Grimhold/Assets/Prefabs/MainMenu Camera.prefab
- path → UI: Project Grimhold/Assets/Prefabs/MainMenu Camera.prefab.meta
- path → UI: Project Grimhold/Assets/Prefabs/MainMenu Canvas.prefab
- path → UI: Project Grimhold/Assets/Prefabs/MainMenu Canvas.prefab.meta
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- message-term → UI: HUD
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Dungeon\_Graybox.prefab
- message-term → World &amp; Visibility: dungeon
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_E.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_N.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_S.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_W.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Player.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_E.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_N.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_S.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_W.anim
- message-term → Art &amp; Animation: animations

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 3758a6b4ea935248ce9984bd9973da2764517e52

[3758a6b4ea935248ce9984bd9973da2764517e52](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3758a6b4ea935248ce9984bd9973da2764517e52)

Date: 2026-07-28T20:28:51-03:00 · Classification: Other Changes (unknown) · Systems: Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Update enemy prefabs: presenters & sprites
```

#### Original body

```text
Adjust NetworkEnemy, NetworkEnemyMelee and NetworkEnemyRanged prefabs to use the new presenter and updated component references. Changed CombatPresenter -> EnemyCombatPresenter and updated _combatControllerSource fileIDs. Rewired NetworkedBehaviours references and replaced obsolete stripped MonoBehaviour fileIDs. Set SpriteRenderer size (0.5 x/y), assigned sprite assets and marked m_WasSpriteAssigned for melee and ranged prefabs. Minor script/source ID swaps to match updated components.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyRanged.prefab
```

#### Classification evidence

- message-term → Art &amp; Animation: sprites

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 4a35adb2b8ab3579620534d4d561b9ed3fe86e12

[4a35adb2b8ab3579620534d4d561b9ed3fe86e12](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4a35adb2b8ab3579620534d4d561b9ed3fe86e12)

Date: 2026-07-28T19:54:51-03:00 · Classification: Other Changes (unknown) · Systems: Movement

Flags: primary or unclassified

#### Original subject

```text
Normalized Enemy Speed
```

#### Original body

```text
Normalized Enemies' movement speed so they are not quicker on diagonaals
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
```

#### Classification evidence

- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### b3fc3959c7344f5c3f6762c472fc8f012a7c0ccf

[b3fc3959c7344f5c3f6762c472fc8f012a7c0ccf](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b3fc3959c7344f5c3f6762c472fc8f012a7c0ccf)

Date: 2026-07-28T19:48:50-03:00 · Classification: Other Changes (unknown) · Systems: Unassigned

Flags: primary or unclassified

#### Original subject

```text
Toggle trap detection collider during activation
```

#### Original body

```text
Add a serialized _detectionCol to BaseTrap and toggle it off when entering Active and back on when Ready to prevent re-triggering while a trap is executing. Call base.OnEnterActive() from DartTrap and SpikesTrap. Update DartTrap and Spikes prefabs to reference the detection collider, adjust collider offsets/sizes and sorting layers, and add an ImpactArea + DamageResolver to the Spikes prefab. Also apply related prefab instance cleanups in Gameplay.unity and reorder the Characters/Traps entries in TagManager.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Traps/DartTrap.prefab
Project Grimhold/Assets/Prefabs/Traps/SpikesTrap.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- No sufficient classification evidence.

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.
- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### 9ba5649d11f8ace8bbe36fe7ca873d26d810c316

[9ba5649d11f8ace8bbe36fe7ca873d26d810c316](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9ba5649d11f8ace8bbe36fe7ca873d26d810c316)

Date: 2026-07-28T19:37:56-03:00 · Classification: Features (inferred) · Systems: Combat, Networking & Sessions

Flags: primary or unclassified

#### Original subject

```text
Implement networked trap state machine and DartTrap
```

#### Original body

```text
Refactor traps to run as authoritative networked entities and add a networked DartTrap workflow.

- BaseTrap converted to NetworkBehaviour with a TickTimer-driven state machine (Ready, Telegraphing, Active, InCooldown). Replaces coroutine timing with FixedUpdateNetwork and Render for visuals. Adds lifecycle hooks (OnEnterTelegraphing/Active/Cooldown/Ready) for subclasses.
- DartTrap rewritten to use RangedAttackConfig + FusionProjectileSpawner to spawn networked projectiles. Adds networked counters/timers for dart bursts and auto-finds the spawner if unassigned.
- Added a new ScriptableObject asset (DartTrapConfig) for ranged attack settings and updated prefabs to reference new components.
- FusionProjectileSpawner now attempts to resolve an IDamageResolver automatically if none assigned.
- TrapInfo simplified to a pure configuration ScriptableObject (defaults added); removed its runtime/networked state.
- SpikesTrap marked DisallowMultipleComponent; prefabs updated accordingly.

These changes make trap behavior deterministic and authoritative on the host and reuse the existing projectile/damage systems for environmental hazards.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Traps/DartTrap.prefab
Project Grimhold/Assets/Prefabs/Traps/SpikesTrap.prefab
Project Grimhold/Assets/Scriptable Objects/DartTrapConfig.asset
Project Grimhold/Assets/Scriptable Objects/DartTrapConfig.asset.meta
Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/TrapInfo.cs
```

#### Classification evidence

- subject-action → Features: Implement networked trap state machine and DartTrap
- path → Combat: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- message-term → Combat: projectiles
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs

#### Diagnostics

- No classification ambiguity detected.

### a56efce30788544a9551aa22124539dc0f1f3005

[a56efce30788544a9551aa22124539dc0f1f3005](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a56efce30788544a9551aa22124539dc0f1f3005)

Date: 2026-07-27T23:03:50-03:00 · Classification: Features (inferred) · Systems: World & Visibility

Flags: primary or unclassified

#### Original subject

```text
Add DartTrap/Dart prefabs and SpikesTrap subclass
```

#### Original body

```text
Introduce a new dart trap: adds Dart prefab, DartTrap prefab and TrapInfo asset, plus DartTrap.cs (shooting coroutine + ShootDart placeholder). Adds SpikesTrap.cs and updates SpikesTrap.prefab to use the SpikesTrap script. Updates Gameplay.unity to place/configure a DartTrap instance and wire references. Adjusts BaseTrap.cs: trapInfo is now protected so subclasses can access it, and OnTriggerEnter2D visibility was changed to private.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Dart.prefab
Project Grimhold/Assets/Prefabs/Dart.prefab.meta
Project Grimhold/Assets/Prefabs/Traps/DartTrap.prefab
Project Grimhold/Assets/Prefabs/Traps/DartTrap.prefab.meta
Project Grimhold/Assets/Prefabs/Traps/SpikesTrap.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.asset
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.asset.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/DartTrap.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.cs.meta
```

#### Classification evidence

- subject-action → Features: Add DartTrap/Dart prefabs and SpikesTrap subclass
- message-term → World &amp; Visibility: visibility

#### Diagnostics

- No classification ambiguity detected.

### 9b6bb5dd9ee13eb8c90f9ef6cebadda63cdf0e49

[9b6bb5dd9ee13eb8c90f9ef6cebadda63cdf0e49](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9b6bb5dd9ee13eb8c90f9ef6cebadda63cdf0e49)

Date: 2026-07-27T20:24:27-03:00 · Classification: Features (inferred) · Systems: Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add trap system and SpikesTrap prefab
```

#### Original body

```text
Introduce a trap system: add TrapInfo (ScriptableObject) and BaseTrap (MonoBehaviour) with networked state using Fusion to manage trap lifecycle (Ready → Telegraphing → Active → InCooldown) and sprite color feedback. Add SpikesTrap asset and prefab (Assets/Prefabs/Traps) and place an instance in Gameplay.unity. Update TagManager to include a "Traps" tag and add necessary .meta files for new folders/assets.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Traps.meta
Project Grimhold/Assets/Prefabs/Traps/SpikesTrap.prefab
Project Grimhold/Assets/Prefabs/Traps/SpikesTrap.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Scenario.meta
Project Grimhold/Assets/Scripts/Scenario/Traps.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/BaseTrap.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.asset
Project Grimhold/Assets/Scripts/Scenario/Traps/SpikesTrap.asset.meta
Project Grimhold/Assets/Scripts/Scenario/Traps/TrapInfo.cs
Project Grimhold/Assets/Scripts/Scenario/Traps/TrapInfo.cs.meta
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- subject-action → Features: Add trap system and SpikesTrap prefab
- message-term → Art &amp; Animation: sprite

#### Diagnostics

- No classification ambiguity detected.

### d40315c294deee4b7cbffcff8e2e831ac047927b

[d40315c294deee4b7cbffcff8e2e831ac047927b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d40315c294deee4b7cbffcff8e2e831ac047927b)

Date: 2026-07-28T19:39:26-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, UI, Input, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
implement raid pause and defeat menu overlay
```

#### Original body

```text
Implement in-raid menu overlay providing pause functionality and automated defeat state presentation.
- Add RaidMenuView and RaidMenuPresenter components to manage in-raid menu UI layout, basic controls display, resume/abandon actions, and local gameplay input suppression.
- Support player defeat state by automatically presenting the defeat menu, locking character actions, enabling mouse cursor, and disabling resume option while keeping abandon option available.
- Update PlayerInputReader and RaidInventoryPresenter to prioritize active inventory screen closure when pressing Escape, preventing accidental menu toggle.
- Add native serialized EventSystem with InputSystemUIInputModule under LocalGameplayHud in NetworkPlayer.prefab to ensure UI pointer interaction across all gameplay scenes.
- Handle raid abandonment with controlled NetworkRunner shutdown and scene transition to MainMenu.
- Add EditMode and PlayMode unit/integration test suites covering Escape key input priority, defeat auto-open behavior, view rendering, and HUD bindings.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuView.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs.meta
```

#### Classification evidence

- subject-action → Features: implement raid pause and defeat menu overlay
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Inventory &amp; Loot: inventory
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuView.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidMenuView.cs.meta
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs.meta
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs.meta
- message-term → UI: menu
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidMenuPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMenuPlayModeTests.cs.meta
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 1bed1112762fcdd4ddf40e7494ed7d82945fa7fb

[1bed1112762fcdd4ddf40e7494ed7d82945fa7fb](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1bed1112762fcdd4ddf40e7494ed7d82945fa7fb)

Date: 2026-07-25T02:41:59-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Implement raid HUD and projectile world impacts
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs
Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PrimaryAttackStatus.cs
Project Grimhold/Assets/Scripts/Player/Combat/PrimaryAttackStatus.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs
Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs.meta
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
```

#### Classification evidence

- subject-action → Features: Implement raid HUD and projectile world impacts
- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
- path → Combat: Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset
- path → Combat: Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PrimaryAttackStatus.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PrimaryAttackStatus.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: projectile
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudView.cs.meta
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs.meta
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs.meta
- path → UI: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- message-term → UI: HUD
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidMainHudArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/NetworkProjectileImpactTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidMainHudPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PrimaryAttackStatusSimulationDriver.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 673cd7d4e2d7b975c8121d2c99e27a0f0f08ebf7

[673cd7d4e2d7b975c8121d2c99e27a0f0f08ebf7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/673cd7d4e2d7b975c8121d2c99e27a0f0f08ebf7)

Date: 2026-07-25T00:58:47-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Movement, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
unify facing and attacks with simulated aim
```

#### Original body

```text
Resolve player aim from AimWorldPosition after tick movement and persist it in FacingDirection.

Make melee and ranged attacks consume the same validated facing direction, preserve it for missing/default/invalid input, and enforce movement-before-combat execution order.

Add the Characters sorting layer for player and enemy visuals so weapon attacks remain visible in every direction while preserving front/back weapon ordering.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: combat
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs.meta
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
- path → Movement: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- message-term → Movement: movement
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 17ee059dec9eaf63b7e188d04590509a47473af2

[17ee059dec9eaf63b7e188d04590509a47473af2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/17ee059dec9eaf63b7e188d04590509a47473af2)

Date: 2026-07-24T22:08:54-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Inventory & Loot, Networking & Sessions, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
keep defeated players as lootable network entities
```

#### Original body

```text
Implement authoritative player defeat loot handling, refactor defeated players into persistent lootable network entities, preserve final defeat visuals, update prefab composition and Fusion registration, add rollback-safe inventory transfer, and expand gameplay architecture tests and documentation.
```

#### Changed paths

```text
Project Grimhold/AGENTS.md
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDefeatPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationSimulationDriver.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationSimulationDriver.cs.meta
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
```

#### Classification evidence

- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- path → Build &amp; Tooling: Project Grimhold/AGENTS.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DefeatedPlayerLootIntegrationPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationSimulationDriver.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationSimulationDriver.cs.meta
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 93508de8c9f4e22b395b70b4aee0f41b3de0c772

[93508de8c9f4e22b395b70b4aee0f41b3de0c772](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/93508de8c9f4e22b395b70b4aee0f41b3de0c772)

Date: 2026-07-23T00:40:18-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, Networking & Sessions, Art & Animation, Input, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Add synchronized dynamic breakable loot drops
```

#### Original body

```text
Implemented dynamic networked breakable objects with authoritative damage handling and deterministic random loot drops.
Added BreakableObject implementing IDamageable.
Added State Authority destruction and duplicate-drop protection.
Added dynamic Breakables spawn group support.
Added synchronized NetworkLootPickup initialization using catalog indices and quantities.
Added world loot sprite sorting layer and sorting order configuration.
Added Escape support to close the raid inventory.
Added prefab, architecture documentation, and automated tests.
Preserved pickup interaction, inventory transfer, and synchronized despawn flow.
```

#### Changed paths

```text
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/Input/PlayerInputActions.inputactions
Project Grimhold/Assets/Prefabs/BreakableObject.prefab
Project Grimhold/Assets/Prefabs/BreakableObject.prefab.meta
Project Grimhold/Assets/Prefabs/LootPickup 1.prefab
Project Grimhold/Assets/Prefabs/LootPickup.prefab
Project Grimhold/Assets/Resources/PNG/Objects.png.meta
Project Grimhold/Assets/Resources/Tiled_files/Objects.png.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Gameplay/BreakableObject.cs
Project Grimhold/Assets/Scripts/Gameplay/BreakableObject.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Networking/InitialBreakableSpawnState.cs
Project Grimhold/Assets/Scripts/Networking/InitialBreakableSpawnState.cs.meta
Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs
Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs.meta
Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Docs/Architecture/BreakableLootArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
```

#### Classification evidence

- subject-action → Features: Add synchronized dynamic breakable loot drops
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: damage
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup 1.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/BreakableLootArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialBreakableSpawnState.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialBreakableSpawnState.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- message-term → Art &amp; Animation: sprite
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/BreakableLootArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/BreakableDamageSimulationDriver.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### a477674bb24dbbf35c942c6ccad8cc1647a7cd6b

[a477674bb24dbbf35c942c6ccad8cc1647a7cd6b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a477674bb24dbbf35c942c6ccad8cc1647a7cd6b)

Date: 2026-07-22T23:33:24-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
make defeated enemies persist as lootable containers
```

#### Original body

```text
Reused the enemy’s existing NetworkObject as its corpse instead of spawning a separate corpse prefab.
Added the shared loot container and interaction components to enemy prefabs.
Kept enemy loot unavailable while alive and enabled it authoritatively after fatal damage.
Disabled enemy movement and combat after death while preserving the body for future death animations.
Added an interaction-layer trigger for corpse inspection.
Updated entity registration to support damage, interaction, and loot capabilities under the same entity ID.
Removed the obsolete LootCorpse prefab.
Updated architecture documentation and automated tests.
Verified all tests: 229 EditMode and 24 PlayMode tests passed.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/LootCorpse.prefab
Project Grimhold/Assets/Prefabs/LootCorpse.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyRanged.prefab
Project Grimhold/Assets/Scripts/Character/DefeatPresenterBase.cs
Project Grimhold/Assets/Scripts/Character/EnemyCharacter.cs
Project Grimhold/Assets/Scripts/Character/EnemyDefeatPresenter.cs
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs
Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs.meta
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs.meta
- message-term → Combat: damage
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootCorpse.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootCorpse.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/EnemyFatalDamageSimulationDriver.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 6c12472a54e7ec92bb17ebb600ce7a2de5fde55d

[6c12472a54e7ec92bb17ebb600ce7a2de5fde55d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6c12472a54e7ec92bb17ebb600ce7a2de5fde55d)

Date: 2026-07-22T02:43:30-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Input, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
implement raid inventory UI, container loot flow, and local interaction toggle
```

#### Original body

```text
implement raid inventory UI, container loot flow, and local interaction toggle
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/LootContainer.prefab
Project Grimhold/Assets/Prefabs/LootCorpse.prefab
Project Grimhold/Assets/Prefabs/LootCorpse.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
Project Grimhold/Assets/Resources/Tiled_files/Objects.png.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Tables.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset.meta
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentRoller.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentRoller.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTable.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTable.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableEntry.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableEntry.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableValidation.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableValidation.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerRandomContentConfig.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerRandomContentConfig.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/ValidatedLootContainerContentSnapshot.cs
Project Grimhold/Assets/Scripts/Gameplay/ValidatedLootContainerContentSnapshot.cs.meta
Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs
Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs.meta
Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs
Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionPromptMetadata.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionPromptMetadata.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSelectionState.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSelectionState.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
```

#### Classification evidence

- subject-action → Features: implement raid inventory UI, container loot flow, and local interaction toggle
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootContainer.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootCorpse.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootCorpse.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentRoller.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentRoller.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTable.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableEntry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableEntry.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableValidation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerContentTableValidation.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerRandomContentConfig.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerRandomContentConfig.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerSeedRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/ValidatedLootContainerContentSnapshot.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/ValidatedLootContainerContentSnapshot.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionPromptMetadata.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionPromptMetadata.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSelectionState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootSelectionState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialLootSpawnState.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/InitialSpawnGroupPolicy.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- path → UI: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: UI
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootCompositionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerContentGenerationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkSpawnManagerLootTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootSelectionStateTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LootTransferPresentationQueuePlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### e320fdc0fd7ec380a2c298e3d384b746caf91250

[e320fdc0fd7ec380a2c298e3d384b746caf91250](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e320fdc0fd7ec380a2c298e3d384b746caf91250)

Date: 2026-07-21T21:48:48-03:00 · Classification: Features (inferred) · Systems: Combat, Extraction, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
add authoritative reusable network loot containers
```

#### Original body

```text
- Add NetworkLootContainer with replicated catalog-indexed contents,
  initialization state, runtime availability, and change sequencing.
- Validate initial content atomically and register loot-source capabilities and
  colliders through the runner-scoped EntityRegistry.
- Preserve initialized contents and disable transfers when grouped registration
  fails.
- Add authoritative runtime availability changes without modifying contents,
  registration, or LootChangeSequence.
- Add LootTransferTransaction with strict extraction validation, receive
  validation, extraction commit, and receive commit ordering.
- Enforce validate/commit contracts in player and container endpoints, treating
  impossible commit-time states as integration errors.
- Separate pickup-specific presentation feedback from generic loot reception so
  container transfers do not display pickup toasts.
- Add PlayerLootTransferNetworkController with primitive-only Fusion RPC
  payloads, authoritative range and quantity resolution, and local
  confirmation reconstruction.
- Support one legitimate client request in flight, a non-overwritable
  authoritative pending request, and bounded last-request idempotency.
- Handle pending duplicates, processed duplicates, payload conflicts, busy
  sequences, stale sequences, malformed confirmations, and unresolved loot
  metadata for rejected transfers.
- Keep request, pending, cache, and confirmation state local and non-networked.
- Add LootContainer and separate development harness prefabs without adding
  debug tooling to production player prefabs or scenes.
- Add Editor/Development-only controls for public requests, duplicate and
  conflict envelopes, busy sequences, and authoritative availability changes.
- Update base, melee, and ranged player prefabs to include the transfer network
  behaviour required by Fusion.
- Add EditMode coverage for initialization, grouped registration, transactions,
  request lifecycle, idempotency, confirmation reconstruction, and prefab
  composition.
- Update loot architecture and base-contract documentation to reflect the final
  implementation.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/Props.meta
Project Grimhold/Assets/Placeholder/Props/Bone-Interactable.png
Project Grimhold/Assets/Placeholder/Props/Bone-Interactable.png.meta
Project Grimhold/Assets/Placeholder/Props/Bone.png
Project Grimhold/Assets/Placeholder/Props/Bone.png.meta
Project Grimhold/Assets/Placeholder/Props/Coins-Interactable.png
Project Grimhold/Assets/Placeholder/Props/Coins-Interactable.png.meta
Project Grimhold/Assets/Placeholder/Props/Coins.png
Project Grimhold/Assets/Placeholder/Props/Coins.png.meta
Project Grimhold/Assets/Placeholder/Props/HealthPotion-Interactable.png
Project Grimhold/Assets/Placeholder/Props/HealthPotion-Interactable.png.meta
Project Grimhold/Assets/Placeholder/Props/HealthPotion.png
Project Grimhold/Assets/Placeholder/Props/HealthPotion.png.meta
Project Grimhold/Assets/Prefabs/Debug.meta
Project Grimhold/Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab
Project Grimhold/Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab.meta
Project Grimhold/Assets/Prefabs/LootContainer.prefab
Project Grimhold/Assets/Prefabs/LootContainer.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset.meta
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Core/ILootPickupFeedbackSink.cs
Project Grimhold/Assets/Scripts/Core/ILootPickupFeedbackSink.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitialEntry.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitialEntry.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestState.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs.meta
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- subject-action → Features: add authoritative reusable network loot containers
- path → Combat: Project Grimhold/Assets/Placeholder/Props/HealthPotion-Interactable.png
- path → Combat: Project Grimhold/Assets/Placeholder/Props/HealthPotion-Interactable.png.meta
- path → Combat: Project Grimhold/Assets/Placeholder/Props/HealthPotion.png
- path → Combat: Project Grimhold/Assets/Placeholder/Props/HealthPotion.png.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Debug/LootContainerTransferDebugHarness.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootContainer.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootContainer.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Bone.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/Coins.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/HealthPotion.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/ILootPickupFeedbackSink.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/ILootPickupFeedbackSink.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitialEntry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitialEntry.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerInitializationRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootContainerTransferDebugHarness.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferConfirmation.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestState.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferTransportRejectionReason.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/EntityRegistryLootSourceTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerInitializationRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContainerPrefabTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferClientRequestStateTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferConfirmationFactoryTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferRequestStateTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferTransactionTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### cac3ae59dd70946240b36b4568edf79aa65c8349

[cac3ae59dd70946240b36b4568edf79aa65c8349](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/cac3ae59dd70946240b36b4568edf79aa65c8349)

Date: 2026-07-21T20:45:25-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions

Flags: primary or unclassified

#### Original subject

```text
Merge branch 'Characters' into Testing
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 2fa5322186f16d5a01a7efff77b0ce799d295360

[2fa5322186f16d5a01a7efff77b0ce799d295360](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2fa5322186f16d5a01a7efff77b0ce799d295360)

Date: 2026-07-21T19:52:29-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, Networking & Sessions, UI, Input, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
add provisional raid inventory UI
```

#### Original body

```text
- add a local read-only inventory screen with reusable slots
- bind the presenter directly to PlayerLootReceiver
- preserve snapshot order and fill remaining empty slots
- add placeholders and per-slot fallback for unresolved definitions
- add Tab inventory toggle and local gameplay input suppression
- restore continuous movement and aim without replaying discrete actions
- add a runner-scoped LocalInputContext without changing runner discovery
- preserve the existing pickup toast
- update the base prefab, automated tests, and architecture documentation
```

#### Changed paths

```text
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/Input/PlayerInputActions.inputactions
Project Grimhold/Assets/Placeholder/UI.meta
Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png
Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI.meta
Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional 1.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional.asset
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs
Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryProjection.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryProjection.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md.meta
```

#### Classification evidence

- subject-action → Features: add provisional raid inventory UI
- path → Inventory &amp; Loot: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png
- path → Inventory &amp; Loot: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional 1.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryProjection.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryProjection.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md.meta
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs.meta
- path → UI: Project Grimhold/Assets/Placeholder/UI.meta
- path → UI: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png
- path → UI: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
- message-term → UI: UI
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions
- path → Input: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
- path → Input: Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs
- path → Input: Project Grimhold/Assets/Scripts/Networking/LocalInputContext.cs.meta
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md.meta
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventoryProjectionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidInventorySlotDataTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### ab8edc9d6d28ef0fc59bfba9f6d3eb9b3baef878

[ab8edc9d6d28ef0fc59bfba9f6d3eb9b3baef878](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ab8edc9d6d28ef0fc59bfba9f6d3eb9b3baef878)

Date: 2026-07-21T18:12:28-03:00 · Classification: Features (inferred) · Systems: Extraction, Inventory & Loot, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
implement temporary player loot inventory
```

#### Original body

```text
Added configurable gameplay slot capacity to PlayerLootReceiver with stacking support and InventoryFull rejection for new loot IDs.

Implemented authoritative loot extraction with Validate/Commit separation, partial extraction, empty-stack removal, and LootChangeSequence updates.

Preserved the replicated NetworkDictionary as the single source of truth, the existing pickup transaction, and snapshot-based presentation.

Added pure inventory rule tests, configured the base player prefab with 16 slots, and updated the loot architecture and contract documentation.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/LootPickup 1.prefab
Project Grimhold/Assets/Prefabs/LootPickup 1.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Resources/PNG/Objects.png.meta
Project Grimhold/Assets/Resources/PNG/doors_lever_chest_animation.png.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional 1.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional 1.asset.meta
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- subject-action → Features: implement temporary player loot inventory
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup 1.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup 1.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional 1.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional 1.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/doors\_lever\_chest\_animation.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 68278f1cee993c62e556a6e3e6cda99ad746f981

[68278f1cee993c62e556a6e3e6cda99ad746f981](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/68278f1cee993c62e556a6e3e6cda99ad746f981)

Date: 2026-07-21T17:35:52-03:00 · Classification: Other Changes (unknown) · Systems: Extraction, Inventory & Loot, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
unify loot inventory and transfer contracts
```

#### Original body

```text
Added unified loot transfer request, result, and typed failure reasons.
Added segregated content, quantity, slot-capacity, reception, and extraction contracts.
Evolved LootEntry into the canonical stack value object.
Migrated PlayerLootReceiver to explicit prevalidation and commit phases.
Preserved the authoritative pickup reservation, rollback, and consumption flow.
Removed the legacy grant/receive contracts.
Added comprehensive EditMode contract and migration tests.
Updated loot and player-interaction architecture documentation in English.
Preserved HUD, presentation, RPCs, networked properties, prefabs, and scenes.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Core/ILootContentReader.cs
Project Grimhold/Assets/Scripts/Core/ILootContentReader.cs.meta
Project Grimhold/Assets/Scripts/Core/ILootExtractor.cs
Project Grimhold/Assets/Scripts/Core/ILootExtractor.cs.meta
Project Grimhold/Assets/Scripts/Core/ILootQuantityReader.cs
Project Grimhold/Assets/Scripts/Core/ILootQuantityReader.cs.meta
Project Grimhold/Assets/Scripts/Core/ILootReceiver.cs
Project Grimhold/Assets/Scripts/Core/ILootSlotCapacityReader.cs
Project Grimhold/Assets/Scripts/Core/ILootSlotCapacityReader.cs.meta
Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs
Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/LootId.cs
Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs.meta
Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs
Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs.meta
Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs
Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs
Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootId.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: loot
- message-term → UI: HUD
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootTransferContractsTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 075df797c2c54c7f69b0d330c29a4cd3a43e94f8

[075df797c2c54c7f69b0d330c29a4cd3a43e94f8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/075df797c2c54c7f69b0d330c29a4cd3a43e94f8)

Date: 2026-07-21T16:46:07-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
implement networked loot and local interaction HUD
```

#### Original body

```text
- Implement authoritative loot collection using a NetworkDictionary with deterministic indices.
- Add safe read-only APIs for querying loot contents, quantities, and derived total value.
- Add change sequences and targeted RPC notifications for the owning player.
- Separate interaction target selection from authoritative execution.
- Add local predictive target detection for displaying the interaction prompt without executing gameplay.
- Record typed results for accepted and rejected interaction attempts.
- Publish interaction results from Render to keep presentation outside network simulation.
- Add a local HUD with an interaction prompt, result feedback, loot list, accumulated value, and pickup toast.
- Enable the HUD exclusively for the player with Input Authority.
- Prevent duplicate feedback using authoritative sequences and local deduplication.
- Suppress generic success feedback when the interaction consumes a loot pickup.
- Integrate the HUD and its dependencies into NetworkPlayer.prefab.
- Update the melee and ranged variants with the inherited NetworkBehaviours.
- Expand tests for interaction resolution, loot catalog contracts, presentation payloads, and HUD lifecycle.
- Update the interaction and loot architecture documentation.

Validation:
- Runtime and Editor assemblies compile without errors.
- Fusion IL weaving completes successfully.
- The base player prefab and both variants import successfully.
- The Host/Client flow was manually validated using two game instances.
- Local HUD isolation, loot replication, targeted RPCs, and remote pickup despawning were verified.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Core/LootGrantPresentationEvent.cs
Project Grimhold/Assets/Scripts/Core/LootGrantPresentationEvent.cs.meta
Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs
Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation.meta
Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/AGENTS.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
```

#### Classification evidence

- subject-action → Features: implement networked loot and local interaction HUD
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantPresentationEvent.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantPresentationEvent.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/InteractionHudPresenter.cs.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LootHudPresenter.cs.meta
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs.meta
- message-term → UI: HUD
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Docs/Architecture/AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/PlayerLootReceiverTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/InteractionLootHudPlayModeTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 9cd8c8b497ae4fa1b1389c1296e16ed0fc50b379

[9cd8c8b497ae4fa1b1389c1296e16ed0fc50b379](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9cd8c8b497ae4fa1b1389c1296e16ed0fc50b379)

Date: 2026-07-20T14:33:59-03:00 · Classification: Features (inferred) · Systems: Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Documentation

Flags: primary or unclassified

#### Original subject

```text
add late-join prevention and scene spawn-point policy architecture
```

#### Original body

```text
- Enforced authoritative coordinator persistence by spawning the NetworkMatchController with Photon Fusion's managed `NetworkSpawnFlags.DontDestroyOnLoad` flags.
- Replaced global ActiveScene fallbacks with runner-scoped scene resolution using `runner.SceneManager.MainRunnerScene` exclusively.
- Implemented a declarative scene configuration system (`SceneSpawnPointPolicy`) to explicitly declare whether a scene requires spawn points (`Required`) or not (`NotRequired`).
- Decoupled spatial scene configuration into `NetworkSpawnSceneConfiguration` and spawn group definitions (`SpawnGroupType`, `SpawnGroupDefinition`).
- Enforced a fail-closed spawning pipeline using incremental load generations and tracking states (Pending, Processing, Completed, Failed) to deduplicate callback execution.
- Added strict validations: `CanUseCurrentSceneSpawnPoints` for spatial-based entities and `CanSpawnAtExplicitTransform` for explicit coordinate-based spawns.
- Fixed an enemy spawning bug by refactoring `_spawnedEnemies` from a dictionary to a list to prevent duplicate key exceptions.
- Added visual/console feedback message when a client fails or is rejected to join an already started session.
- Updated launcher start/shutdown lifecycles with secure try-finally blocks and async cleanup.
- Refactored all networking code and architecture documents to align with the new spawn-point terminology.
```

#### Changed paths

```text
Project Grimhold/Assets/Editor.meta
Project Grimhold/Assets/Prefabs/NetworkMatchController.prefab
Project Grimhold/Assets/Prefabs/NetworkMatchController.prefab.meta
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/LobbyMenuController.cs
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/NetworkMatchController.cs
Project Grimhold/Assets/Scripts/Networking/NetworkMatchController.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnSceneConfiguration.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnSceneConfiguration.cs.meta
Project Grimhold/Assets/Scripts/Networking/SceneSpawnPointPolicy.cs
Project Grimhold/Assets/Scripts/Networking/SceneSpawnPointPolicy.cs.meta
Project Grimhold/Assets/Scripts/Networking/SpawnGroupDefinition.cs
Project Grimhold/Assets/Scripts/Networking/SpawnGroupDefinition.cs.meta
Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs
Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
```

#### Classification evidence

- subject-action → Features: add late-join prevention and scene spawn-point policy architecture
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkMatchController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkMatchController.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnSceneConfiguration.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnSceneConfiguration.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SceneSpawnPointPolicy.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SceneSpawnPointPolicy.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SpawnGroupDefinition.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SpawnGroupDefinition.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SpawnGroupType.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- message-term → Networking &amp; Sessions: Photon Fusion
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor.meta
- path → Documentation: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md

#### Diagnostics

- No classification ambiguity detected.

### a0f91e7b9ab10e645c1cd30962b0c96cf58d65e8

[a0f91e7b9ab10e645c1cd30962b0c96cf58d65e8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a0f91e7b9ab10e645c1cd30962b0c96cf58d65e8)

Date: 2026-07-20T13:02:24-03:00 · Classification: Features (inferred) · Systems: World & Visibility

Flags: primary or unclassified

#### Original subject

```text
Add a dungeon graybox to the game
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Dungeon_Graybox.prefab
Project Grimhold/Assets/Prefabs/Dungeon_Graybox.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
```

#### Classification evidence

- subject-action → Features: Add a dungeon graybox to the game
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Dungeon\_Graybox.prefab
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Dungeon\_Graybox.prefab.meta
- message-term → World &amp; Visibility: dungeon

#### Diagnostics

- No classification ambiguity detected.

### 4b1640bc78dd7490d57708f89306f4383ee91b3d

[4b1640bc78dd7490d57708f89306f4383ee91b3d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4b1640bc78dd7490d57708f89306f4383ee91b3d)

Date: 2026-07-21T20:24:45-03:00 · Classification: Other Changes (unknown) · Systems: Unassigned

Flags: primary or unclassified

#### Original subject

```text
Update Gameplay.unity
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/Gameplay.unity
```

#### Classification evidence

- No sufficient classification evidence.

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.
- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### b890317588aa2530670097f84d243941b4888e2e

[b890317588aa2530670097f84d243941b4888e2e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b890317588aa2530670097f84d243941b4888e2e)

Date: 2026-07-21T20:24:19-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, Networking & Sessions, Art & Animation, Documentation

Flags: primary or unclassified

#### Original subject

```text
Add enemy AI, FSM, and shared presentation base
```

#### Original body

```text
Introduce enemy systems and refactor shared presentation/API contracts. Adds EnemyFSM, IEnemyState and concrete states (Patrol/Chase/Attack/Dead), EnemyMovementAIController and EnemyCombatAIController, plus NetworkEnemyMelee/NetworkEnemyRanged prefabs. Add shared presentation components (CharacterAnimatorView, CombatPresenterBase, DefeatPresenterBase) and interfaces (IAnimatorController, ICombatController, IMovementState) and adapt player presenters to inherit them. Make combat/attack/projectile systems entity-agnostic (DamageResolver, MeleeAttack, RangedAttack, FusionProjectileSpawner), update NetworkSpawnManager to support spawn-group prefab lists, and include architecture doc EnemyCombatArchitecture.md.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyMelee.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkEnemyRanged.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemyRanged.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Character/CharacterAnimatorView.cs
Project Grimhold/Assets/Scripts/Character/CharacterAnimatorView.cs.meta
Project Grimhold/Assets/Scripts/Character/CombatPresenterBase.cs
Project Grimhold/Assets/Scripts/Character/CombatPresenterBase.cs.meta
Project Grimhold/Assets/Scripts/Character/DefeatPresenterBase.cs
Project Grimhold/Assets/Scripts/Character/DefeatPresenterBase.cs.meta
Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs
Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs
Project Grimhold/Assets/Scripts/Character/EnemyDefeatPresenter.cs
Project Grimhold/Assets/Scripts/Character/EnemyFSM.cs
Project Grimhold/Assets/Scripts/Character/EnemyFSM.cs.meta
Project Grimhold/Assets/Scripts/Character/EnemyStateType.cs
Project Grimhold/Assets/Scripts/Character/EnemyStateType.cs.meta
Project Grimhold/Assets/Scripts/Character/IEnemyState.cs
Project Grimhold/Assets/Scripts/Character/IEnemyState.cs.meta
Project Grimhold/Assets/Scripts/Character/States.meta
Project Grimhold/Assets/Scripts/Character/States/EnemyAttackState.cs
Project Grimhold/Assets/Scripts/Character/States/EnemyAttackState.cs.meta
Project Grimhold/Assets/Scripts/Character/States/EnemyChaseState.cs
Project Grimhold/Assets/Scripts/Character/States/EnemyChaseState.cs.meta
Project Grimhold/Assets/Scripts/Character/States/EnemyDeadState.cs
Project Grimhold/Assets/Scripts/Character/States/EnemyDeadState.cs.meta
Project Grimhold/Assets/Scripts/Character/States/EnemyPatrolState.cs
Project Grimhold/Assets/Scripts/Character/States/EnemyPatrolState.cs.meta
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
Project Grimhold/Assets/Scripts/Core/IAnimatorController.cs
Project Grimhold/Assets/Scripts/Core/IAnimatorController.cs.meta
Project Grimhold/Assets/Scripts/Core/ICombatController.cs
Project Grimhold/Assets/Scripts/Core/ICombatController.cs.meta
Project Grimhold/Assets/Scripts/Core/IMovementState.cs
Project Grimhold/Assets/Scripts/Core/IMovementState.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs.meta
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDefeatPresenter.cs
Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add enemy AI, FSM, and shared presentation base
- path → Combat: Project Grimhold/Assets/Scripts/Character/CombatPresenterBase.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/CombatPresenterBase.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/States/EnemyAttackState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/States/EnemyAttackState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
- path → Combat: Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
- message-term → Combat: combat
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Character/CharacterAnimatorView.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Character/CharacterAnimatorView.cs.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
- path → Documentation: Project Grimhold/Docs/Architecture/EnemyCombatArchitecture.md
- message-term → Documentation: architecture doc

#### Diagnostics

- No classification ambiguity detected.

### 03ff9bfe8002779348071365bd7d106c4c9a3bf3

[03ff9bfe8002779348071365bd7d106c4c9a3bf3](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/03ff9bfe8002779348071365bd7d106c4c9a3bf3)

Date: 2026-07-19T21:36:46-03:00 · Classification: Features (inferred) · Systems: Unassigned

Flags: primary or unclassified

#### Original subject

```text
Add enemy defeat presenter
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Scripts/Character/EnemyDefeatPresenter.cs
Project Grimhold/Assets/Scripts/Character/EnemyDefeatPresenter.cs.meta
```

#### Classification evidence

- subject-action → Features: Add enemy defeat presenter

#### Diagnostics

- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### 4529692c64fff8a7f074b2dd10aa5c74589b797a

[4529692c64fff8a7f074b2dd10aa5c74589b797a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4529692c64fff8a7f074b2dd10aa5c74589b797a)

Date: 2026-07-18T22:38:42-03:00 · Classification: Other Changes (unknown) · Systems: Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Graybox map created
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Resources.meta
Project Grimhold/Assets/Resources/PNG.meta
Project Grimhold/Assets/Resources/PNG/COUPON.pdf
Project Grimhold/Assets/Resources/PNG/COUPON.pdf.meta
Project Grimhold/Assets/Resources/PNG/Objects.png
Project Grimhold/Assets/Resources/PNG/Objects.png.meta
Project Grimhold/Assets/Resources/PNG/Water_coasts_animation.png
Project Grimhold/Assets/Resources/PNG/Water_coasts_animation.png.meta
Project Grimhold/Assets/Resources/PNG/decorative_cracks_coasts_animation.png
Project Grimhold/Assets/Resources/PNG/decorative_cracks_coasts_animation.png.meta
Project Grimhold/Assets/Resources/PNG/decorative_cracks_floor.png
Project Grimhold/Assets/Resources/PNG/decorative_cracks_floor.png.meta
Project Grimhold/Assets/Resources/PNG/decorative_cracks_walls.png
Project Grimhold/Assets/Resources/PNG/decorative_cracks_walls.png.meta
Project Grimhold/Assets/Resources/PNG/doors_lever_chest_animation.png
Project Grimhold/Assets/Resources/PNG/doors_lever_chest_animation.png.meta
Project Grimhold/Assets/Resources/PNG/fire_animation.png
Project Grimhold/Assets/Resources/PNG/fire_animation.png.meta
Project Grimhold/Assets/Resources/PNG/fire_animation2.png
Project Grimhold/Assets/Resources/PNG/fire_animation2.png.meta
Project Grimhold/Assets/Resources/PNG/trap_animation.png
Project Grimhold/Assets/Resources/PNG/trap_animation.png.meta
Project Grimhold/Assets/Resources/PNG/walls_floor.png
Project Grimhold/Assets/Resources/PNG/walls_floor.png.meta
Project Grimhold/Assets/Resources/PNG/water_detilazation_v2.png
Project Grimhold/Assets/Resources/PNG/water_detilazation_v2.png.meta
Project Grimhold/Assets/Resources/PSD.meta
Project Grimhold/Assets/Resources/PSD/COUPON.pdf
Project Grimhold/Assets/Resources/PSD/COUPON.pdf.meta
Project Grimhold/Assets/Resources/PSD/Objects.psd
Project Grimhold/Assets/Resources/PSD/Objects.psd.meta
Project Grimhold/Assets/Resources/PSD/Water_coasts_animation.psd
Project Grimhold/Assets/Resources/PSD/Water_coasts_animation.psd.meta
Project Grimhold/Assets/Resources/PSD/decorative_cracks_coasts_animation.psd
Project Grimhold/Assets/Resources/PSD/decorative_cracks_coasts_animation.psd.meta
Project Grimhold/Assets/Resources/PSD/decorative_cracks_floor.psd
Project Grimhold/Assets/Resources/PSD/decorative_cracks_floor.psd.meta
Project Grimhold/Assets/Resources/PSD/decorative_cracks_walls.psd
Project Grimhold/Assets/Resources/PSD/decorative_cracks_walls.psd.meta
Project Grimhold/Assets/Resources/PSD/doors_lever_chest_animation.psd
Project Grimhold/Assets/Resources/PSD/doors_lever_chest_animation.psd.meta
Project Grimhold/Assets/Resources/PSD/fire_animation.psd
Project Grimhold/Assets/Resources/PSD/fire_animation.psd.meta
Project Grimhold/Assets/Resources/PSD/fire_animation2.psd
Project Grimhold/Assets/Resources/PSD/fire_animation2.psd.meta
Project Grimhold/Assets/Resources/PSD/trap_animation.psd
Project Grimhold/Assets/Resources/PSD/trap_animation.psd.meta
Project Grimhold/Assets/Resources/PSD/walls_floor.psd
Project Grimhold/Assets/Resources/PSD/walls_floor.psd.meta
Project Grimhold/Assets/Resources/PSD/water_details_animation.psd
Project Grimhold/Assets/Resources/PSD/water_details_animation.psd.meta
Project Grimhold/Assets/Resources/ProvisionalTileset 48x56 (Tile 8x8).png
Project Grimhold/Assets/Resources/ProvisionalTileset 48x56 (Tile 8x8).png.meta
Project Grimhold/Assets/Resources/Tiled_files.meta
Project Grimhold/Assets/Resources/Tiled_files/Dungeon1.tmx
Project Grimhold/Assets/Resources/Tiled_files/Dungeon1.tmx.meta
Project Grimhold/Assets/Resources/Tiled_files/Objects.png
Project Grimhold/Assets/Resources/Tiled_files/Objects.png.meta
Project Grimhold/Assets/Resources/Tiled_files/Water_coasts_animation.png
Project Grimhold/Assets/Resources/Tiled_files/Water_coasts_animation.png.meta
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_coasts_animation.png
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_coasts_animation.png.meta
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_floor.png
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_floor.png.meta
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_walls.png
Project Grimhold/Assets/Resources/Tiled_files/decorative_cracks_walls.png.meta
Project Grimhold/Assets/Resources/Tiled_files/doors_lever_chest_animation.png
Project Grimhold/Assets/Resources/Tiled_files/doors_lever_chest_animation.png.meta
Project Grimhold/Assets/Resources/Tiled_files/fire_animation.png
Project Grimhold/Assets/Resources/Tiled_files/fire_animation.png.meta
Project Grimhold/Assets/Resources/Tiled_files/fire_animation2.png
Project Grimhold/Assets/Resources/Tiled_files/fire_animation2.png.meta
Project Grimhold/Assets/Resources/Tiled_files/trap_animation.png
Project Grimhold/Assets/Resources/Tiled_files/trap_animation.png.meta
Project Grimhold/Assets/Resources/Tiled_files/walls_floor.png
Project Grimhold/Assets/Resources/Tiled_files/walls_floor.png.meta
Project Grimhold/Assets/Resources/Tiled_files/water_details_animation.png
Project Grimhold/Assets/Resources/Tiled_files/water_details_animation.png.meta
Project Grimhold/Assets/Resources/Tilemaps.meta
Project Grimhold/Assets/Resources/Tilemaps/New Tile Palette.prefab
Project Grimhold/Assets/Resources/Tilemaps/New Tile Palette.prefab.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_0.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_0.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_1.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_1.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_10.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_10.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_11.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_11.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_12.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_12.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_13.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_13.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_14.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_14.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_15.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_15.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_16.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_16.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_17.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_17.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_18.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_18.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_19.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_19.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_2.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_2.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_20.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_20.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_21.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_21.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_22.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_22.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_23.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_23.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_24.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_24.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_25.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_25.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_26.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_26.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_27.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_27.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_28.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_28.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_29.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_29.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_3.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_3.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_30.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_30.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_31.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_31.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_32.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_32.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_4.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_4.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_5.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_5.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_6.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_6.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_7.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_7.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_8.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_8.asset.meta
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_9.asset
Project Grimhold/Assets/Resources/Tilemaps/ProvisionalTileset 48x56 (Tile 8x8)_9.asset.meta
Project Grimhold/Assets/Resources/tileset.png
Project Grimhold/Assets/Resources/tileset.png.meta
Project Grimhold/Assets/Scenes/Graybox.unity
Project Grimhold/Assets/Scenes/Graybox.unity.meta
```

#### Classification evidence

- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/Water\_coasts\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/Water\_coasts\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/decorative\_cracks\_coasts\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/decorative\_cracks\_coasts\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/doors\_lever\_chest\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/doors\_lever\_chest\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/fire\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/fire\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/trap\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PNG/trap\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/Water\_coasts\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/Water\_coasts\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/decorative\_cracks\_coasts\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/decorative\_cracks\_coasts\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/doors\_lever\_chest\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/doors\_lever\_chest\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/fire\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/fire\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/trap\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/trap\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/water\_details\_animation.psd
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/PSD/water\_details\_animation.psd.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/Water\_coasts\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/Water\_coasts\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/decorative\_cracks\_coasts\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/decorative\_cracks\_coasts\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/doors\_lever\_chest\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/doors\_lever\_chest\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/fire\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/fire\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/trap\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/trap\_animation.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/water\_details\_animation.png
- path → Art &amp; Animation: Project Grimhold/Assets/Resources/Tiled\_files/water\_details\_animation.png.meta

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### ec0dbb6ee846f7fd231409c78d569c57706eafce

[ec0dbb6ee846f7fd231409c78d569c57706eafce](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ec0dbb6ee846f7fd231409c78d569c57706eafce)

Date: 2026-07-18T20:50:53-03:00 · Classification: Tests (inferred) · Systems: Input, Tests

Flags: secondary

#### Original subject

```text
Update PlayerInputReaderPlayModeTests.cs
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
```

#### Classification evidence

- artifact-paths → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### f36998d312b304319f6f0cd745fab4cc5e4cafa3

[f36998d312b304319f6f0cd745fab4cc5e4cafa3](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f36998d312b304319f6f0cd745fab4cc5e4cafa3)

Date: 2026-07-19T21:17:42-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add enemy animator and combat presenter components
```

#### Original body

```text
Introduces EnemyAnimatorView and CombatPresenter components to handle enemy-specific animation updates and combat presentation. These replace the player-only components in the NetworkEnemy prefab and integrate with the enemy's movement AI controller. Also adjusts weapon hand distance from 0.5 to 0.2.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs
Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs.meta
Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs
Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs.meta
```

#### Classification evidence

- subject-action → Features: Add enemy animator and combat presenter components
- path → Combat: Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/EnemyCombatPresenter.cs.meta
- message-term → Combat: combat
- message-term → Movement: movement
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Character/EnemyAnimatorView.cs.meta
- message-term → Art &amp; Animation: animation

#### Diagnostics

- No classification ambiguity detected.

### f5fe81d6d17e8b7ec9086a834bd5f1dbf8b3afa3

[f5fe81d6d17e8b7ec9086a834bd5f1dbf8b3afa3](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f5fe81d6d17e8b7ec9086a834bd5f1dbf8b3afa3)

Date: 2026-07-19T21:01:17-03:00 · Classification: Refactoring (inferred) · Systems: Movement

Flags: primary or unclassified

#### Original subject

```text
Refactor enemy AI and adjust movement speeds
```

#### Original body

```text
Remove player-specific components from NetworkEnemy prefab that were not used. Refactor EnemyMovementAIController to use patrol and pursuit mechanics with line-of-sight detection instead of a single moveSpeed parameter. Increase NetworkPlayer movement speed from 1 to 4.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Resources.meta
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
```

#### Classification evidence

- subject-action → Refactoring: Refactor enemy AI and adjust movement speeds
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
- message-term → Movement: movement

#### Diagnostics

- No classification ambiguity detected.

### c8c50b61773c74e0ba8daa71c0251dd969b893a1

[c8c50b61773c74e0ba8daa71c0251dd969b893a1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c8c50b61773c74e0ba8daa71c0251dd969b893a1)

Date: 2026-07-18T12:43:20-03:00 · Classification: Features (inferred) · Systems: Movement, Networking & Sessions

Flags: primary or unclassified

#### Original subject

```text
Add Enemy Spawn and basic movement
```

#### Original body

```text
- Created EnemyCharacter prefab
- Created EnemyCharacter.cs
- Implemented random direction movement
- Added Enemy spawning to NetworkSpawnManager
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Character/EnemyCharacter.cs
Project Grimhold/Assets/Scripts/Character/EnemyCharacter.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs.meta
```

#### Classification evidence

- subject-action → Features: Add Enemy Spawn and basic movement
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs.meta
- message-term → Movement: movement
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs

#### Diagnostics

- No classification ambiguity detected.

### 6c7fe8654291fb5e288499bdde51d529d0afe2c0

[6c7fe8654291fb5e288499bdde51d529d0afe2c0](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6c7fe8654291fb5e288499bdde51d529d0afe2c0)

Date: 2026-07-18T01:20:16-03:00 · Classification: Documentation (inferred) · Systems: Combat, Inventory & Loot, Documentation

Flags: secondary

#### Original subject

```text
Add player interaction document. Update documentarion
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- artifact-paths → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md, Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md, Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md, Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: interaction
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md

#### Diagnostics

- No classification ambiguity detected.

### 5acd4e3b078609bff634b1db169b5af4ccddfa70

[5acd4e3b078609bff634b1db169b5af4ccddfa70](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5acd4e3b078609bff634b1db169b5af4ccddfa70)

Date: 2026-07-18T01:17:27-03:00 · Classification: Tests (inferred) · Systems: Combat, Build & Tooling, Tests

Flags: secondary

#### Original subject

```text
Update RangedAttackTests.cs
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
```

#### Classification evidence

- artifact-paths → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 745a21750d7c26fb2eacd81a5fa3e8e817e87cb8

[745a21750d7c26fb2eacd81a5fa3e8e817e87cb8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/745a21750d7c26fb2eacd81a5fa3e8e817e87cb8)

Date: 2026-07-18T00:22:46-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
implement synchronized loot pickup system
```

#### Original body

```text
add LootId and typed loot delivery contracts
add LootDefinition support for stable loot identifiers
replace string-based interaction results with typed failure reasons
implement NetworkLootPickup with authoritative reservation, delivery, and despawn
implement PlayerLootReceiver with Fusion lifecycle registration
extend EntityRegistry with loot receiver registration and lookup
preserve multiple capabilities under the same EntityId
prevent duplicate rewards from concurrent interaction requests
restore pickup availability when loot delivery is rejected
add EditMode coverage for loot contracts, receiver state, registry behavior, and interaction results
add loot interaction architecture documentation
add reusable loot pickup prefab and graybox-ready configuration
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/LootPickup.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- subject-action → Features: implement synchronized loot pickup system
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 97e33636ffef67e0fa02514b757127bdbce9d15a

[97e33636ffef67e0fa02514b757127bdbce9d15a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/97e33636ffef67e0fa02514b757127bdbce9d15a)

Date: 2026-07-17T23:29:00-03:00 · Classification: Features (inferred) · Systems: Combat, Abilities, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
Add authoritative loot pickup interaction flow
```

#### Original body

```text
Introduces a new loot interaction contract and transaction pipeline: `LootId`, `LootGrantRequest`, `LootReceiveResult`, `ILootReceiver`, and `IPickup`. `EntityRegistry` now tracks loot receivers, and `InteractionResult` was refactored to use typed failure reasons with success/reject factories.

Adds `NetworkLootPickup` to enforce state-authority pickup handling, grant loot atomically, and despawn only on successful grants, plus `PlayerLootReceiver` as an authoritative per-player loot container. Updates `LootDefinition` to expose `LootId`, expands/edit-fixes related interaction tests, adds dedicated loot tests, and documents the architecture in `Docs/Architecture/LootInteractionArchitecture.md`.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
Project Grimhold/Assets/Prefabs/LootPickup.prefab
Project Grimhold/Assets/Prefabs/LootPickup.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Core/ILootReceiver.cs
Project Grimhold/Assets/Scripts/Core/ILootReceiver.cs.meta
Project Grimhold/Assets/Scripts/Core/IPickup.cs
Project Grimhold/Assets/Scripts/Core/IPickup.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs
Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/LootId.cs
Project Grimhold/Assets/Scripts/Core/LootId.cs.meta
Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs.meta
Project Grimhold/Assets/Scripts/Gameplay.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs.meta
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- subject-action → Features: Add authoritative loot pickup interaction flow
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Abilities: Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/LootPickup.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootGrantRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootId.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootId.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootReceiveResult.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/NetworkLootPickupTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 98dd4a3aac4631c80b2b685b07ee1f5e9d2089c6

[98dd4a3aac4631c80b2b685b07ee1f5e9d2089c6](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/98dd4a3aac4631c80b2b685b07ee1f5e9d2089c6)

Date: 2026-07-17T21:18:23-03:00 · Classification: Features (inferred) · Systems: Inventory & Loot, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Add loot definition catalog and validation tests
```

#### Original body

```text
Introduce a new loot data foundation using ScriptableObjects: `LootDefinition`, `LootDefinitionCatalog`, and supporting enums for category and rarity. Add catalog/definition assets (including a provisional coin entry) to seed content. Include comprehensive EditMode tests covering validation rules, duplicate detection, ID lookup behavior, and cache rebuild logic.
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Loot.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin_provisional.asset.meta
Project Grimhold/Assets/Scripts/Loot.meta
Project Grimhold/Assets/Scripts/Loot/LootCategory.cs
Project Grimhold/Assets/Scripts/Loot/LootCategory.cs.meta
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs.meta
Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs
Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs.meta
Project Grimhold/Assets/Scripts/Loot/LootRarity.cs
Project Grimhold/Assets/Scripts/Loot/LootRarity.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs.meta
```

#### Classification evidence

- subject-action → Features: Add loot definition catalog and validation tests
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/coin\_provisional.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootCategory.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootCategory.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinitionCatalog.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootRarity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootRarity.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs.meta
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionCatalogTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### f66be5b31ef85b6e6ae718cd8d624b5f026936c8

[f66be5b31ef85b6e6ae718cd8d624b5f026936c8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f66be5b31ef85b6e6ae718cd8d624b5f026936c8)

Date: 2026-07-17T20:49:08-03:00 · Classification: Features (inferred) · Systems: Combat, Inventory & Loot, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Add networked player interaction system
```

#### Original body

```text
Introduces a full interaction pipeline with new core interaction contracts and payload types, a resolver, Physics2D target query, and a network controller/config for player interactions. Extends EntityRegistry to track IInteractable entities alongside damageables and support safer register/unregister flows. Updates player prefabs to wire new networked interaction behaviours, adds a PlayerInteractionConfig asset, renames the ScriptableObjects folder to "Scriptable Objects", and adds edit-mode tests for registry interaction handling, resolver logic, and target queries.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scriptable Objects.meta
Project Grimhold/Assets/Scriptable Objects/MeleeAttackConfig.asset
Project Grimhold/Assets/Scriptable Objects/MeleeAttackConfig.asset.meta
Project Grimhold/Assets/Scriptable Objects/PlayerClassCatalog.asset
Project Grimhold/Assets/Scriptable Objects/PlayerClassCatalog.asset.meta
Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset
Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset.meta
Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset
Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset.meta
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Core/IInteractable.cs
Project Grimhold/Assets/Scripts/Core/IInteractable.cs.meta
Project Grimhold/Assets/Scripts/Core/IInteractionTargetQuery.cs
Project Grimhold/Assets/Scripts/Core/IInteractionTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionRequest.cs
Project Grimhold/Assets/Scripts/Core/InteractionRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
Project Grimhold/Assets/Scripts/Core/InteractionResult.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionTarget.cs
Project Grimhold/Assets/Scripts/Core/InteractionTarget.cs.meta
Project Grimhold/Assets/Scripts/Core/InteractionTargetQuery.cs
Project Grimhold/Assets/Scripts/Core/InteractionTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction.meta
Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs
Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/Physics2DInteractionTargetQuery.cs
Project Grimhold/Assets/Scripts/Player/Interaction/Physics2DInteractionTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionConfig.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionConfig.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
```

#### Classification evidence

- subject-action → Features: Add networked player interaction system
- path → Combat: Project Grimhold/Assets/Scriptable Objects/MeleeAttackConfig.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/MeleeAttackConfig.asset.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/RangedAttackConfig.asset.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/PlayerInteractionConfig.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionPresentationEvent.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionResult.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionTarget.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionTarget.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionTargetQuery.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/InteractionTargetQuery.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/InteractionResolver.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/Physics2DInteractionTargetQuery.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/Physics2DInteractionTargetQuery.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionConfig.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionConfig.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs.meta
- message-term → Inventory &amp; Loot: interaction
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/EntityRegistryInteractionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/InteractionResolverTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Physics2DInteractionTargetQueryTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 6dd6739246ab05f6f43b9cbff29c74c2d72470bc

[6dd6739246ab05f6f43b9cbff29c74c2d72470bc](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6dd6739246ab05f6f43b9cbff29c74c2d72470bc)

Date: 2026-07-17T19:51:50-03:00 · Classification: Other Changes (unknown) · Systems: Input, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Latch Interact input and add play mode tests
```

#### Original body

```text
Adds a dedicated Gameplay/Interact action (keyboard E) to the input actions asset and generated wrapper, and updates PlayerInputReader to buffer pending Interact presses so they are consumed exactly once per network input tick. Also enables PlayMode Test Runner and adds edit/play mode coverage to verify interact latching, clearing after consume, and reset behavior when the reader is disabled and re-enabled.
```

#### Changed paths

```text
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/Input/PlayerInputActions.inputactions
Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
Project Grimhold/Assets/Tests/PlayMode.meta
Project Grimhold/Assets/Tests/PlayMode/Editor.meta
Project Grimhold/Assets/Tests/PlayMode/Player.meta
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs.meta
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Input: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs.meta
- message-term → Input: input actions
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/PlayMode/Editor.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Editor.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Player/PlayerInputReaderPlayModeTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 614830255eac2232688130740ca9ec70ae2ece1e

[614830255eac2232688130740ca9ec70ae2ece1e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/614830255eac2232688130740ca9ec70ae2ece1e)

Date: 2026-07-17T19:04:23-03:00 · Classification: Other Changes (unknown) · Systems: Combat

Flags: primary or unclassified

#### Original subject

```text
Stop combat visuals after player defeat
```

#### Original body

```text
Gate damage and attack presentation so they no longer trigger or keep running once the character is dead. Update PlayerDefeatPresenter to manage body/combat visual roots, hide them after the defeat transition plus a configurable delay, and restore their original active state on reset. NetworkPlayer prefab now wires the new visual root references and hide-delay setting.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDefeatPresenter.cs
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
- message-term → Combat: combat

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### b18d814d8dcb4db06d2c98692d1e21d9236a2998

[b18d814d8dcb4db06d2c98692d1e21d9236a2998](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b18d814d8dcb4db06d2c98692d1e21d9236a2998)

Date: 2026-07-17T18:27:20-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add player defeat presenter and death state sync
```

#### Original body

```text
Introduces `PlayerDefeatPresenter` to drive defeat visuals (rotation/fade), hide weapon visuals, and coordinate with combat/damage presenters when a character dies or revives. Updates movement and animator logic to respect defeat state by blocking movement while dead and freezing locomotion animation, and wires the new presenter plus updated combat presenter fields in `NetworkPlayer` prefab.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDefeatPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerDefeatPresenter.cs.meta
```

#### Classification evidence

- subject-action → Features: Add player defeat presenter and death state sync
- message-term → Combat: weapon
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- message-term → Movement: movement
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
- message-term → Art &amp; Animation: animation

#### Diagnostics

- No classification ambiguity detected.

### 52d09a56f71d5fd5a8107196150365c692436b7f

[52d09a56f71d5fd5a8107196150365c692436b7f](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/52d09a56f71d5fd5a8107196150365c692436b7f)

Date: 2026-07-17T17:57:58-03:00 · Classification: Features (inferred) · Systems: Combat, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add damage feedback visuals on player hit
```

#### Original body

```text
Introduce `DamageFeedbackPresenter` under `Character/Presentation` to handle hit feedback by watching `CharacterBase.Health` and triggering a sprite flash plus scale pulse when health drops. The presenter caches/restores original visual state safely, handles delayed network spawn initialization, and is wired into `NetworkPlayer.prefab` with default references and animation curve settings.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Character/Presentation.meta
Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs
Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs.meta
```

#### Classification evidence

- subject-action → Features: Add damage feedback visuals on player hit
- path → Combat: Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/Presentation/DamageFeedbackPresenter.cs.meta
- message-term → Combat: damage
- message-term → Art &amp; Animation: sprite

#### Diagnostics

- No classification ambiguity detected.

### 03921bb1d0a693251efe55540fe4430014daacda

[03921bb1d0a693251efe55540fe4430014daacda](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/03921bb1d0a693251efe55540fe4430014daacda)

Date: 2026-07-17T00:05:07-03:00 · Classification: Features (inferred) · Systems: Combat, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add player combat presentation animations
```

#### Original body

```text
Introduces `PlayerCombatPresenter` to drive procedural attack visuals from combat network events, including 8-way direction snapping, weapon pivot swing animation, sorting changes, and temporary animator facing overrides. Updates player prefabs to wire the presenter and weapon sprite setup for melee/ranged variants, and adjusts sword/staff sprite import settings (pivot/alignment and pixels-per-unit) so weapon visuals align correctly during attacks.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/staves-16x16.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/swords-16x16.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs.meta
```

#### Classification evidence

- subject-action → Features: Add player combat presentation animations
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerCombatPresenter.cs.meta
- message-term → Combat: combat
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
- message-term → Art &amp; Animation: animations

#### Diagnostics

- No classification ambiguity detected.

### afd0fbb68cae0b117781b14c68c2b5f0a4804fd3

[afd0fbb68cae0b117781b14c68c2b5f0a4804fd3](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/afd0fbb68cae0b117781b14c68c2b5f0a4804fd3)

Date: 2026-07-16T22:01:35-03:00 · Classification: Features (inferred) · Systems: Combat

Flags: primary or unclassified

#### Original subject

```text
Add combat visual anchors to NetworkPlayer
```

#### Original body

```text
Updates the NetworkPlayer prefab with a new combat visuals hierarchy (`CombatVisuals` -> `WeaponPivot` -> `WeaponSprite`) and a dedicated `AttackOrigin` transform. `PlayerCombatNetworkController._attackOrigin` now points to the new `AttackOrigin` node instead of the root transform, preparing cleaner separation between attack logic and visual weapon positioning.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
```

#### Classification evidence

- subject-action → Features: Add combat visual anchors to NetworkPlayer
- message-term → Combat: combat

#### Diagnostics

- No classification ambiguity detected.

### 19388f7defd9c062afd332646353c55f5652cc97

[19388f7defd9c062afd332646353c55f5652cc97](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/19388f7defd9c062afd332646353c55f5652cc97)

Date: 2026-07-16T21:36:13-03:00 · Classification: Features (inferred) · Systems: Abilities, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add placeholder RPG item sprite sheets
```

#### Original body

```text
Adds a new placeholder asset pack under `Assets/Placeholder/RPG Items 16x16 Pack 1` with spell book, staves, and swords spritesheets plus Unity `.meta` files. The imported textures are configured as multi-sprite 16x16 sheets, so individual item sprites are ready to use in the project.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/staves-16x16.png
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/staves-16x16.png.meta
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/swords-16x16.png
Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/swords-16x16.png.meta
```

#### Classification evidence

- subject-action → Features: Add placeholder RPG item sprite sheets
- path → Abilities: Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png
- path → Abilities: Project Grimhold/Assets/Placeholder/RPG Items 16x16 Pack 1/spell-books-16x16.png.meta
- message-term → Abilities: spell
- message-term → Art &amp; Animation: sprite

#### Diagnostics

- No classification ambiguity detected.

### d2ea0fe1b49347f0800f3b0fb43c8823adb3ae46

[d2ea0fe1b49347f0800f3b0fb43c8823adb3ae46](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d2ea0fe1b49347f0800f3b0fb43c8823adb3ae46)

Date: 2026-07-16T01:52:31-03:00 · Classification: Features (inferred) · Systems: Movement, Documentation

Flags: primary or unclassified

#### Original subject

```text
Add local camera follow for local player
```

#### Original body

```text
Introduces a presentation-only local camera system with `LocalCameraController` on the main camera and `LocalPlayerCameraBinder` on player prefabs, so each client camera tracks only its input-authority player. Updates network player prefabs to register the new binder in Fusion networked behaviours, adjusts the base player collider setup (Capsule2D to Box2D and motor reference), and documents the architecture/lifecycle decisions for initialization, cleanup, and respawn safety.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Player/Presentation/LocalCameraController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalCameraController.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerCameraBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerCameraBinder.cs.meta
Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add local camera follow for local player
- path → Movement: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### 1048bfbb42c4f3376ef950d797960880df7a4e7e

[1048bfbb42c4f3376ef950d797960880df7a4e7e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1048bfbb42c4f3376ef950d797960880df7a4e7e)

Date: 2026-07-15T22:46:16-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Networking & Sessions, Documentation

Flags: primary or unclassified

#### Original subject

```text
Translate combat docs and expand architecture
```

#### Original body

```text
Standardized XML comments, headers, and inline notes across core combat/network scripts from Spanish to English for consistency. Also heavily expanded `PlayerCombatArchitecture.md` with implementation status, config references, melee/ranged/damage flow breakdowns, acceptance criteria, known limitations, and manual validation steps to better document the current combat pipeline.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: combat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 8d6ed8f5f4c2bab272ff9e00f0793909d82b8e32

[8d6ed8f5f4c2bab272ff9e00f0793909d82b8e32](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8d6ed8f5f4c2bab272ff9e00f0793909d82b8e32)

Date: 2026-07-15T22:30:45-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Movement, Input, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Refine ranged combat targeting and add tests
```

#### Original body

```text
Improves ranged combat behavior across input, attack execution, and projectile hit resolution. Ranged attacks now use aim world position when available, `RangedAttack` validates lazily at execution time, and projectile collision filtering now explicitly ignores owner hits while preserving selected target IDs for damage requests. It also tightens attack config validation by requiring damage to be strictly positive.

Includes supporting Unity asset/prefab tuning for projectile visuals/spawn wiring and adds new EditMode coverage for aim math, ranged attack config validation, ranged attack execution paths, and player input aim/movement preservation.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/CircleBullet.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs
Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
- path → Combat: Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs.meta
- message-term → Combat: combat
- message-term → Movement: movement
- path → Input: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Input: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/PlayerAimMathTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackConfigTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/RangedAttackTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerInputReaderTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 1a6d4724dc16570452cdbaef86b422b87eb8e197

[1a6d4724dc16570452cdbaef86b422b87eb8e197](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1a6d4724dc16570452cdbaef86b422b87eb8e197)

Date: 2026-07-15T21:48:43-03:00 · Classification: Features (inferred) · Systems: Combat, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Add class-based player spawning and join tokens
```

#### Original body

```text
Introduces selectable player classes (Melee/Ranged) from the main menu and carries that choice through Fusion connection tokens using a new join-data codec. Adds a PlayerClassCatalog ScriptableObject to map class IDs to dedicated network prefabs, updates spawning to resolve prefab by class (with validation and host fallback context), and restructures prefabs/scenes accordingly. Also adds edit-mode tests for join-data encoding/decoding and catalog validation/lookups.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerMelee.prefab.meta
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayerRanged.prefab.meta
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/ScriptableObjects/PlayerClassCatalog.asset
Project Grimhold/Assets/ScriptableObjects/PlayerClassCatalog.asset.meta
Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
Project Grimhold/Assets/Scripts/Core/PlayerClassId.cs
Project Grimhold/Assets/Scripts/Core/PlayerClassId.cs.meta
Project Grimhold/Assets/Scripts/Core/PlayerJoinData.cs
Project Grimhold/Assets/Scripts/Core/PlayerJoinData.cs.meta
Project Grimhold/Assets/Scripts/Core/PlayerJoinDataCodec.cs
Project Grimhold/Assets/Scripts/Core/PlayerJoinDataCodec.cs.meta
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/LocalPlayerJoinContext.cs
Project Grimhold/Assets/Scripts/Networking/LocalPlayerJoinContext.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/ScriptableObjects.meta
Project Grimhold/Assets/Scripts/ScriptableObjects/PlayerClassCatalog.cs
Project Grimhold/Assets/Scripts/ScriptableObjects/PlayerClassCatalog.cs.meta
Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs.meta
```

#### Classification evidence

- subject-action → Features: Add class-based player spawning and join tokens
- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LocalPlayerJoinContext.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/LocalPlayerJoinContext.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs
- message-term → UI: menu
- path → Persistence &amp; Backend: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerClassCatalogTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/PlayerJoinDataCodecTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 839518003c69765cdf93240942c49e3b9f0224d8

[839518003c69765cdf93240942c49e3b9f0224d8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/839518003c69765cdf93240942c49e3b9f0224d8)

Date: 2026-07-15T20:53:45-03:00 · Classification: Other Changes (unknown) · Systems: Combat, Movement, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
Refine melee targeting and add combat tests
```

#### Original body

```text
Reworked combat flow to use melee on the network player, including movement-facing attack direction, stricter dependency/config validation, and editor gizmo visualization for melee range. Updated `MeleeAttack` and `Physics2DAttackTargetQuery` to normalize direction, defensively exclude attacker IDs, deduplicate targets correctly, keep closest hits per entity, and apply max-target limits after deduplication. Added EditMode tests covering melee execution rules and 2D target-query filtering/order behavior, plus placeholder sword sprite assets wired into the player prefab.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/sword_blades_spritesheet.png
Project Grimhold/Assets/Placeholder/sword_blades_spritesheet.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs.meta
Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Tests.meta
Project Grimhold/Assets/Tests/EditMode.meta
Project Grimhold/Assets/Tests/EditMode/Editor.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs.meta
```

#### Classification evidence

- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackGizmoDrawer.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs.meta
- message-term → Combat: combat
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement.meta
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs.meta
- message-term → Movement: movement
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/MeleeAttackTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/Physics2DAttackTargetQueryTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Player/Movement/PlayerMovementRulesTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 2bfdc0224b972d6de34c9ba38d0e79bbd6db3108

[2bfdc0224b972d6de34c9ba38d0e79bbd6db3108](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2bfdc0224b972d6de34c9ba38d0e79bbd6db3108)

Date: 2026-07-15T02:12:48-03:00 · Classification: Documentation (inferred) · Systems: Combat, Movement, Networking & Sessions, Documentation

Flags: secondary

#### Original subject

```text
Clarify PlayerCombat architecture and projectile rules
```

#### Original body

```text
Refines the PlayerCombat architecture document to emphasize deterministic, authoritative multiplayer behavior with Photon Fusion. Clarifies AttackRequest Origin semantics and that PlayerCombatNetworkController processes input only on State Authority. Reworks the NetworkProjectile section into an authority-first design (kinematic movement, continuous collision casts, owner filtering, single-impact guarantee, range/lifetime clamping, collider scaling and physics sync). Adjusts the network authority table and rewrites physics/synchronization rules for explicit authoritative physics and transform/physics alignment.
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- artifact-paths → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: projectile
- message-term → Movement: movement
- message-term → Networking &amp; Sessions: Photon Fusion
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### c76b3cc2f9ad1fb9ddbd1f11d1d06a340fcdccb4

[c76b3cc2f9ad1fb9ddbd1f11d1d06a340fcdccb4](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c76b3cc2f9ad1fb9ddbd1f11d1d06a340fcdccb4)

Date: 2026-07-15T01:50:38-03:00 · Classification: Features (inferred) · Systems: Combat, Networking & Sessions, Persistence & Backend, Build & Tooling, Documentation

Flags: primary or unclassified

#### Original subject

```text
Add networked ranged attack system
```

#### Original body

```text
Implement a complete ranged combat system with network-synchronized projectiles using Fusion. Includes RangedAttack strategy, FusionProjectileSpawner for network-authoritative spawning, and NetworkProjectile for client-side simulation with collision detection. Adds supporting data structures (ProjectileSpawnRequest/Result) and IProjectileSpawner interface for loose coupling. Integrates with existing NetworkPlayer prefab and updates PlaceholderProjectile with physics components. Also enables development mode in Windows build profile for debugging.
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/CircleBullet.png
Project Grimhold/Assets/Placeholder/CircleBullet.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset
Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs.meta
Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs.meta
Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs.meta
Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs
Project Grimhold/Assets/Scripts/Core/IProjectileSpawner.cs
Project Grimhold/Assets/Scripts/Core/IProjectileSpawner.cs.meta
Project Grimhold/Assets/Scripts/Core/ProjectileSpawnRequest.cs
Project Grimhold/Assets/Scripts/Core/ProjectileSpawnRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/ProjectileSpawnResult.cs
Project Grimhold/Assets/Scripts/Core/ProjectileSpawnResult.cs.meta
Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
```

#### Classification evidence

- subject-action → Features: Add networked ranged attack system
- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
- path → Combat: Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset
- path → Combat: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/NetworkProjectile.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttack.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/ProjectileSpawnRequest.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/ProjectileSpawnRequest.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/ProjectileSpawnResult.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/ProjectileSpawnResult.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: combat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Combat/FusionProjectileSpawner.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### e7195ff389a9aefe621d09846d63e3556f5e33b8

[e7195ff389a9aefe621d09846d63e3556f5e33b8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e7195ff389a9aefe621d09846d63e3556f5e33b8)

Date: 2026-07-15T00:22:31-03:00 · Classification: Features (inferred) · Systems: Combat

Flags: primary or unclassified

#### Original subject

```text
Add shared attack configs
```

#### Original body

```text
Introduces a common `AttackConfig` base and adds ranged attack configuration support. Melee configs now inherit shared validation, melee attack components reject invalid configs, and the new ranged config/prefab assets are added under the updated ScriptableObjects location.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab.meta
Project Grimhold/Assets/ScriptableObjects.meta
Project Grimhold/Assets/ScriptableObjects/MeleeAttackConfig.asset
Project Grimhold/Assets/ScriptableObjects/MeleeAttackConfig.asset.meta
Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset
Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset.meta
Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs
Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs.meta
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs
Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs.meta
```

#### Classification evidence

- subject-action → Features: Add shared attack configs
- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab
- path → Combat: Project Grimhold/Assets/Prefabs/PlaceholderProjectile.prefab.meta
- path → Combat: Project Grimhold/Assets/ScriptableObjects/MeleeAttackConfig.asset
- path → Combat: Project Grimhold/Assets/ScriptableObjects/MeleeAttackConfig.asset.meta
- path → Combat: Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset
- path → Combat: Project Grimhold/Assets/ScriptableObjects/RangedAttackConfig.asset.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/AttackConfig.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/RangedAttackConfig.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 09729c984d120d3fc489bf5cc4cf5bd0b58169a2

[09729c984d120d3fc489bf5cc4cf5bd0b58169a2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/09729c984d120d3fc489bf5cc4cf5bd0b58169a2)

Date: 2026-07-14T22:17:45-03:00 · Classification: Features (inferred) · Systems: Combat, Networking & Sessions, Documentation

Flags: primary or unclassified

#### Original subject

```text
Add melee combat target resolution
```

#### Original body

```text
Introduces a melee combat pipeline with `MeleeAttack`, `Physics2DAttackTargetQuery`, `DamageResolver`, and an `EntityRegistry` on the Fusion runner. Updates the network player prefab and character base to register colliders/entities, adds the melee config asset, and refreshes the combat contracts and architecture docs.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs
Project Grimhold/Assets/Scripts/Combat.meta
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs.meta
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs.meta
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs.meta
Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs
Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs.meta
Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackTarget.cs
Project Grimhold/Assets/Scripts/Core/AttackTarget.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackTargetQuery.cs
Project Grimhold/Assets/Scripts/Core/AttackTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageResolvedEvent.cs
Project Grimhold/Assets/Scripts/Core/DamageResolvedEvent.cs.meta
Project Grimhold/Assets/Scripts/Core/IAttackTargetQuery.cs
Project Grimhold/Assets/Scripts/Core/IAttackTargetQuery.cs.meta
Project Grimhold/Assets/Scripts/Core/IDamageResolver.cs
Project Grimhold/Assets/Scripts/Core/IDamageResolver.cs.meta
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/ScriptableObjects.meta
Project Grimhold/Assets/Scripts/ScriptableObjects/MeleeAttackConfig.asset
Project Grimhold/Assets/Scripts/ScriptableObjects/MeleeAttackConfig.asset.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- subject-action → Features: Add melee combat target resolution
- path → Combat: Project Grimhold/Assets/Scripts/Combat.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/EntityRegistry.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttack.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/MeleeAttackConfig.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs
- path → Combat: Project Grimhold/Assets/Scripts/Combat/Physics2DAttackTargetQuery.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackTarget.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackTarget.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackTargetQuery.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackTargetQuery.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageResolvedEvent.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageResolvedEvent.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/ScriptableObjects/MeleeAttackConfig.asset
- path → Combat: Project Grimhold/Assets/Scripts/ScriptableObjects/MeleeAttackConfig.asset.meta
- message-term → Combat: combat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: architecture docs

#### Diagnostics

- No classification ambiguity detected.

### 277fe03bc1919411b7237c92049a4df860815b5a

[277fe03bc1919411b7237c92049a4df860815b5a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/277fe03bc1919411b7237c92049a4df860815b5a)

Date: 2026-07-14T21:46:55-03:00 · Classification: Features (inferred) · Systems: Combat, Input, Documentation

Flags: primary or unclassified

#### Original subject

```text
Add combat attack contracts
```

#### Original body

```text
Adds shared attack enums, request/result/event types, and the `IAttack` contract, plus a new `PlayerCombatNetworkController` wired into `NetworkPlayer`. Updates the combat architecture doc to match the expanded interface.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Core/AttackFailureReason.cs
Project Grimhold/Assets/Scripts/Core/AttackFailureReason.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs
Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackPerformedEvent.cs
Project Grimhold/Assets/Scripts/Core/AttackPerformedEvent.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackRequest.cs
Project Grimhold/Assets/Scripts/Core/AttackRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackResult.cs
Project Grimhold/Assets/Scripts/Core/AttackResult.cs.meta
Project Grimhold/Assets/Scripts/Core/AttackType.cs
Project Grimhold/Assets/Scripts/Core/AttackType.cs.meta
Project Grimhold/Assets/Scripts/Core/IAttack.cs
Project Grimhold/Assets/Scripts/Core/IAttack.cs.meta
Project Grimhold/Assets/Scripts/Player/Combat.meta
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs.meta
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- subject-action → Features: Add combat attack contracts
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackFailureReason.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackFailureReason.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackPerformedEvent.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackPerformedEvent.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackRequest.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackRequest.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackResult.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackResult.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackType.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/AttackType.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs.meta
- message-term → Combat: combat
- path → Input: Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs
- path → Input: Project Grimhold/Assets/Scripts/Core/AttackInputMode.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: architecture doc

#### Diagnostics

- No classification ambiguity detected.

### be9103e1f91cb1de8888e3b9e6f2094c996c8ec5

[be9103e1f91cb1de8888e3b9e6f2094c996c8ec5](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/be9103e1f91cb1de8888e3b9e6f2094c996c8ec5)

Date: 2026-07-14T20:57:18-03:00 · Classification: Features (inferred) · Systems: Combat, Networking & Sessions, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add networked damage system and character architecture
```

#### Original body

```text
Introduces core gameplay infrastructure for handling damage in a networked multiplayer environment:

- Added damage system: DamageRequest, DamageResult, DamageFailureReason, and DamageType for structured damage handling
- Added CharacterBase abstract class implementing networked health and damage system with state authority validation
- Added core interfaces: IEntity, ICharacter, and IDamageable to define entity contracts
- Added EntityId value type for stable entity identification independent of network infrastructure
- Added NetworkEnemy prefab configured with Fusion networking, 2D physics, and animation components
- Cleared render pipeline global settings runtime list
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkEnemy.prefab.meta
Project Grimhold/Assets/Scripts/Character.meta
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs.meta
Project Grimhold/Assets/Scripts/Core.meta
Project Grimhold/Assets/Scripts/Core/DamageFailureReason.cs
Project Grimhold/Assets/Scripts/Core/DamageFailureReason.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
Project Grimhold/Assets/Scripts/Core/DamageRequest.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageResult.cs
Project Grimhold/Assets/Scripts/Core/DamageResult.cs.meta
Project Grimhold/Assets/Scripts/Core/DamageType.cs
Project Grimhold/Assets/Scripts/Core/DamageType.cs.meta
Project Grimhold/Assets/Scripts/Core/EntityId.cs
Project Grimhold/Assets/Scripts/Core/EntityId.cs.meta
Project Grimhold/Assets/Scripts/Core/ICharacter.cs
Project Grimhold/Assets/Scripts/Core/ICharacter.cs.meta
Project Grimhold/Assets/Scripts/Core/IDamageable.cs
Project Grimhold/Assets/Scripts/Core/IDamageable.cs.meta
Project Grimhold/Assets/Scripts/Core/IEntity.cs
Project Grimhold/Assets/Scripts/Core/IEntity.cs.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
```

#### Classification evidence

- subject-action → Features: Add networked damage system and character architecture
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageFailureReason.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageFailureReason.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageRequest.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageRequest.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageResult.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageResult.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageType.cs
- path → Combat: Project Grimhold/Assets/Scripts/Core/DamageType.cs.meta
- message-term → Combat: damage
- message-term → Networking &amp; Sessions: networking
- message-term → Art &amp; Animation: animation

#### Diagnostics

- No classification ambiguity detected.

### 84d551cdbcc497cc232799ef8f487a236d50bf92

[84d551cdbcc497cc232799ef8f487a236d50bf92](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/84d551cdbcc497cc232799ef8f487a236d50bf92)

Date: 2026-07-14T20:54:23-03:00 · Classification: Features (inferred) · Systems: Input

Flags: primary or unclassified

#### Original subject

```text
Add primary attack and aim input
```

#### Original body

```text
Rename the fire button to `PrimaryAttack`, add a pointer position action, and thread the new aim data through the player input reader and network input. The reader now captures world-space aim position instead of look rotation and keeps button presses latched until Fusion consumes them.
```

#### Changed paths

```text
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/Input/PlayerInputActions.inputactions
Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs
```

#### Classification evidence

- subject-action → Features: Add primary attack and aim input
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs

#### Diagnostics

- No classification ambiguity detected.

### 02766859178f9a8fe8048143d11be7b7c98aa623

[02766859178f9a8fe8048143d11be7b7c98aa623](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/02766859178f9a8fe8048143d11be7b7c98aa623)

Date: 2026-07-14T20:38:38-03:00 · Classification: Documentation (inferred) · Systems: Combat, Inventory & Loot, Networking & Sessions, Documentation

Flags: secondary

#### Original subject

```text
Expand combat contract architecture doc
```

#### Original body

```text
Updates the base architecture spec to cover melee and ranged attacks, shared damage resolution, projectile spawning, entity IDs, interaction, and presentation events. Also clarifies Photon Fusion authority rules, combat configuration, and integration requirements for enemy melee damage handling.
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- artifact-paths → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Combat: combat
- message-term → Inventory &amp; Loot: interaction
- message-term → Networking &amp; Sessions: Photon Fusion
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Documentation: architecture doc

#### Diagnostics

- No classification ambiguity detected.

### acfd92d7f10b3ace04f0ef11cc6088cd87ba156a

[acfd92d7f10b3ace04f0ef11cc6088cd87ba156a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/acfd92d7f10b3ace04f0ef11cc6088cd87ba156a)

Date: 2026-07-14T20:24:10-03:00 · Classification: Documentation (inferred) · Systems: Documentation

Flags: secondary

#### Original subject

```text
Update Project_Grimhold_Contratos_Base_v1.md
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- artifact-paths → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md

#### Diagnostics

- No classification ambiguity detected.

### 3da5fb53db1756a80d3895dab94ef96e92897e4e

[3da5fb53db1756a80d3895dab94ef96e92897e4e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3da5fb53db1756a80d3895dab94ef96e92897e4e)

Date: 2026-07-14T20:21:26-03:00 · Classification: Documentation (inferred) · Systems: Combat, Inventory & Loot, Networking & Sessions, Documentation

Flags: secondary

#### Original subject

```text
Add base contracts architecture specification
```

#### Original body

```text
Add comprehensive specification document for Project Grimhold's base contracts, interfaces, and structures. Defines the architecture for game systems including character, attack, damage, and interaction handling, with principles for network synchronization using Photon Fusion. This establishes the foundation for all gameplay systems.
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
```

#### Classification evidence

- artifact-paths → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- message-term → Combat: damage
- message-term → Inventory &amp; Loot: interaction
- message-term → Networking &amp; Sessions: Photon Fusion
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md

#### Diagnostics

- No classification ambiguity detected.

### 8bf0f161fd66cc9d5676533d3c56f1017ac66189

[8bf0f161fd66cc9d5676533d3c56f1017ac66189](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8bf0f161fd66cc9d5676533d3c56f1017ac66189)

Date: 2026-07-14T20:01:18-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions, UI, Input

Flags: primary or unclassified

#### Original subject

```text
Hotfix 2
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → Input: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 9d0c66f7e3c6f6fa577e3eb1d2b77797ad82c37d

[9d0c66f7e3c6f6fa577e3eb1d2b77797ad82c37d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9d0c66f7e3c6f6fa577e3eb1d2b77797ad82c37d)

Date: 2026-07-14T00:18:58-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions, UI

Flags: primary or unclassified

#### Original subject

```text
hotfix
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 98126e9e1469fb2c59b9699a8cefa09c5bb87bd7

[98126e9e1469fb2c59b9699a8cefa09c5bb87bd7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/98126e9e1469fb2c59b9699a8cefa09c5bb87bd7)

Date: 2026-07-13T23:47:58-03:00 · Classification: Refactoring (inferred) · Systems: Inventory & Loot, Networking & Sessions, UI

Flags: primary or unclassified

#### Original subject

```text
Refactor network spawn system to support multiple spawn groups
```

#### Original body

```text
Replace NetworkPlayerSpawner with NetworkSpawnManager to support multiple spawn group types (players, enemies, loot, NPCs, bosses, misc). Updated FusionSessionLauncher to remove hardcoded dependencies and use DontDestroyOnLoad. Fixed gameplay scene reference in LobbyMenuController and added UI input blocking during network operations in MainMenuController. Scene updates include separate spawn point groups for players and crystals.
```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/LobbyMenuController.cs
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs
Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs.meta
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- subject-action → Refactoring: Refactor network spawn system to support multiple spawn groups
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs.meta
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs
- message-term → UI: UI

#### Diagnostics

- No classification ambiguity detected.

### c46f6e5cb8710adb6e6ce214fc90d3aa743caf2a

[c46f6e5cb8710adb6e6ce214fc90d3aa743caf2a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c46f6e5cb8710adb6e6ce214fc90d3aa743caf2a)

Date: 2026-07-13T23:12:15-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions, UI, World & Visibility

Flags: primary or unclassified

#### Original subject

```text
Improve lobby flow and session initialization
```

#### Original body

```text
Refactor networking and lobby flow with better error handling and UI feedback. Changes include:

- Add lobby status text UI element for clients waiting for game start
- Rename LobbyMenu to LobbyMenuController and add button/status element references
- Improve MainMenuController to show lobby panel on successful session join
- Update FusionSessionLauncher to return bool and add try-catch-finally for better error handling
- Change default resolution to 960x540 for development
- Use button interactability instead of panel visibility for better UX during connection attempts
```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/LobbyMenuController.cs
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- message-term → Networking &amp; Sessions: lobby
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs
- message-term → UI: UI
- message-term → World &amp; Visibility: visibility

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 64e5031562fc891a37d65d96fada1ade5438d306

[64e5031562fc891a37d65d96fada1ade5438d306](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/64e5031562fc891a37d65d96fada1ade5438d306)

Date: 2026-07-13T22:42:04-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions, UI, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Basic Lobby UI
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Player.prefab
Project Grimhold/Assets/Prefabs/Player.prefab.meta
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Prefabs/Systems.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scenes/Gameplay.unity.meta
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scripts/LobbyMenuController.cs
Project Grimhold/Assets/Scripts/LobbyMenuController.cs.meta
Project Grimhold/Assets/Scripts/MainMenuController.cs
Project Grimhold/Assets/Scripts/MainMenuController.cs.meta
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/ProjectSettings/EditorBuildSettings.asset
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/LobbyMenuController.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- message-term → Networking &amp; Sessions: Lobby
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scripts/LobbyMenuController.cs
- path → UI: Project Grimhold/Assets/Scripts/LobbyMenuController.cs.meta
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs
- path → UI: Project Grimhold/Assets/Scripts/MainMenuController.cs.meta
- message-term → UI: UI
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorBuildSettings.asset

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 708aed87fa9a5fc620596c31c043fe29cb9201d7

[708aed87fa9a5fc620596c31c043fe29cb9201d7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/708aed87fa9a5fc620596c31c043fe29cb9201d7)

Date: 2026-07-13T22:13:42-03:00 · Classification: Other Changes (unknown) · Systems: UI

Flags: primary or unclassified

#### Original subject

```text
Basic UI
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/MainMenu.unity
Project Grimhold/Assets/Scenes/MainMenu.unity.meta
Project Grimhold/Assets/Scenes/SampleScene.unity
```

#### Classification evidence

- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity
- path → UI: Project Grimhold/Assets/Scenes/MainMenu.unity.meta
- message-term → UI: UI

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### f37144e0008da57cd9230eefee572390c78057db

[f37144e0008da57cd9230eefee572390c78057db](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f37144e0008da57cd9230eefee572390c78057db)

Date: 2026-07-13T01:08:40-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, World & Visibility, Art & Animation, Documentation

Flags: primary or unclassified

#### Original subject

```text
add synchronized movement and idle animations
```

#### Original body

```text
Return the actual applied displacement from the kinematic movement motor.
Add networked facing direction and movement state to the player controller.
Preserve the last valid facing direction while the player is idle.
Determine the movement state from the displacement applied after collision resolution.
Add a decoupled animator view for updating movement parameters.
Configure directional idle and movement blend trees.
Add eight-direction walk animations using two-frame looping clips.
Adjust animation timing to better match the player movement speed.
```

#### Changed paths

```text
Project Grimhold/Assets/Animations.meta
Project Grimhold/Assets/Animations/Player.meta
Project Grimhold/Assets/Animations/Player/Idle.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_E.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_E.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_N.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_N.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_NE.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_NE.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_NW.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_NW.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_S.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_S.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_SE.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_SE.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_SW.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_SW.anim.meta
Project Grimhold/Assets/Animations/Player/Idle/Idle_W.anim
Project Grimhold/Assets/Animations/Player/Idle/Idle_W.anim.meta
Project Grimhold/Assets/Animations/Player/Player.controller
Project Grimhold/Assets/Animations/Player/Player.controller.meta
Project Grimhold/Assets/Animations/Player/Walk.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_E.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_E.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_N.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_N.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_NE.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_NE.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_NW.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_NW.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_S.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_S.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_SE.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_SE.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_SW.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_SW.anim.meta
Project Grimhold/Assets/Animations/Player/Walk/Walk_W.anim
Project Grimhold/Assets/Animations/Player/Walk/Walk_W.anim.meta
Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters_by_AxulArt.meta
Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters_by_AxulArt/Small-8-Direction-Characters_by_AxulArt.png
Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters_by_AxulArt/Small-8-Direction-Characters_by_AxulArt.png.meta
Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters_by_AxulArt/license.txt
Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters_by_AxulArt/license.txt.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demon_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demon_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devilHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devilHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devil_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devil_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragon_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragon_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHeroHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHeroHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHero_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHero_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghostHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghostHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghost_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghost_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKingHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKingHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKing_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKing_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblin_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblin_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblinHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblinHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblin_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblin_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knightHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knightHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knight_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knight_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHeroHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHeroHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHero_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHero_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancerHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancerHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancer_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancer_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orcHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orcHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orc_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orc_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeletonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeletonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeleton_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeleton_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slime_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slime_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slimeball_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slimeball_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlockHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlockHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlock_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlock_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/weapons_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/weapons_.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation.meta
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs.meta
Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- subject-action → Features: add synchronized movement and idle animations
- path → Combat: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → Combat: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- path → Movement: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- message-term → Movement: movement
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_E.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_E.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_N.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_N.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NE.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_NW.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_S.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_S.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SE.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_SW.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_W.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Idle/Idle\_W.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Player.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Player.controller.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_E.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_E.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_N.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_N.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NE.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_NW.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_S.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_S.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SE.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SE.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SW.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_SW.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_W.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Player/Walk/Walk\_W.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters\_by\_AxulArt.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters\_by\_AxulArt/Small-8-Direction-Characters\_by\_AxulArt.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters\_by\_AxulArt/Small-8-Direction-Characters\_by\_AxulArt.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters\_by\_AxulArt/license.txt
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/Small-8-Direction-Characters\_by\_AxulArt/license.txt.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scripts/Player/Presentation/PlayerAnimatorView.cs.meta
- message-term → Art &amp; Animation: animations
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### 83da80cd02184c140caeacb664db2be9f701a78d

[83da80cd02184c140caeacb664db2be9f701a78d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/83da80cd02184c140caeacb664db2be9f701a78d)

Date: 2026-07-12T21:42:03-03:00 · Classification: Features (inferred) · Systems: Combat, Movement, World & Visibility, Art & Animation, Build & Tooling, Documentation

Flags: primary or unclassified

#### Original subject

```text
Added placeholder dungeon sprite sheets
```

#### Original body

```text
Added placeholder dungeon sprite sheets and updated `NetworkPlayer` to use the new sprite reference with a neutral white tint.
```

#### Changed paths

```text
Project Grimhold/AGENTS.md
Project Grimhold/Assets/Placeholder.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demon_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/demon_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devilHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devilHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devil_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/devil_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragon_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/dragon_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHeroHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHeroHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHero_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/fHero_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghostHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghostHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghost_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/ghost_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKingHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKingHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKing_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblinKing_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblin_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/goblin_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblinHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblinHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblin_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/hobgoblin_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knightHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knightHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knight_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/knight_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHeroHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHeroHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHero_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/mHero_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancerHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancerHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancer_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/necromancer_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orcHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orcHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orc_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/orc_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeletonHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeletonHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeleton_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/skeleton_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slime_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slime_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slimeball_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/slimeball_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlockHand_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlockHand_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlock_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/warlock_.png.meta
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/weapons_.png
Project Grimhold/Assets/Placeholder/dungeonSprites_ v1.5/weapons_.png.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
```

#### Classification evidence

- subject-action → Features: Added placeholder dungeon sprite sheets
- path → Combat: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → Combat: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- path → Movement: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → World &amp; Visibility: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- message-term → World &amp; Visibility: dungeon
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/demon\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devilHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/devil\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/dragon\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHeroHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/fHero\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghostHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/ghost\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKingHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblinKing\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/goblin\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblinHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/hobgoblin\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knightHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/knight\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHeroHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/mHero\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancerHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/necromancer\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orcHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/orc\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeletonHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/skeleton\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slime\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/slimeball\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlockHand\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/warlock\_.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png
- path → Art &amp; Animation: Project Grimhold/Assets/Placeholder/dungeonSprites\_ v1.5/weapons\_.png.meta
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### ea9ef93d67e98c83f9c03ebd56a53d781ab9ad1b

[ea9ef93d67e98c83f9c03ebd56a53d781ab9ad1b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ea9ef93d67e98c83f9c03ebd56a53d781ab9ad1b)

Date: 2026-07-12T21:34:55-03:00 · Classification: Features (inferred) · Systems: Movement, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
Add 2D kinematic movement and network controller
```

#### Original body

```text
Add Kinematic2DMovementMotor and PlayerMovementNetworkController to handle kinematic 2D movement and Fusion-driven network input. New files: Movement folder, Kinematic2DMovementMotor.cs (+meta), PlayerMovementNetworkController.cs (+meta). Update NetworkPlayer.prefab: changed sprite tint, set tag to Player, added NetworkTransform, Rigidbody2D, CapsuleCollider2D, movement components and networked behaviours. SampleScene updated with multiple square GameObjects (sprite + colliders) and scene roots added. TagManager updated (serializedVersion bump) to add a WorldCollision layer and rendering layer entries. Overall prepares player prefab and scene for server-authoritative kinematic movement.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scenes/SampleScene 1.unity
Project Grimhold/Assets/Scripts/Player/Movement.meta
Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs
Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs.meta
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs.meta
Project Grimhold/ProjectSettings/ProjectSettings.asset
Project Grimhold/ProjectSettings/TagManager.asset
```

#### Classification evidence

- subject-action → Features: Add 2D kinematic movement and network controller
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement.meta
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/Kinematic2DMovementMotor.cs.meta
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs.meta
- message-term → Movement: movement
- message-term → Art &amp; Animation: sprite

#### Diagnostics

- No classification ambiguity detected.

### 98f5994c5e519eec6ab950b88b9619eaab2bf482

[98f5994c5e519eec6ab950b88b9619eaab2bf482](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/98f5994c5e519eec6ab950b88b9619eaab2bf482)

Date: 2026-07-10T20:19:30-03:00 · Classification: Architecture & Infrastructure (inferred) · Systems: Persistence & Backend, Input, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Add Windows build profile; update URP & project
```

#### Original body

```text
Adds a Windows build profile asset and meta under Assets/Settings/Build Profiles. Updates URP and UniversalRenderPipeline global settings (prefilter flags and runtime RIDs) and enables a filter override in DefaultVolumeProfile. Adjusts EditorBuildSettings to point to SampleScene 1.unity, adds a preloaded input actions asset, enables Standalone static batching, and turns on UnityConnect.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

#### Changed paths

```text
Project Grimhold/Assets/DefaultVolumeProfile.asset
Project Grimhold/Assets/Settings/Build Profiles.meta
Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
Project Grimhold/Assets/Settings/Build Profiles/Windows.asset.meta
Project Grimhold/Assets/Settings/UniversalRP.asset
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/ProjectSettings/EditorBuildSettings.asset
Project Grimhold/ProjectSettings/ProjectSettings.asset
Project Grimhold/ProjectSettings/UnityConnectSettings.asset
```

#### Classification evidence

- subject-action → Architecture &amp; Infrastructure: Add Windows build profile
- path → Persistence &amp; Backend: Project Grimhold/Assets/DefaultVolumeProfile.asset
- path → Persistence &amp; Backend: Project Grimhold/Assets/Settings/Build Profiles.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Persistence &amp; Backend: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset.meta
- message-term → Input: input actions
- path → Build &amp; Tooling: Project Grimhold/Assets/Settings/Build Profiles.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Settings/Build Profiles/Windows.asset.meta
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorBuildSettings.asset

#### Diagnostics

- No classification ambiguity detected.

### 0e32fc42809439ad9d817f523344d55c4dbcdd41

[0e32fc42809439ad9d817f523344d55c4dbcdd41](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0e32fc42809439ad9d817f523344d55c4dbcdd41)

Date: 2026-07-10T19:35:20-03:00 · Classification: Features (inferred) · Systems: Networking & Sessions

Flags: primary or unclassified

#### Original subject

```text
Add network player spawning
```

#### Original body

```text
Adds a `NetworkPlayerSpawner` callback to spawn and despawn player objects on the server, wires it into `FusionSessionLauncher`, and updates the scene with spawn points and the player prefab reference.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab.meta
Project Grimhold/Assets/Scenes/SampleScene 1.unity
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs
Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs.meta
```

#### Classification evidence

- subject-action → Features: Add network player spawning
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkPlayerSpawner.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 82422649fd421ac7e3235348baba31f6cebfeb2d

[82422649fd421ac7e3235348baba31f6cebfeb2d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/82422649fd421ac7e3235348baba31f6cebfeb2d)

Date: 2026-07-10T19:09:19-03:00 · Classification: Features (inferred) · Systems: Movement, Networking & Sessions, Input, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Add input system and networking infrastructure
```

#### Original body

```text
Implement Unity Input System with Photon Fusion networking support. Adds player input handling, network session management, player spawning system, and sample multiplayer scene. Includes input action definitions bound to WASD movement controls.
```

#### Changed paths

```text
Project Grimhold/Assets/Input.meta
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/Input/PlayerInputActions.cs.meta
Project Grimhold/Assets/Input/PlayerInputActions.inputactions
Project Grimhold/Assets/Input/PlayerInputActions.inputactions.meta
Project Grimhold/Assets/Scenes/SampleScene 1.unity
Project Grimhold/Assets/Scenes/SampleScene 1.unity.meta
Project Grimhold/Assets/Scripts.meta
Project Grimhold/Assets/Scripts/Networking.meta
Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs.meta
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkRunnerCallbacksAdapter.cs
Project Grimhold/Assets/Scripts/Networking/NetworkRunnerCallbacksAdapter.cs.meta
Project Grimhold/Assets/Scripts/Player.meta
Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs
Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs.meta
Project Grimhold/ProjectSettings/EditorBuildSettings.asset
```

#### Classification evidence

- subject-action → Features: Add input system and networking infrastructure
- message-term → Movement: movement
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkRunnerCallbacksAdapter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkRunnerCallbacksAdapter.cs.meta
- message-term → Networking &amp; Sessions: networking
- path → Input: Project Grimhold/Assets/Input.meta
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs.meta
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.inputactions.meta
- path → Input: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs
- path → Input: Project Grimhold/Assets/Scripts/Networking/FusionInputProvider.cs.meta
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputButton.cs.meta
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerInputReader.cs.meta
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs
- path → Input: Project Grimhold/Assets/Scripts/Player/PlayerNetworkInput.cs.meta
- message-term → Input: input system
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorBuildSettings.asset

#### Diagnostics

- No classification ambiguity detected.

### 8ea0778e475930ac6f9959b4b110211f7a87e145

[8ea0778e475930ac6f9959b4b110211f7a87e145](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8ea0778e475930ac6f9959b4b110211f7a87e145)

Date: 2026-07-10T19:29:05-03:00 · Classification: Tests (inferred) · Systems: Tests

Flags: secondary

#### Original subject

```text
Add Test Objects
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Arrow.cs
Project Grimhold/Assets/Arrow.cs.meta
Project Grimhold/Assets/Arrow.prefab
Project Grimhold/Assets/Arrow.prefab.meta
Project Grimhold/Assets/ArrowTrap.cs
Project Grimhold/Assets/ArrowTrap.cs.meta
Project Grimhold/Assets/Scenes/SampleScene.unity
```

#### Classification evidence

- subject-action → Tests: Add Test
- message-term → Tests: Test

#### Diagnostics

- No classification ambiguity detected.

### 4d718a6b1ca8183ff78082aaff416e65ea6628f8

[4d718a6b1ca8183ff78082aaff416e65ea6628f8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4d718a6b1ca8183ff78082aaff416e65ea6628f8)

Date: 2026-07-09T21:28:29-03:00 · Classification: Features (inferred) · Systems: Movement, Inventory & Loot, Networking & Sessions, UI, Art & Animation, Input, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Add Fusion Intro demo and enable hub mode networking
```

#### Original body

```text
Enable hub mode (HubMode: 1) in Photon Fusion configuration for centralized networking architecture. Add complete Fusion-Intro-Shared demo package including networked player movement, color interaction mechanics, prefabs, materials, and example scene.
```

#### Changed paths

```text
Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion
Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapGreen.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapGreen.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapRed.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapRed.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PhotonCap.fbx
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PhotonCap.fbx.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PlayerCapsule.fbx
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PlayerCapsule.fbx.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/PhotonDummy.prefab
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/PhotonDummy.prefab.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundTexture.png
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundTexture.png.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectTexture.png
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectTexture.png.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed_URP.mat
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed_URP.mat.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/NetworkRunner.prefab
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/NetworkRunner.prefab.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/PlayerObject.prefab
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/PlayerObject.prefab.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/FusionIntroShared.unity
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/FusionIntroShared.unity.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorDeposit.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorDeposit.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorPicker.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorPicker.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorResult.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorResult.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IInteractable.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IInteractable.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerColor.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerColor.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerInteract.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerInteract.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerSpawner.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerSpawner.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs.meta
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build_info.txt
Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build_info.txt.meta
Project Grimhold/Assets/Photon/FusionMenu/Editor.meta
Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef
Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef.meta
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs
Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset
Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset.meta
Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity
Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs
Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset
Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset.meta
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf
Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf.meta
Project Grimhold/Assets/TextMesh Pro.meta
Project Grimhold/Assets/TextMesh Pro/Fonts.meta
Project Grimhold/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt
Project Grimhold/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt.meta
Project Grimhold/Assets/TextMesh Pro/Fonts/LiberationSans.ttf
Project Grimhold/Assets/TextMesh Pro/Fonts/LiberationSans.ttf.meta
Project Grimhold/Assets/TextMesh Pro/Resources.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset.meta
Project Grimhold/Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt
Project Grimhold/Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt.meta
Project Grimhold/Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt
Project Grimhold/Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Style Sheets.meta
Project Grimhold/Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset
Project Grimhold/Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset.meta
Project Grimhold/Assets/TextMesh Pro/Resources/TMP Settings.asset
Project Grimhold/Assets/TextMesh Pro/Resources/TMP Settings.asset.meta
Project Grimhold/Assets/TextMesh Pro/Shaders.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl
Project Grimhold/Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP LIT.shadergraph.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-HDRP UNLIT.shadergraph.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-URP Lit.shadergraph.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_SDF.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Sprite.shader
Project Grimhold/Assets/TextMesh Pro/Shaders/TMP_Sprite.shader.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro.cginc
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro.cginc.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc.meta
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc
Project Grimhold/Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc.meta
Project Grimhold/ProjectSettings/EditorBuildSettings.asset
Project Grimhold/ProjectSettings/SceneTemplateSettings.json
```

#### Classification evidence

- subject-action → Features: Add Fusion Intro demo and enable hub mode networking
- path → Movement: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs
- path → Movement: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs.meta
- message-term → Movement: movement
- message-term → Inventory &amp; Loot: interaction
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Capsule\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/Eye\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapBlue\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapGreen.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapGreen.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapRed.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Materials/PhotonCapRed.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PhotonCap.fbx
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PhotonCap.fbx.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PlayerCapsule.fbx
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/Models/PlayerCapsule.fbx.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/PhotonDummy.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/CapsuleDummy/PhotonDummy.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundMaterial\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundTexture.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoGroundTexture.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectMaterial\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectTexture.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/DemoObjectTexture.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SmallSphereMateial\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereBlue\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereGreen\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed\_URP.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Models/Materials/SphereRed\_URP.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/NetworkRunner.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/NetworkRunner.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/PlayerObject.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Prefabs/PlayerObject.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/FusionIntroShared.unity
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/FusionIntroShared.unity.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorDeposit.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorDeposit.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorPicker.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorPicker.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorResult.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/ColorResult.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IInteractable.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IInteractable.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerColor.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerColor.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerInteract.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerInteract.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerMovement.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerSpawner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/PlayerSpawner.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build\_info.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build\_info.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf.meta
- message-term → Networking &amp; Sessions: networking
- path → UI: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset
- path → UI: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scenes/Resources/FusionMenuSceneInfo.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs
- path → UI: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/SimpleStartMenu.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/FusionMenuConfig.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/FusionSampleMenu.unity.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Fusion.Menu.asmdef.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Common.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenu.Sdk.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConfig.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuFpsAvgCounter.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuImageFitter.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuMachineId.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuPartyCodeGenerator.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPlugin.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginPing.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginTooltip.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuScreenPluginVersion.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIController.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIGameplay.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUILoading.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIMain.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIParty.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIPopup.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUIScenes.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/FusionMenuUISettings.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuMachineGuid.id.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/Resources/FusionMenuPartyCodeGenerator.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenu.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundScreen.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuBackgroundWindow.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonDropdown.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonIcon.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonPrimary.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonSecondary.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonText.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonToggle.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldDropdown.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldToggle.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconArrow.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconBackArrow.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCancel.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCheckmark.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconCopy.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconEdit.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconLoading.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconQuestionmark.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuIconSettings.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLoadingIcon.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogo.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCorona.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuLogoCoronaSmall.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuCard.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuMenuHeader.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplay.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewGameplayOverlay.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewLoading.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewMainMenu.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameChange.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPartyMenu.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewPopUp.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewScenes.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.controller.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewSettings.prefab.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium SDF.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Medium.ttf.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular SDF.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/Roboto-Regular.ttf.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular SDF.asset.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf
- path → UI: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/RobotoCondensed-Regular.ttf.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteBackground.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteEmpty.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquare.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1px.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png
- path → Art &amp; Animation: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuSpriteSquareRounded1pxShadow.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/TextMesh Pro/Shaders/TMP\_Sprite.shader
- path → Art &amp; Animation: Project Grimhold/Assets/TextMesh Pro/Shaders/TMP\_Sprite.shader.meta
- path → Input: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs
- path → Input: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/Scripts/IntroInput.cs.meta
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuButtonInputField.prefab.meta
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuFieldInput.prefab.meta
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab
- path → Input: Project Grimhold/Assets/Photon/FusionMenu/Runtime/RuntimeAssets/FusionMenuViewNameInput.prefab.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build\_info.txt
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionDemos/Fusion-Intro-Shared/build\_info.txt.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/Fusion.Menu.Editor.asmdef.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Common.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuEditor.Sdk.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/FusionMenu/Editor/FusionMenuMachineIdImporter.cs.meta
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorBuildSettings.asset

#### Diagnostics

- No classification ambiguity detected.

### 4af4fb040e596f498ebcb6af736da6be5d8425fa

[4af4fb040e596f498ebcb6af736da6be5d8425fa](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4af4fb040e596f498ebcb6af736da6be5d8425fa)

Date: 2026-07-09T20:43:45-03:00 · Classification: Other Changes (unknown) · Systems: Networking & Sessions, UI, World & Visibility, Persistence & Backend, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Photon fusion files
```

#### Original body

```text

```

#### Changed paths

```text
Project Grimhold/Assets/Photon.meta
Project Grimhold/Assets/Photon/Fusion.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.dll.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.dll.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.pdb.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.pdb.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Log.dll.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Log.dll.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.dll.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.dll.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.pdb.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.pdb.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.dll.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.dll.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.pdb.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.pdb.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.dll.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.dll.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.pdb.debug
Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.pdb.debug.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.dll
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.dll.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.xml
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.xml.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.dll
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.dll.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.xml
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.xml.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.dll
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.dll.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.xml
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.xml.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.dll
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.xml
Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.xml.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Common.dll.release
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Common.dll.release.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Log.dll.release
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Log.dll.release.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Realtime.dll.release
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Realtime.dll.release.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Runtime.dll.release
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Runtime.dll.release.meta
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Sockets.dll.release
Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Sockets.dll.release.meta
Project Grimhold/Assets/Photon/Fusion/CodeGen.meta
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.Trigger.fusionweavertrigger
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.Trigger.fusionweavertrigger.meta
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.User.cs
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.User.cs.meta
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.asmdef
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.asmdef.meta
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.cs
Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin.meta
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png
Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png.meta
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef.meta
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs
Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs
Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs.meta
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs
Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs.meta
Project Grimhold/Assets/Photon/Fusion/Plugins.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a/libnanosockets.so
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a/libnanosockets.so.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a/libnanosockets.so
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a/libnanosockets.so.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86/libnanosockets.so
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86/libnanosockets.so.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86_64.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86_64/libnanosockets.so
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86_64/libnanosockets.so.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux/libnanosockets.so
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux/libnanosockets.so.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM/libnanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM/libnanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64/libnanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64/libnanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/NanoSockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/NanoSockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64/libnanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64/libnanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86/libnanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86/libnanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static/NanoSockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static/NanoSockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL/NanoSockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL/NanoSockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64/nanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64/nanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64/nanosockets.dll
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64/nanosockets.dll.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS/libnanosockets.a
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS/libnanosockets.a.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/libnanosockets_LICENSE.txt
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/libnanosockets_LICENSE.txt.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS/libnanosockets.dylib
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS/libnanosockets.dylib.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS/libnanosockets.a
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS/libnanosockets.a.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS.meta
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS/libnanosockets.a
Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS/libnanosockets.a.meta
Project Grimhold/Assets/Photon/Fusion/Resources.meta
Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion
Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion.meta
Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset
Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset.meta
Project Grimhold/Assets/Photon/Fusion/Runtime.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.AssemblyAttributes.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.AssemblyAttributes.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.asmdef
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.asmdef.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionAddressablePrefabsPreloader.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionAddressablePrefabsPreloader.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBasicBillboard.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBasicBillboard.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrap.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrap.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUI.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUI.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUISkin.guiskin
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUISkin.guiskin.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButton.png
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButton.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonDown.png
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonDown.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonOver.png
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonOver.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartWindow.png
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartWindow.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkCharacterController.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkCharacterController.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkObjectProviderDefault.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkObjectProviderDefault.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono OFL.txt
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono OFL.txt.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/Roboto-Regular.ttf
Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/Roboto-Regular.ttf.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts/Roboto-Regular.ttf
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts/Roboto-Regular.ttf.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/RadialChartIcon.png
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/RadialChartIcon.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatisticsButtonIcon.png
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatisticsButtonIcon.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsArrowIcon.png
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsArrowIcon.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsCloseIcon.png
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsCloseIcon.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsMultipeerIcon.png
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsMultipeerIcon.png.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/BehaviourStat.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/BehaviourStat.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/ForecastObjectStats.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/ForecastObjectStats.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineChart.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineChart.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineSeparator.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineSeparator.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultiLineGraph.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultiLineGraph.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultilineGraphLegendItem.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultilineGraphLegendItem.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultipleOptionsPanel.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultipleOptionsPanel.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/NOStats.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/NOStats.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RadialChart.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RadialChart.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RateButtons.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RateButtons.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/Scrollbar.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/Scrollbar.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/BehaviourView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/BehaviourView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/ForecastObjectView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/ForecastObjectView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/LagCompensationView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/LagCompensationView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/MemoryView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/MemoryView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetObjectView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetObjectView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetworkView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetworkView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/SimulationView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/SimulationView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/TimeView.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/TimeView.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsRoot.prefab
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsRoot.prefab.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStatisticsPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStatisticsPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStats.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStats.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatistics.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatistics.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsConfig.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsConfig.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectStats.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectStats.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsMemoryPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsMemoryPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectStats.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectStats.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsRoot.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsRoot.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsSimulationPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsSimulationPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LagCompensationStatisticsPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LagCompensationStatisticsPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LineChart.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LineChart.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraph.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraph.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraphLegendItem.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraphLegendItem.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultipleOptionsPanel.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultipleOptionsPanel.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/RadialChart.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/RadialChart.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/TimeStatisticsPage.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/TimeStatisticsPage.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionChartShader.shader
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionChartShader.shader.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionRadialChartShader.shader
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionRadialChartShader.shader.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/MultilineGraph.shader
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/MultilineGraph.shader.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionChartShader.mat
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionChartShader.mat.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionMultilineGraphShader.mat
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionMultilineGraphShader.mat.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionRadialChartShader.mat
Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit_FusionRadialChartShader.mat.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs.meta
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs
Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs.meta
Project Grimhold/Assets/Photon/Fusion/build_info.txt
Project Grimhold/Assets/Photon/Fusion/build_info.txt.meta
Project Grimhold/Assets/Photon/Fusion/package.json
Project Grimhold/Assets/Photon/Fusion/package.json.meta
Project Grimhold/Assets/Photon/Fusion/release_history.txt
Project Grimhold/Assets/Photon/Fusion/release_history.txt.meta
Project Grimhold/Assets/Photon/FusionDemos.meta
Project Grimhold/Assets/Photon/FusionMenu.meta
Project Grimhold/Assets/Photon/PhotonLibs.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/PhotonWebSocket.asmdef
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/PhotonWebSocket.asmdef.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/SocketWebTcp.cs
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/SocketWebTcp.cs.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.cs
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.cs.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.jslib
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.jslib.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.README
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.README.meta
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.dll
Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.dll.meta
Project Grimhold/Assets/Photon/PhotonLibs/changes-library.txt
Project Grimhold/Assets/Photon/PhotonLibs/changes-library.txt.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.dll
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.dll.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.xml
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.xml.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll.meta
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.xml
Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.xml.meta
Project Grimhold/Assets/Photon/PhotonRealtime.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AppSettings.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AppSettings.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AsyncExtensions.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AsyncExtensions.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Callbacks.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Callbacks.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ConnectionHandler.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ConnectionHandler.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/CustomTypesUnity.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/CustomTypesUnity.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Enums.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Enums.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ErrorInfo.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ErrorInfo.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/EventBetter.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/EventBetter.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Extensions.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Extensions.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/FindFriendsArgs.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/FindFriendsArgs.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/FriendInfo.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/FriendInfo.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Lobby.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Lobby.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Log.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Log.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/MatchmakingExtensions.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/MatchmakingExtensions.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Photon.Realtime.asmdef
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Photon.Realtime.asmdef.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/PhotonPing.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/PhotonPing.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Player.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Player.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ProtocolPorts.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/ProtocolPorts.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RaiseEventArgs.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RaiseEventArgs.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClient.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClient.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientOps.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientOps.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientVersion.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientVersion.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Region.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Region.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RegionHandler.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RegionHandler.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs
Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs.meta
Project Grimhold/Assets/Photon/PhotonRealtime/Code/changes-realtime.txt
Project Grimhold/Assets/Photon/PhotonRealtime/Code/changes-realtime.txt.meta
Project Grimhold/Packages/manifest.json
Project Grimhold/ProjectSettings/ProjectSettings.asset
```

#### Classification evidence

- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.dll.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.dll.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.pdb.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Common.pdb.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Log.dll.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Log.dll.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.dll.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.dll.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.pdb.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Realtime.pdb.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.dll.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.dll.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.pdb.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Runtime.pdb.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.dll.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.dll.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.pdb.debug
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Debug/Fusion.Sockets.pdb.debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Common.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Log.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Realtime.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Fusion.Sockets.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Common.dll.release
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Common.dll.release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Log.dll.release
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Log.dll.release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Realtime.dll.release
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Realtime.dll.release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Runtime.dll.release
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Runtime.dll.release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Sockets.dll.release
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Assemblies/Release/Fusion.Sockets.dll.release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.Trigger.fusionweavertrigger
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.Trigger.fusionweavertrigger.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.User.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.User.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/CodeGen/Fusion.CodeGen.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a/libnanosockets.so
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/arm64-v8a/libnanosockets.so.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a/libnanosockets.so
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/armeabi-v7a/libnanosockets.so.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86/libnanosockets.so
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86/libnanosockets.so.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86\_64.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86\_64/libnanosockets.so
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Android/x86\_64/libnanosockets.so.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux/libnanosockets.so
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Linux/libnanosockets.so.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM/libnanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM/libnanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64/libnanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/ARM64/libnanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/NanoSockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/NanoSockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64/libnanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x64/libnanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86/libnanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Metro/x86/libnanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static/NanoSockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Static/NanoSockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL/NanoSockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/WebGL/NanoSockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64/nanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/arm64/nanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64/nanosockets.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/Windows/x64/nanosockets.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS/libnanosockets.a
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/iOS/libnanosockets.a.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/libnanosockets\_LICENSE.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/libnanosockets\_LICENSE.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS/libnanosockets.dylib
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/macOS/libnanosockets.dylib.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS/libnanosockets.a
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/tvOS/libnanosockets.a.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS/libnanosockets.a
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Plugins/NanoSockets/visionOS/libnanosockets.a.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.AssemblyAttributes.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.AssemblyAttributes.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Fusion.Unity.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionAddressablePrefabsPreloader.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionAddressablePrefabsPreloader.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBasicBillboard.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBasicBillboard.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrap.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrap.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUI.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUISkin.guiskin
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUISkin.guiskin.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButton.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButton.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonDown.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonDown.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonOver.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartButtonOver.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartWindow.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionBootstrapDebugGUIStartWindow.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkCharacterController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkCharacterController.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkObjectProviderDefault.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkObjectProviderDefault.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono OFL.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono OFL.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/JetBrainsMono-Regular.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/Roboto-Regular.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/RuntimeAssets/Roboto-Regular.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts/Roboto-Regular.ttf
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Fonts/Roboto-Regular.ttf.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/RadialChartIcon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/RadialChartIcon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatisticsButtonIcon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatisticsButtonIcon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsArrowIcon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsArrowIcon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsCloseIcon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsCloseIcon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsMultipeerIcon.png
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Icons/StatsMultipeerIcon.png.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/BehaviourStat.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/BehaviourStat.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/ForecastObjectStats.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/ForecastObjectStats.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineChart.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineChart.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineSeparator.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/LineSeparator.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultiLineGraph.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultiLineGraph.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultilineGraphLegendItem.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultilineGraphLegendItem.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultipleOptionsPanel.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/MultipleOptionsPanel.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/NOStats.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/NOStats.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RadialChart.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RadialChart.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RateButtons.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/RateButtons.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/Scrollbar.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/IndividualElements/Scrollbar.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/BehaviourView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/BehaviourView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/ForecastObjectView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/ForecastObjectView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/LagCompensationView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/LagCompensationView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/MemoryView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/MemoryView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetObjectView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetObjectView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetworkView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/NetworkView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/SimulationView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/SimulationView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/TimeView.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Prefabs/Pages/TimeView.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsRoot.prefab
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsRoot.prefab.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStatisticsPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStatisticsPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStats.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionBehaviourStats.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatistics.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatistics.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsConfig.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsConfig.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectStats.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsForecastObjectStats.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsMemoryPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsMemoryPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectStats.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkObjectStats.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsNetworkPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsRoot.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsRoot.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsSimulationPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/FusionStatisticsSimulationPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LagCompensationStatisticsPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LagCompensationStatisticsPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LineChart.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/LineChart.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraph.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraph.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraphLegendItem.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultilineGraphLegendItem.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultipleOptionsPanel.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/MultipleOptionsPanel.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/RadialChart.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/RadialChart.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/TimeStatisticsPage.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/Scripts/TimeStatisticsPage.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionChartShader.shader
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionChartShader.shader.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionRadialChartShader.shader
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/FusionRadialChartShader.shader.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/MultilineGraph.shader
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/MultilineGraph.shader.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionChartShader.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionChartShader.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionMultilineGraphShader.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionMultilineGraphShader.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionRadialChartShader.mat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Statistics/ShaderAndMaterial/Unlit\_FusionRadialChartShader.mat.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/build\_info.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/build\_info.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/package.json
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/package.json.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/release\_history.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/Fusion/release\_history.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionDemos.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/FusionMenu.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/PhotonWebSocket.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/PhotonWebSocket.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/SocketWebTcp.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/SocketWebTcp.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.jslib
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/WebSocket.jslib.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.README
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.README.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/changes-library.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/changes-library.txt.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.xml
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.xml.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AppSettings.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AppSettings.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AsyncExtensions.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AsyncExtensions.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Callbacks.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Callbacks.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ConnectionHandler.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ConnectionHandler.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/CustomTypesUnity.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/CustomTypesUnity.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Enums.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Enums.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ErrorInfo.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ErrorInfo.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EventBetter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EventBetter.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Extensions.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Extensions.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/FindFriendsArgs.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/FindFriendsArgs.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/FriendInfo.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/FriendInfo.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Lobby.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Lobby.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Log.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Log.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/MatchmakingExtensions.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/MatchmakingExtensions.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Photon.Realtime.asmdef
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Photon.Realtime.asmdef.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/PhotonPing.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/PhotonPing.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Player.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Player.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ProtocolPorts.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/ProtocolPorts.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RaiseEventArgs.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RaiseEventArgs.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClient.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClient.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientOps.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientOps.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientVersion.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RealtimeClientVersion.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Region.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Region.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RegionHandler.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RegionHandler.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/changes-realtime.txt
- path → Networking &amp; Sessions: Project Grimhold/Assets/Photon/PhotonRealtime/Code/changes-realtime.txt.meta
- message-term → Networking &amp; Sessions: Photon fusion
- path → UI: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs
- path → UI: Project Grimhold/Assets/Photon/Fusion/Runtime/FusionMenuSceneInfo.cs.meta
- path → UI: Project Grimhold/Assets/Photon/FusionMenu.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerAOIGizmos.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerLagCompensationGizmos.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLink.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerVisibilityLinksRoot.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/EnterRoomArgs.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/JoinRandomRoomArgs.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/Room.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomInfo.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Photon/PhotonRealtime/Code/RoomOptions.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Photon/PhotonRealtime/Code/AuthenticationValues.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/OFL.txt.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fonts/Oswald-Header.ttf.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-off.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-dark-help-button-on.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-2x.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box-no-vertical-borders.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-box.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-help-box.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-inline-selector.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-off.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-light-help-button-on.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo-2x.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-logo.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-outline-box.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header-2x.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion-script-header.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/Fusion.Unity.xml.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorAssetLibrary.asset.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPages.asset.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorHubPagesBeginner.asset.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionEditorSkin.guiskin.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-community.png.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-correct-icon.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-discord.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-icon.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-information.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-install.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-link.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-logo.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-missing.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark-2.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-photon-cloud-32-dark.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-samples2.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-box.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-active.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button-hover.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-button.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-sand.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-steel.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-skin-window.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-hub-txt.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-ADVANCED.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/Fusion-mode-selection-BEGINNER.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/FusionHub/FusionHubSkin.guiskin.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/EditorResources/networked-property-icon.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.AssemblyAttributes.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.asmdef.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorAssetLibrary.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubModeSelectionWindow.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubPageSO.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorHubWindowSdk.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionEditorSkin.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionHubWindow.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionPluginProjectSettings.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionRunnerVisibilityControlsWindow.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporter.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/Editor/NetworkProjectConfigImporterEditor.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/build\_info.txt
- path → Build &amp; Tooling: Project Grimhold/Assets/Photon/Fusion/build\_info.txt.meta
- path → Build &amp; Tooling: Project Grimhold/Packages/manifest.json

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### c5d13cdf5e36cab27cf77f026047a9c786499def

[c5d13cdf5e36cab27cf77f026047a9c786499def](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c5d13cdf5e36cab27cf77f026047a9c786499def)

Date: 2026-07-09T19:02:18-03:00 · Classification: Other Changes (unknown) · Systems: Persistence & Backend, Audio, Input, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Initial Unity Files
```

#### Original body

```text

```

#### Changed paths

```text
.gitignore
Project Grimhold/.vsconfig
Project Grimhold/Assets/DefaultVolumeProfile.asset
Project Grimhold/Assets/DefaultVolumeProfile.asset.meta
Project Grimhold/Assets/InputSystem_Actions.inputactions
Project Grimhold/Assets/InputSystem_Actions.inputactions.meta
Project Grimhold/Assets/Scenes.meta
Project Grimhold/Assets/Scenes/SampleScene.unity
Project Grimhold/Assets/Scenes/SampleScene.unity.meta
Project Grimhold/Assets/Settings.meta
Project Grimhold/Assets/Settings/Lit2DSceneTemplate.scenetemplate
Project Grimhold/Assets/Settings/Lit2DSceneTemplate.scenetemplate.meta
Project Grimhold/Assets/Settings/Renderer2D.asset
Project Grimhold/Assets/Settings/Renderer2D.asset.meta
Project Grimhold/Assets/Settings/Scenes.meta
Project Grimhold/Assets/Settings/Scenes/URP2DSceneTemplate.unity
Project Grimhold/Assets/Settings/Scenes/URP2DSceneTemplate.unity.meta
Project Grimhold/Assets/Settings/UniversalRP.asset
Project Grimhold/Assets/Settings/UniversalRP.asset.meta
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset.meta
Project Grimhold/Packages/manifest.json
Project Grimhold/Packages/packages-lock.json
Project Grimhold/ProjectSettings/AudioManager.asset
Project Grimhold/ProjectSettings/ClusterInputManager.asset
Project Grimhold/ProjectSettings/DynamicsManager.asset
Project Grimhold/ProjectSettings/EditorBuildSettings.asset
Project Grimhold/ProjectSettings/EditorSettings.asset
Project Grimhold/ProjectSettings/GraphicsSettings.asset
Project Grimhold/ProjectSettings/InputManager.asset
Project Grimhold/ProjectSettings/MemorySettings.asset
Project Grimhold/ProjectSettings/MultiplayerManager.asset
Project Grimhold/ProjectSettings/NavMeshAreas.asset
Project Grimhold/ProjectSettings/NetworkManager.asset
Project Grimhold/ProjectSettings/PackageManagerSettings.asset
Project Grimhold/ProjectSettings/Physics2DSettings.asset
Project Grimhold/ProjectSettings/PhysicsCoreProjectSettings2D.asset
Project Grimhold/ProjectSettings/PresetManager.asset
Project Grimhold/ProjectSettings/ProjectSettings.asset
Project Grimhold/ProjectSettings/ProjectVersion.txt
Project Grimhold/ProjectSettings/QualitySettings.asset
Project Grimhold/ProjectSettings/ShaderGraphSettings.asset
Project Grimhold/ProjectSettings/TagManager.asset
Project Grimhold/ProjectSettings/TimeManager.asset
Project Grimhold/ProjectSettings/URPProjectSettings.asset
Project Grimhold/ProjectSettings/UnityConnectSettings.asset
Project Grimhold/ProjectSettings/VFXManager.asset
Project Grimhold/ProjectSettings/VersionControlSettings.asset
Project Grimhold/ProjectSettings/XRSettings.asset
```

#### Classification evidence

- path → Persistence &amp; Backend: Project Grimhold/Assets/DefaultVolumeProfile.asset
- path → Persistence &amp; Backend: Project Grimhold/Assets/DefaultVolumeProfile.asset.meta
- path → Audio: Project Grimhold/ProjectSettings/AudioManager.asset
- path → Input: Project Grimhold/Assets/InputSystem\_Actions.inputactions
- path → Input: Project Grimhold/Assets/InputSystem\_Actions.inputactions.meta
- path → Input: Project Grimhold/ProjectSettings/ClusterInputManager.asset
- path → Input: Project Grimhold/ProjectSettings/InputManager.asset
- path → Build &amp; Tooling: Project Grimhold/Packages/manifest.json
- path → Build &amp; Tooling: Project Grimhold/Packages/packages-lock.json
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorBuildSettings.asset
- path → Build &amp; Tooling: Project Grimhold/ProjectSettings/EditorSettings.asset

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### eb34b0ca77f55fca08a2705463e1891a774da87d

[eb34b0ca77f55fca08a2705463e1891a774da87d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/eb34b0ca77f55fca08a2705463e1891a774da87d)

Date: 2026-07-09T18:43:56-03:00 · Classification: Other Changes (unknown) · Systems: Unassigned

Flags: primary or unclassified

#### Original subject

```text
Initial commit
```

#### Original body

```text
Initial commit
```

#### Changed paths

```text
.gitignore
```

#### Classification evidence

- No sufficient classification evidence.

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.
- **generic-title:** The subject does not identify a concrete change. Retained in the archive; omitted from summary headlines.
- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.


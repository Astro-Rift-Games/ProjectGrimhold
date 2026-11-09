# Unreleased

[Archive index](README.md)

Release: Unreleased

97 original commits; 97 canonical entries.

## Canonical classification

55 features; 23 bug fixes; 1 refactoring; 1 architecture & infrastructure; 9 documentation; 2 tests; 4 maintenance; 2 other changes

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Abilities | 7 | 6 | 1 | 0 | 4 | 1 |
| Art & Animation | 7 | 7 | 1 | 0 | 4 | 0 |
| Audio | 3 | 0 | 0 | 0 | 0 | 0 |
| Build & Tooling | 33 | 14 | 1 | 1 | 6 | 2 |
| Combat | 34 | 5 | 0 | 0 | 6 | 0 |
| Documentation | 24 | 8 | 0 | 0 | 4 | 0 |
| Extraction | 9 | 2 | 0 | 0 | 2 | 0 |
| Input | 0 | 0 | 0 | 0 | 1 | 0 |
| Inventory & Loot | 47 | 20 | 1 | 1 | 6 | 1 |
| Movement | 3 | 0 | 0 | 0 | 0 | 0 |
| Networking & Sessions | 15 | 6 | 0 | 0 | 2 | 0 |
| Persistence & Backend | 21 | 6 | 0 | 1 | 3 | 0 |
| Tests | 53 | 19 | 1 | 1 | 7 | 0 |
| UI | 21 | 5 | 1 | 0 | 4 | 0 |
| Unassigned | 0 | 0 | 0 | 0 | 2 | 0 |
| World & Visibility | 16 | 4 | 0 | 0 | 0 | 0 |

## Original commit records

### 74e257bdcaa399bb6bdc9ed82e833f60d1db2170

[74e257bdcaa399bb6bdc9ed82e833f60d1db2170](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/74e257bdcaa399bb6bdc9ed82e833f60d1db2170)

Date: 2026-11-08T22:46:35-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): mirror training weapon return locally and exclude it from corpses
```

#### Original body

```text
TASK-279: LocalProfileStore applies the Doc 09 §15.2 return rule on extraction commit while keeping the raw retry payload; corpse generation loads a projection without training weapons but still clears them from the player on definitive defeat or abandon.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/CorpseLootProjection.cs
Project Grimhold/Assets/Scripts/Player/CorpseLootProjection.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs
Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs
odd/tasks/task-279-training-weapon-return.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs.meta
- path → Combat: odd/tasks/task-279-training-weapon-return.md
- message-term → Combat: weapon
- path → Extraction: Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs
- path → Extraction: Project Grimhold/Assets/Scripts/Stash/TrainingWeaponReturnRule.cs.meta
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs.meta
- path → Extraction: odd/tasks/task-279-training-weapon-return.md
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/CorpseLootProjection.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/CorpseLootProjection.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs.meta
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/CorpseLootProjectionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponExtractionReturnTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerCorpseGenerationPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 5a43fde5fbecde02d1a35cd656fe3c94d172237a

[5a43fde5fbecde02d1a35cd656fe3c94d172237a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5a43fde5fbecde02d1a35cd656fe3c94d172237a)

Date: 2026-11-08T22:46:35-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): return training weapons to the Trainer on extraction commit
```

#### Original body

```text
TASK-279 (Doc 09 §15.2): when the character owns a normal weapon in loadout, stash or prepared equipment after settlement, training weapons are removed from all three in the same revision-guarded write; otherwise they are kept.
```

#### Changed paths

```text
backend/src/services/ExtractionCommitService.js
backend/src/services/InventoryService.js
backend/src/services/TrainingWeapons.js
backend/tests/extraction.training-return.test.js
```

#### Classification evidence

- type → Features: feat
- path → Combat: backend/src/services/TrainingWeapons.js
- message-term → Combat: weapons
- path → Extraction: backend/src/services/ExtractionCommitService.js
- path → Extraction: backend/tests/extraction.training-return.test.js
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- message-term → Inventory &amp; Loot: equipment
- path → Persistence &amp; Backend: backend/src/services/ExtractionCommitService.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/services/TrainingWeapons.js
- path → Persistence &amp; Backend: backend/tests/extraction.training-return.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/tests/extraction.training-return.test.js

#### Diagnostics

- No classification ambiguity detected.

### 13406d0a245b3ae7934637333c5763eda282f976

[13406d0a245b3ae7934637333c5763eda282f976](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/13406d0a245b3ae7934637333c5763eda282f976)

Date: 2026-11-08T22:42:10-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot

Flags: primary or unclassified

#### Original subject

```text
fix(inventory): restore serialized loot catalog on RaidInventoryPresenter
```

#### Original body

```text
Commit 43c23186 placed NotTransferableMessage between [SerializeField] and _lootCatalog, so the catalog stopped deserializing and Town/Raid inventory binding failed.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory

#### Diagnostics

- No classification ambiguity detected.

### 9f58c40e244dddc6fa6fe6ac9a1f5502ae3d2da2

[9f58c40e244dddc6fa6fe6ac9a1f5502ae3d2da2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9f58c40e244dddc6fa6fe6ac9a1f5502ae3d2da2)

Date: 2026-11-08T21:06:39-03:00 · Classification: Architecture & Infrastructure (explicit) · Systems: Inventory & Loot, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
ci: implement automated quality gate with pre-push hooks and GitHub Actions
```

#### Original body

```text
Add CI workflow for New-Testing and main covering secrets scan, backend suites, parity contracts, and test coverage. Add automated Git pre-push hook with native Windows desktop alerts and silent initialization in Unity Editor and npm prepare.
```

#### Changed paths

```text
.githooks/pre-push
.github/workflows/ci.yml
AGENTS.md
Project Grimhold/Assets/Editor/GitHooks.meta
Project Grimhold/Assets/Editor/GitHooks/GitHooksSetup.cs
Project Grimhold/Assets/Editor/GitHooks/GitHooksSetup.cs.meta
backend/package.json
backend/tests/concurrency/inventory.real.test.js
tools/hooks/setup-hooks.js
tools/security/scan-secrets.js
```

#### Classification evidence

- type → Architecture &amp; Infrastructure: ci
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.real.test.js
- path → Persistence &amp; Backend: backend/package.json
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.real.test.js
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: .github/workflows/ci.yml
- path → Build &amp; Tooling: AGENTS.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/GitHooks.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/GitHooks/GitHooksSetup.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/GitHooks/GitHooksSetup.cs.meta
- path → Build &amp; Tooling: tools/hooks/setup-hooks.js
- path → Build &amp; Tooling: tools/security/scan-secrets.js
- path → Tests: backend/tests/concurrency/inventory.real.test.js
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 9509f3b4842b9f6d1b936665435ae49f75904a3b

[9509f3b4842b9f6d1b936665435ae49f75904a3b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9509f3b4842b9f6d1b936665435ae49f75904a3b)

Date: 2026-11-08T20:22:49-03:00 · Classification: Documentation (inferred) · Systems: Abilities, Build & Tooling, Tests

Flags: secondary

#### Original subject

```text
Enforce strict TDD in project workflows
```

#### Original body

```text
Update repository, task-execution, and Unity guidance to require Red-Green-Refactor TDD, discourage superficial tests and architectural shortcuts, and require observing failing tests before implementation.
```

#### Changed paths

```text
.agents/skills/grimhold-task-execution/SKILL.md
AGENTS.md
Project Grimhold/AGENTS.md
```

#### Classification evidence

- artifact-paths → Documentation: .agents/skills/grimhold-task-execution/SKILL.md, AGENTS.md, Project Grimhold/AGENTS.md
- path → Abilities: .agents/skills/grimhold-task-execution/SKILL.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/SKILL.md
- path → Build &amp; Tooling: AGENTS.md
- path → Build &amp; Tooling: Project Grimhold/AGENTS.md
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### cdd69eb020f4b1b76d8ddffef3946559ea3fd172

[cdd69eb020f4b1b76d8ddffef3946559ea3fd172](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/cdd69eb020f4b1b76d8ddffef3946559ea3fd172)

Date: 2026-11-08T11:42:31-03:00 · Classification: Documentation (explicit) · Systems: Combat, Networking & Sessions, Persistence & Backend, Tests

Flags: secondary

#### Original subject

```text
docs(odd): record TASK-278 validation and delivery evidence
```

#### Original body

```text
Close the execution record with the backend, Unity and TASK-277 commit
identities, regression results, known full-suite failures and excluded
working-tree changes.

Record that the final independent source review passed with issues but
its isolated focused test was unrun after setup timed out. Preserve the
user's explicit delivery disposition without treating it as verification.

Document exact temporary-directory cleanup and preservation of unrelated
working bytes and index entries. No push was authorized or performed.

Refs: TASK-278, TASK-277
```

#### Changed paths

```text
Project Grimhold/Assets/Scenes/Lobby-Town.unity
odd/tasks/task-278-training-weapons.md
```

#### Classification evidence

- type → Documentation: docs
- path → Combat: odd/tasks/task-278-training-weapons.md
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scenes/Lobby-Town.unity
- message-term → Persistence &amp; Backend: backend
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 5fc0ebf17d638caba9aef9c3bbc5d01d8ef34d14

[5fc0ebf17d638caba9aef9c3bbc5d01d8ef34d14](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5fc0ebf17d638caba9aef9c3bbc5d01d8ef34d14)

Date: 2026-11-08T11:36:13-03:00 · Classification: Bug Fixes (explicit) · Systems: Combat, Inventory & Loot, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): align training weapon validation and progression coverage
```

#### Original body

```text
Exclude training weapons from required progression-value coverage and
reject explicit progression values for them. Preserve ordinary loot
values while treating training weapons as non-progression equipment.

Use valid training fixtures and assert all nine one-handed weapons in
the OffHand catalog. Verify training weapons reuse their normal weapon
family animation sets without regenerating authored animations.

Validation: LootDefinitionTests 51/51, OffHandRetargetingTests 83/83 with
regeneration excluded, and RaidLootValueCatalogTests 5/5. The regeneration
test remains unchanged and was not run to protect dirty animation assets.

Refs: TASK-277, TASK-278
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Progression/RaidLootValueCatalog.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/OffHandRetargetingTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Progression/RaidLootValueCatalogTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/OffHandRetargetingTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Progression/RaidLootValueCatalog.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Progression/RaidLootValueCatalogTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/OffHandRetargetingTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Progression/RaidLootValueCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/OffHandRetargetingTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Progression/RaidLootValueCatalogTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 95d8c7cb79d033bdfc5e1c85c3073e9c7762d9ba

[95d8c7cb79d033bdfc5e1c85c3073e9c7762d9ba](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/95d8c7cb79d033bdfc5e1c85c3073e9c7762d9ba)

Date: 2026-11-08T11:35:30-03:00 · Classification: Features (explicit) · Systems: Combat, Abilities, Inventory & Loot, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(town): preserve Trainer equipment through raid preparation
```

#### Original body

```text
Request training weapons through the backend and apply its authoritative
acknowledgement, including instance identity and revision. Reconcile lost
responses and stale revisions without replaying the POST, while keeping
the request gate held until reconciliation finishes.

Add the Trainer interaction, data-driven selection view, prefab wiring
and Town scene instance. Equip the selected weapon in Weapon Set A and
preserve ordinary displaced equipment.

Remove recovery-weapon configuration, services, consent UI and network
state. Require an equipped weapon for Ready and direct weaponless players
to the Trainer. Acknowledge launch only after reservation save succeeds;
ignore cancelled and late completions.

Migrate consumers, sandbox preparation and architecture documentation.
Add admission, Host/Client ACK, cancellation and request regression tests.
Keep the shared store and interface changes together to avoid missing
consumers in an intermediate commit. Unity-generated assets contribute
substantially to this large review unit.

Validation: compilation and Fusion weaving/baking passed. Focused Unity
and PlayMode suites passed; endpoint coverage now passes 21/21. The full
EditMode run has 5,980 passes and 19 recorded failures out of 5,999 tests.
OffHand regeneration and live multiplayer were not run. Independent
source review passed with issues; final isolated execution was unrun.

Refs: TASK-278
Player-Added: Ask the Trainer for a free training weapon in Town.
Player-Fixed: Ready now directs you to the Trainer when no weapon is equipped.
```

#### Changed paths

```text
Project Grimhold/Assets/Editor/CharcoalUiMigrationManifest.json
Project Grimhold/Assets/Editor/Task220Composition.cs
Project Grimhold/Assets/Prefabs/SocialPlayer.prefab
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Prefabs/TownTrainerNpc.prefab
Project Grimhold/Assets/Prefabs/TownTrainerNpc.prefab.meta
Project Grimhold/Assets/Resources/LocalProfilePersistenceConfiguration.asset
Project Grimhold/Assets/Scenes/Lobby-Town.unity
Project Grimhold/Assets/Scriptable Objects/DialogueSequences/TownTrainerNpcDialogue.asset
Project Grimhold/Assets/Scriptable Objects/DialogueSequences/TownTrainerNpcDialogue.asset.meta
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationNetworkController.cs
Project Grimhold/Assets/Scripts/Networking/TownTrainerNpcInteractable.cs
Project Grimhold/Assets/Scripts/Networking/TownTrainerNpcInteractable.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/ITownEquipmentMutationEndpoint.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RecoveryWeaponConsentView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RecoveryWeaponConsentView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownEquipmentMutationEndpoint.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownTrainerPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownTrainerPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownTrainerView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownTrainerView.cs.meta
Project Grimhold/Assets/Scripts/Sandbox/SandboxTestProfile.cs
Project Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs
Project Grimhold/Assets/Scripts/Stash/ExpeditionPreparationResult.cs
Project Grimhold/Assets/Scripts/Stash/IPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs
Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs.meta
Project Grimhold/Assets/Scripts/Stash/LocalProfilePersistenceConfiguration.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/ProfileReconciliationService.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs.meta
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparation.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparation.cs.meta
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparationLaunchRules.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparationLaunchRules.cs.meta
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/DevelopmentProfileBootstrapTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownEquipmentMutationEndpointTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Sandbox/SandboxTestProfileTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/TownInventoryPresenterPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/TownTrainerRequestPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/TownTrainerRequestPlayModeTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/AbilitySandbox.md
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
Project Grimhold/Docs/Architecture/LobbyAndSessionTransitionArchitecture.md
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
- path → Combat: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/RecoveryWeaponConsentView.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Presentation/RecoveryWeaponConsentView.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparation.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparation.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparationLaunchRules.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponPreparationLaunchRules.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs.meta
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: weapons
- path → Abilities: Project Grimhold/Docs/Architecture/AbilitySandbox.md
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/ITownEquipmentMutationEndpoint.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownEquipmentMutationEndpoint.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownEquipmentMutationEndpointTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/TownInventoryPresenterPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scenes/Lobby-Town.unity
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationNetworkController.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownTrainerNpcInteractable.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownTrainerNpcInteractable.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/DevelopmentProfileBootstrapTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/LobbyAndSessionTransitionArchitecture.md
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- path → UI: Project Grimhold/Assets/Editor/CharcoalUiMigrationManifest.json
- message-term → UI: UI
- path → Persistence &amp; Backend: Project Grimhold/Assets/Resources/LocalProfilePersistenceConfiguration.asset
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Sandbox/SandboxTestProfile.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfilePersistenceConfiguration.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileReconciliationService.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/DevelopmentProfileBootstrapTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Sandbox/SandboxTestProfileTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/CharcoalUiMigrationManifest.json
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/Task220Composition.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/DevelopmentProfileBootstrapTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownEquipmentMutationEndpointTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Sandbox/SandboxTestProfileTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/AbilitySandbox.md
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LobbyAndSessionTransitionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Scripts/Sandbox/SandboxTestProfile.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/DevelopmentProfileBootstrapTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/ExpeditionWeaponRequirementTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RecoveryWeaponLaunchConsentTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownTrainerNpcPrefabTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TrainingWeaponAdmissionTicketTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownEquipmentMutationEndpointTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownTrainerPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Sandbox/SandboxTestProfileTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/TrainingWeaponTownRequestTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/TownInventoryPresenterPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/TownTrainerRequestPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/TownTrainerRequestPlayModeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/TrainingWeaponAdmissionPlayModeTests.cs.meta
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 20111527450cdd438ce61effa14142e202220efd

[20111527450cdd438ce61effa14142e202220efd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/20111527450cdd438ce61effa14142e202220efd)

Date: 2026-11-08T11:34:33-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, World & Visibility, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(inventory): issue training weapons through guarded requests
```

#### Original body

```text
Issue training equipment only through the authoritative training request.
Require the current revision, reject pending reservations and legacy v1
inventory, and replace previous training equipment atomically. Keep
normal displaced weapons and reject training sales or buybacks.

Remove recovery-weapon grants from reservations and admission tickets.
Reject weaponless preparation with PREPARED_WEAPON_REQUIRED instead of
silently assigning arming_sword. Reject unknown instances in equipment
updates instead of minting equipment through PUT.

Document the request and errors. Cover replacement, no-write rejections,
reservation identity and signed admission with unit and integration tests.

Validation: backend 581/581; integration 200/200; new HTTP probes 7/7.
Independent source review passed with issues. The final isolated test
execution was unrun after setup timed out; this is not a passing test.

Refs: TASK-278, TASK-277
Player-Fixed: Enter the Dungeon with the training weapon you selected.
```

#### Changed paths

```text
backend/README.md
backend/src/config/shopCatalog.js
backend/src/routes/inventory.routes.js
backend/src/services/InventoryService.js
backend/src/services/SessionTicketService.js
backend/src/validators/inventory.validators.js
backend/tests/integration/admission.integration.test.js
backend/tests/integration/item.instances.test.js
backend/tests/inventory.instances.test.js
backend/tests/inventory.lifecycle.test.js
backend/tests/inventory.protocol.test.js
backend/tests/inventory.reservation.test.js
backend/tests/inventory.service.test.js
backend/tests/inventory.training.test.js
backend/tests/inventory.training.verifier.test.js
backend/tests/reservation.weapon-required.test.js
```

#### Classification evidence

- type → Features: feat
- path → Combat: backend/tests/reservation.weapon-required.test.js
- message-term → Combat: weapons
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/src/validators/inventory.validators.js
- path → Inventory &amp; Loot: backend/tests/inventory.instances.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.lifecycle.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.protocol.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.reservation.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.service.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.training.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.training.verifier.test.js
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: backend/src/services/SessionTicketService.js
- message-term → World &amp; Visibility: Dungeon
- path → Persistence &amp; Backend: backend/README.md
- path → Persistence &amp; Backend: backend/src/config/shopCatalog.js
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/src/validators/inventory.validators.js
- path → Persistence &amp; Backend: backend/tests/integration/admission.integration.test.js
- path → Persistence &amp; Backend: backend/tests/integration/item.instances.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.instances.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.lifecycle.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.protocol.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.reservation.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.service.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.training.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.training.verifier.test.js
- path → Persistence &amp; Backend: backend/tests/reservation.weapon-required.test.js
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/tests/integration/admission.integration.test.js
- path → Tests: backend/tests/integration/item.instances.test.js
- path → Tests: backend/tests/inventory.instances.test.js
- path → Tests: backend/tests/inventory.lifecycle.test.js
- path → Tests: backend/tests/inventory.protocol.test.js
- path → Tests: backend/tests/inventory.reservation.test.js
- path → Tests: backend/tests/inventory.service.test.js
- path → Tests: backend/tests/inventory.training.test.js
- path → Tests: backend/tests/inventory.training.verifier.test.js
- path → Tests: backend/tests/reservation.weapon-required.test.js
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 43c23186e748216fb04bc40b661bb10c607bd9d1

[43c23186e748216fb04bc40b661bb10c607bd9d1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/43c23186e748216fb04bc40b661bb10c607bd9d1)

Date: 2026-11-08T04:18:52-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): reject deposits of non-transferable items into containers
```

#### Original body

```text
Enforce LootDefinition.CanBeTransferred for Inventory-to-container
deposits (Doc 07 section 15, TASK-269). NetworkLootContainer
.ValidateReceive rejects a non-transferable definition right after
catalog resolution, before capacity or commit, so chests, Raid stashes
and defeated-player containers never receive it and the source slot is
unchanged. Defeat conversion into a player container is forced loss,
not a transfer, and stays ungated.

- Add LootInventoryRules.CanTransferItem (false for a missing
  definition).
- Append LootTransferFailureReason.NotTransferable; existing values
  keep their wire numbers and LootTransferResult accepts it as a
  rejection reason.
- In container mode the Raid Inventory dims non-transferable cells
  while keeping drags to Equipment and Quick Slots; drag and
  right-click deposits send no request and show "Este objeto no se
  puede transferir a contenedores", also used for the host rejection.

Only the three training weapons are authored as non-transferable.

Tests:
- LootInventoryRulesTests: CanTransferItem true/false/missing
  definition; NotTransferable is a valid rejection.
- LockedLootContainerPlayModeTests: a non-transferable deposit is
  rejected without changing content, provenance or change sequence,
  while a transferable deposit stays valid.
- RaidInventoryViewPlayModeTests: transfer restriction dims the icon,
  keeps the cell draggable and resets on re-present or empty cells.

Docs: Contratos_Base (new failure reason), LootInteractionArchitecture
and RaidInventoryUIArchitecture.

Player-Improved: Training weapons now appear dimmed when a chest is open and can no longer be stored in chests or other containers.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs
Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/Project_Grimhold_Contratos_Base_v1.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: weapons
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferFailureReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferResult.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/Project\_Grimhold\_Contratos\_Base\_v1.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LockedLootContainerPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryViewPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### c6c8df367e772cf5a01d909963fc753ebc003e19

[c6c8df367e772cf5a01d909963fc753ebc003e19](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c6c8df367e772cf5a01d909963fc753ebc003e19)

Date: 2026-11-08T03:51:39-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): reject drops of non-discardable items on the host
```

#### Original body

```text
Enforce LootDefinition.CanBeDiscarded for voluntary Inventory-to-world
drops (Doc 07 section 15, TASK-268). State Authority rejects a
non-discardable definition right after catalog resolution, before any
quantity, origin, placement or spawn work, so no pickup is created and
the Inventory slot and revision stay unchanged in every drop mode.

- Add LootInventoryRules.CanDiscardItem (false for a missing definition).
- Add LootDropFailureReason.NotDiscardable = 9; existing values keep
  their wire numbers and confirmation reconstruction accepts it via
  Enum.IsDefined.
- LootDropContextActionProvider keeps the three drop actions listed but
  disabled for non-discardable items and refuses to execute them.
- RaidInventoryPresenter shows "Este objeto no se puede soltar" when the
  host rejects a drop for this reason.

Tests:
- LootInventoryRulesTests: CanDiscardItem true/false/missing definition.
- LootContextActionProviderTests: non-discardable item disables every
  drop action and never opens the quantity selector.
- PlayerLootReceiverSlotsPlayModeTests: authority rejects SingleUnit,
  FullStack, ExactAmount and legacy unaddressed drops with NotDiscardable
  without spawning a pickup or mutating the Inventory.

Docs: InventoryWorldDropArchitecture.md documents the discardability
check and the disabled drop actions.

Player-Improved: Items that cannot be dropped, such as training weapons, now show their drop options disabled and can no longer be thrown on the ground.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootDropFailureReason.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 89699bb0ba7727f71dc35ca84f5f92b145baeca7

[89699bb0ba7727f71dc35ca84f5f92b145baeca7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/89699bb0ba7727f71dc35ca84f5f92b145baeca7)

Date: 2026-11-08T03:21:35-03:00 · Classification: Features (explicit) · Systems: Networking & Sessions, UI, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(town): block a new expedition while the Duo partner is in Dungeon
```

#### Original body

```text
A player who returns to Town before their partner can no longer Ready,
Start, create or join a Raid while the partner's expedition is still
active (Game Design 01 §6.3/§10, 02 §18). The lock lifts on its own
once the partner returns and the Party is restored.

- Town cannot observe the Raid runner, so the signal is the unresolved
  Duo TownPartyContinuationContext: TownPartyRules
  .IsAwaitingPartyCompletion holds for a pending two-member context.
- SessionConnectionCoordinator exposes it, SocialPlayerIdentity
  replicates IsAwaitingPartyCompletion from its State Authority and
  TownPartyDirectory reads it per player. TownRaidPreparationDirectory
  rejects Ready (cancel stays allowed), Start and Join from that player.
- TownRaidPreparationView locks Ready/Start/Create/Join with the notice
  "Tu compañero sigue en la Dungeon. Espera a que finalice su
  expedición"; the presenter repaints when the lock flips.
- A continuation whose other member already owns a Town Party
  (TownPartyRules.IsContinuationSuperseded) is withdrawn and
  invalidated, so the claimant is not locked forever.
- Editor-only "Simulate Party Member In Dungeon" on
  SessionConnectionCoordinator forces the lock for single-client tests.

A partner who never reaches Town again (quit, Host loss) keeps the
lock until the player returns to the main menu; expiry is FIX-07.

Tests:
- TownPartyRulesTests: awaiting holds for a pending Duo and lifts on
  restore, never for Solo; supersession only by another member.
- SessionConnectionStateMachineTests: coordinator awaiting state across
  restore, abandonment, Solo and the Editor simulation flag.
- TownRaidPreparationViewPlayModeTests: lock, notice, busy interplay
  and unlock.

Docs: TownRaidQueueArchitecture.md documents the awaiting-partner lock.

Refs: TASK-267
Player-Added: If you return to Town before your partner, you now see that they are still in the Dungeon and can start the next expedition together once they are back.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
Project Grimhold/Assets/Scripts/Networking/SocialPlayerIdentity.cs
Project Grimhold/Assets/Scripts/Networking/TownPartyDirectory.cs
Project Grimhold/Assets/Scripts/Networking/TownPartyRules.cs
Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationDirectory.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationView.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownPartyRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/TownRaidPreparationViewPlayModeTests.cs
Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SocialPlayerIdentity.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownPartyDirectory.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownPartyRules.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationDirectory.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownPartyRulesTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- message-term → UI: menu
- message-term → World &amp; Visibility: Dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownPartyRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionConnectionStateMachineTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownPartyRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/TownRaidPreparationViewPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 11e6ae82d2bcb0571de16fb34590fd62250d41b4

[11e6ae82d2bcb0571de16fb34590fd62250d41b4](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/11e6ae82d2bcb0571de16fb34590fd62250d41b4)

Date: 2026-11-07T23:32:22-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(weapons): add training weapon definitions and catalog entries
```

#### Original body

```text
Define ScriptableObjects for the 3 MVP training weapons (short sword, dagger, and wand) with fixed base damage, zero attribute requirements, and no scaling per Doc 09 §15. Add IsTrainingWeapon operational lifecycle flag and validation in LootDefinition, register definitions in LootDefinitionCatalog, and cover parameters and catalog integration with EditMode tests.

Player-Added: Added basic training short sword, dagger, and wand for contingency expeditions.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Weapons/TrainingDagger.png
Project Grimhold/Assets/Art/Weapons/TrainingDagger.png.meta
Project Grimhold/Assets/Art/Weapons/TrainingSword.png
Project Grimhold/Assets/Art/Weapons/TrainingSword.png.meta
Project Grimhold/Assets/Art/Weapons/TrainingWand.png
Project Grimhold/Assets/Art/Weapons/TrainingWand.png.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDagger.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDagger.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSword.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSword.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWand.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWand.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset.meta
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingDagger.png
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingDagger.png.meta
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingSword.png
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingSword.png.meta
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingWand.png
- path → Combat: Project Grimhold/Assets/Art/Weapons/TrainingWand.png.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- scope → Combat: weapons
- message-term → Combat: weapons
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDagger.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDagger.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingDaggerWeaponDefinition.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSword.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSword.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingShortSwordWeaponDefinition.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWand.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/TrainingWandWeaponDefinition.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingDagger.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingDagger.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingSword.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingSword.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingWand.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Weapons/TrainingWand.png.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### bde28bdcc1ef55562950e5f7c7562740f17b7d5e

[bde28bdcc1ef55562950e5f7c7562740f17b7d5e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/bde28bdcc1ef55562950e5f7c7562740f17b7d5e)

Date: 2026-11-07T23:08:42-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(town): lock build, equipment and stash while Ready
```

#### Original body

```text
Ready now locks the exact prepared Town build (Game Design 02 §7.2)
until the player returns to NotReady. Quick Slot bindings and Stash
Sort stay available.

- TownRaidPreparationRules.CanMutateTownBuild is the single rule: only
  a Waiting member who is not Ready may change the build; a frozen
  launch, a non-member or an invalid snapshot stays locked.
  TownEquipmentMutationGate delegates to it for every Town screen.
- LobbyStashPresenter refuses transfers, drags, splits, gem installs
  and Equipment changes while locked, closes Take All/Leave All, shows
  a persistent locked line and reopens as soon as Ready is cleared.
- The attribute panel marks "Locked while Ready" and follows the gate
  while open (e.g. a Host cancel resetting Ready).

Town inventory is written through LocalProfileStore and the backend
HTTP clients, not Fusion RPCs, so the lock lives at the client
mutation endpoints; backend enforcement is a follow-up.

Tests:
- TownRaidPreparationRulesTests: Ready lock/unlock, per-member scope,
  non-member, invalid and frozen snapshots.
- TownAttributeAssignmentPresentationTests: IsBuildLocked.
- LobbyStashPresenterPlayModeTests: Ready blocks every transfer, shows
  the locked line, keeps Sort, NotReady restores transfers.

Docs: TownRaidQueueArchitecture.md documents the Ready build lock.

Refs: TASK-266
Player-Improved: While you are Ready in Town your build, equipment and Stash are locked with a clear notice, and your Quick Slots can still be rearranged.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationRules.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownAttributeAssignmentPresentation.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownAttributeAssignmentPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownAttributeAssignmentView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownEquipmentMutationGate.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownRaidPreparationRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Progression/TownAttributeAssignmentPresentationTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
odd/tasks/task-284-stun.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownEquipmentMutationGate.cs
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationRules.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownRaidPreparationRulesTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- path → UI: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownRaidPreparationRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Progression/TownAttributeAssignmentPresentationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/TownRaidQueueArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/TownRaidPreparationRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Progression/TownAttributeAssignmentPresentationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### d074a912f9b0012a63ae2f255f4da93177155bff

[d074a912f9b0012a63ae2f255f4da93177155bff](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d074a912f9b0012a63ae2f255f4da93177155bff)

Date: 2026-11-07T22:34:24-03:00 · Classification: Features (explicit) · Systems: Build & Tooling, Documentation

Flags: primary or unclassified

#### Original subject

```text
feat(changelog): support live development changelog and segment historical sprint tags
```

#### Original body

```text
Update changelog renderer and CLI to display unreleased development notes under a live section directly in CHANGELOG.md without waiting for production release tags. Update changelog workflow to publish all changelogs on push, synchronize Unity bundleVersion to 0.6.0-dev, and regenerate archives based on annotated sprint tags.
```

#### Changed paths

```text
.github/workflows/changelog.yml
CHANGELOG.md
CHANGELOG_DEV.md
Project Grimhold/ProjectSettings/ProjectSettings.asset
changelog-players.json
docs/changelog/README.md
docs/changelog/generated-files.json
docs/changelog/history-2026-07.md
docs/changelog/report.json
docs/changelog/unreleased.md
docs/changelog/v0.2.0.md
docs/changelog/v0.3.0.md
docs/changelog/v0.4.0.md
docs/changelog/v0.5.0.md
tools/changelog/src/cli.js
tools/changelog/src/render-players.js
```

#### Classification evidence

- type → Features: feat
- path → Build &amp; Tooling: .github/workflows/changelog.yml
- path → Build &amp; Tooling: CHANGELOG.md
- path → Build &amp; Tooling: CHANGELOG\_DEV.md
- path → Build &amp; Tooling: changelog-players.json
- path → Build &amp; Tooling: docs/changelog/README.md
- path → Build &amp; Tooling: docs/changelog/generated-files.json
- path → Build &amp; Tooling: docs/changelog/history-2026-07.md
- path → Build &amp; Tooling: docs/changelog/report.json
- path → Build &amp; Tooling: docs/changelog/unreleased.md
- path → Build &amp; Tooling: docs/changelog/v0.2.0.md
- path → Build &amp; Tooling: docs/changelog/v0.3.0.md
- path → Build &amp; Tooling: docs/changelog/v0.4.0.md
- path → Build &amp; Tooling: docs/changelog/v0.5.0.md
- path → Build &amp; Tooling: tools/changelog/src/cli.js
- path → Build &amp; Tooling: tools/changelog/src/render-players.js
- scope → Build &amp; Tooling: changelog
- message-term → Build &amp; Tooling: changelog
- path → Documentation: docs/changelog/README.md
- path → Documentation: docs/changelog/generated-files.json
- path → Documentation: docs/changelog/history-2026-07.md
- path → Documentation: docs/changelog/report.json
- path → Documentation: docs/changelog/unreleased.md
- path → Documentation: docs/changelog/v0.2.0.md
- path → Documentation: docs/changelog/v0.3.0.md
- path → Documentation: docs/changelog/v0.4.0.md
- path → Documentation: docs/changelog/v0.5.0.md

#### Diagnostics

- No classification ambiguity detected.

### aaaff9161561130d938c8973e81f490679826404

[aaaff9161561130d938c8973e81f490679826404](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/aaaff9161561130d938c8973e81f490679826404)

Date: 2026-11-07T22:33:04-03:00 · Classification: Maintenance (explicit) · Systems: Abilities, Build & Tooling

Flags: secondary

#### Original subject

```text
chore(skills): refine hacknplan task design to focus on scope
```

#### Original body

```text
Update hacknplan-task-design skill and templates to enforce functional scope definition and prevent prescribing technical implementation recipes in tickets.
```

#### Changed paths

```text
.agents/skills/hacknplan-task-design/SKILL.md
.agents/skills/hacknplan-task-design/assets/story-template.md
.agents/skills/hacknplan-task-design/assets/task-template.md
.agents/skills/hacknplan-task-design/references/workflow.md
```

#### Classification evidence

- type → Maintenance: chore
- path → Abilities: .agents/skills/hacknplan-task-design/SKILL.md
- path → Abilities: .agents/skills/hacknplan-task-design/assets/story-template.md
- path → Abilities: .agents/skills/hacknplan-task-design/assets/task-template.md
- path → Abilities: .agents/skills/hacknplan-task-design/references/workflow.md
- scope → Abilities: skills
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/SKILL.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/assets/story-template.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/assets/task-template.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/references/workflow.md
- scope → Build &amp; Tooling: skills

#### Diagnostics

- No classification ambiguity detected.

### 7c36b1a544a5d3f727e00b74b5738139ae3c1e4a

[7c36b1a544a5d3f727e00b74b5738139ae3c1e4a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/7c36b1a544a5d3f727e00b74b5738139ae3c1e4a)

Date: 2026-11-07T22:30:20-03:00 · Classification: Features (explicit) · Systems: Combat, Abilities, Movement, Inventory & Loot, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(abilities): add authoritative Stun for players and creatures
```

#### Original body

```text
Add a networked, State Authority-owned Stun for player avatars and
hostile creatures, apply it from Charge (Embestida) impacts and make it
a purifiable state (TASK-284).

- PlayerStunNetworkEffect holds a copied ExpiryTimer; reapplication
  replaces it. Applying it interrupts active abilities with
  AbilityExecutionStopReason.Stun (no cost/cooldown refund) and ends
  the running consumable, lockpicking and assisted revive. Purification
  protection refuses it; Downed or defeat clears it.
- PlayerStunGate joins the existing Downed/consumption choke points:
  ability start, weapon attacks, shield, Weapon Set change, consumables
  (new ConsumableFailureReason.Stunned = 14), interaction and loot
  drop/split/transfer. Voluntary movement, Sprint and aim stance are
  suppressed; knockback and forced displacement still apply.
- Purification reports and purges Stun; a Stunned caster is rejected
  by the common start gate while an ally's Purification removes it.
- EnemyStunNetworkEffect on every enemy prefab blocks voluntary
  movement and attacks and cancels the queued hit. The Specter cancels
  an active channel without detonating, returns to Persecucion and
  defers reactive displacement until the Stun ends.
- Charge applies a configurable Stun (_stunSeconds, default 1 s, 0
  disables) to the hit creature after damage when it survives.
- IStunStatus exposes IsActive/RemainingSeconds for HUD and a
  provisional StunStatusIndicatorPresenter shows a yellow marker.
- Prefabs composed with minimal YAML edits.

Tests: player Stun gates, interruption, expiry, protection and
Purification (SupportAbilitiesPlayModeTests); enemy/Specter Stun and
indicator (ImmobilizeEffectPlayModeTests); Charge Stun
(ChargeAbilityPlayModeTests); prefab composition
(PlayerAbilityRuntimeCompositionTests, AbilityEnemyTargetMarkerTests).

Docs: AbilitySystemArchitecture (Stun section, Charge, Purification)
and PlayerMovementArchitecture §20.3; execution tracked in
odd/tasks/task-284-stun.md.

Player-Added: Charge now stuns the enemy it hits for a moment, stopping it from moving or attacking and cancelling the Specter's area attack.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab
Project Grimhold/Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab
Project Grimhold/Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab
Project Grimhold/Assets/Prefabs/Enemies/Slimes/RedSlime.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Scripts/Abilities/Behaviours/ChargeAbilityBehaviour.cs
Project Grimhold/Assets/Scripts/Abilities/Behaviours/PurificationAbilityBehaviour.cs
Project Grimhold/Assets/Scripts/Abilities/Effects/EnemyStunNetworkEffect.cs
Project Grimhold/Assets/Scripts/Abilities/Effects/EnemyStunNetworkEffect.cs.meta
Project Grimhold/Assets/Scripts/Abilities/Effects/IStunStatus.cs
Project Grimhold/Assets/Scripts/Abilities/Effects/IStunStatus.cs.meta
Project Grimhold/Assets/Scripts/Abilities/Effects/PlayerStunNetworkEffect.cs
Project Grimhold/Assets/Scripts/Abilities/Effects/PlayerStunNetworkEffect.cs.meta
Project Grimhold/Assets/Scripts/Abilities/Effects/PurificationNetworkEffect.cs
Project Grimhold/Assets/Scripts/Abilities/Runtime/PlayerAbilityRuntimeNetworkController.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterAIController.cs
Project Grimhold/Assets/Scripts/Gameplay/Consumables/ConsumableFailureReason.cs
Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerShieldDefenseNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerStunGate.cs
Project Grimhold/Assets/Scripts/Player/PlayerStunGate.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/StunStatusIndicatorPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/StunStatusIndicatorPresenter.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/AbilityEnemyTargetMarkerTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/PlayerAbilityRuntimeCompositionTests.cs
Project Grimhold/Assets/Tests/PlayMode/ChargeAbilityPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/ImmobilizeEffectPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/SupportAbilitiesPlayModeTests.cs
Project Grimhold/Docs/Architecture/AbilitySystemArchitecture.md
Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
odd/tasks/task-284-stun.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerShieldDefenseNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- message-term → Combat: weapon
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Behaviours/ChargeAbilityBehaviour.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Behaviours/PurificationAbilityBehaviour.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/EnemyStunNetworkEffect.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/EnemyStunNetworkEffect.cs.meta
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/IStunStatus.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/IStunStatus.cs.meta
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/PlayerStunNetworkEffect.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/PlayerStunNetworkEffect.cs.meta
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Effects/PurificationNetworkEffect.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Abilities/Runtime/PlayerAbilityRuntimeNetworkController.cs
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/AbilityEnemyTargetMarkerTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/PlayerAbilityRuntimeCompositionTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/ChargeAbilityPlayModeTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/SupportAbilitiesPlayModeTests.cs
- path → Abilities: Project Grimhold/Docs/Architecture/AbilitySystemArchitecture.md
- scope → Abilities: abilities
- message-term → Abilities: abilities
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/EnemyMovementAIController.cs
- path → Movement: Project Grimhold/Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs
- path → Movement: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- message-term → Movement: movement
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInteractionNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- message-term → Inventory &amp; Loot: interaction
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
- message-term → UI: HUD
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/AbilityEnemyTargetMarkerTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/PlayerAbilityRuntimeCompositionTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/AbilitySystemArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerMovementArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/AbilityEnemyTargetMarkerTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Abilities/PlayerAbilityRuntimeCompositionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/ChargeAbilityPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/ImmobilizeEffectPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/SupportAbilitiesPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 93e9bcb3b91e96a60a1df5f6e50c01ec9eeb732f

[93e9bcb3b91e96a60a1df5f6e50c01ec9eeb732f](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/93e9bcb3b91e96a60a1df5f6e50c01ec9eeb732f)

Date: 2026-11-07T21:07:33-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, World & Visibility, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(inventory): add quantity selector for stack split and partial drop
```

#### Original body

```text
Add a numeric quantity selector dialog (slider synced with a TMP input,
range [1, stack - 1], Confirm/Cancel) and use it for "Dividir" in the
Raid inventory and Town stash and for "Soltar cantidad..." in the Raid
inventory (TASK-282).

- Partial drop sends the requested amount as an intention through the
  new LootTransferQuantityMode.ExactAmount; State Authority accepts only
  slot-addressed amounts in [1, slot quantity - 1] and spawns the pickup
  with that exact amount.
- Raid split goes through a new Input Authority request,
  PlayerLootSplitNetworkController, into PlayerLootReceiver.TrySplitStack;
  Stash split uses StashTransferCoordinator.SplitAsync. Both target the
  lowest-index empty slot and disable "Dividir" when none is available.
- Author the dialog prefab with an idempotent Editor menu item and wire
  it into NetworkPlayer, RaidInventoryUI and StashInventory with minimal
  prefab edits.
- Update the inventory, loot interaction and persistence architecture
  docs, and track execution in odd/tasks/task-282-quantity-selector-partial-drop.md.
```

#### Changed paths

```text
.pi/subagents/unity-operator.md
Project Grimhold/Assets/Editor/Task282Composition.cs
Project Grimhold/Assets/Editor/Task282Composition.cs.meta
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
Project Grimhold/Assets/Prefabs/UI/QuantitySelectorDialog.prefab
Project Grimhold/Assets/Prefabs/UI/QuantitySelectorDialog.prefab.meta
Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs
Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootSplitContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LootSplitContextActionProvider.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/QuantitySelectorDialogView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/QuantitySelectorDialogView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/QuantitySelectorRange.cs
Project Grimhold/Assets/Scripts/Player/Presentation/QuantitySelectorRange.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
Project Grimhold/Assets/Scripts/Stash/StashTransferCoordinator.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RpcAcceptanceContractTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionCompositionConfigurationTests.cs
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
odd/tasks/task-282-quantity-selector-partial-drop.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/EnemyCombatAIController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityMode.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferQuantityResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropConfirmation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootDropRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootSplitNetworkController.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootDropContextActionProvider.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootSplitContextActionProvider.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LootSplitContextActionProvider.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RpcAcceptanceContractTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionCompositionConfigurationTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/QuantitySelectorDialog.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/QuantitySelectorDialog.prefab.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- path → UI: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- message-term → UI: menu
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/Task282Composition.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/Task282Composition.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RpcAcceptanceContractTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionCompositionConfigurationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/InventoryWorldDropArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootContextActionProviderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDropExactAmountTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootSplitContextActionProviderTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/QuantitySelectorRangeTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RpcAcceptanceContractTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/SessionCompositionConfigurationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 5ea17995d1b8a1f8c73b33240927566a884bd0ad

[5ea17995d1b8a1f8c73b33240927566a884bd0ad](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5ea17995d1b8a1f8c73b33240927566a884bd0ad)

Date: 2026-11-07T21:01:23-03:00 · Classification: Bug Fixes (explicit) · Systems: Combat, Extraction, Inventory & Loot, Networking & Sessions, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(persistence): reserve recovery weapons and rehydrate extracted gear
```

#### Original body

```text
Two failures blocked the Town -> raid -> Town -> raid cycle.

Recovery weapon: the weapon granted when none is equipped is now
prepared with a backend-issued instance, waits for the backend
reservation and a revision-consistent hydration, and is retired once
the matching reservation is confirmed. Town preparation resumes a
pending launch before its unchanged-revision early return, and rollback
is guarded by the expected reservation ID in Unity and the backend.

Extraction: Unity discarded the instance IDs the backend issues on the
extraction commit, and the failed clear-pending write was ignored, so
the next reservation rejected ID-free equipment. After a successful
commit, the live path and the login retry fetch authoritative inventory
and progression, and LocalProfileStore.TryCompleteExtractionCommit
applies it and clears the pending extraction in one validated write.
Failures keep the pending payload for retry; no IDs are fabricated.

Tests:
- RecoveryWeaponStrictRepositoryTests
- PostExtractionRehydrationTests: success, write failure, missing
  instance, revision mismatch, receipt, reservation and ack guards
- backend inventory.reservation.test.js: guarded rollback

Docs: LocalPlayerPersistenceArchitecture.md covers recovery
reservation, guarded rollback, extraction acknowledgement and two known
limitations left as follow-up.

Player-Fixed: After extracting, you can start another raid without the loadout failing to reserve.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
Project Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs
Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs
Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs.meta
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/ProfileReconciliationService.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs
Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs.meta
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
backend/src/routes/inventory.routes.js
backend/src/services/InventoryService.js
backend/tests/inventory.reservation.test.js
odd/tasks/raid-admission-loadout-fix.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Combat: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
- path → Combat: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/IRecoveryWeaponReservationService.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs
- path → Combat: Project Grimhold/Assets/Scripts/Stash/RecoveryWeaponAuthorityState.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- message-term → Combat: weapons
- path → Extraction: Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs.meta
- message-term → Extraction: Extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/tests/inventory.reservation.test.js
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RecoveryWeaponReservationPreparation.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/SessionConnectionCoordinator.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownRaidPreparationNetworkController.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileReconciliationService.cs
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/tests/inventory.reservation.test.js
- scope → Persistence &amp; Backend: persistence
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PostExtractionRehydrationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponStrictRepositoryTests.cs.meta
- path → Tests: backend/tests/inventory.reservation.test.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### e439345666bc80eabcdd1191fa6e47895cc94700

[e439345666bc80eabcdd1191fa6e47895cc94700](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e439345666bc80eabcdd1191fa6e47895cc94700)

Date: 2026-11-07T21:01:22-03:00 · Classification: Bug Fixes (explicit) · Systems: Combat, Extraction, Inventory & Loot, Networking & Sessions, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(inventory): keep equipped gear out of the 30-slot raid backpack
```

#### Original body

```text
Equipped weapons and armor were counted against the 30-slot backpack
when reserving a loadout and when admitting the avatar, so a full
backpack plus equipment was rejected after an extraction (31 > 30).
The inventory design keeps Equipment separate from the backpack.

- LocalProfileStore counts only backpack physical slots: equipment
  units individually, other items by StackMax.
- RaidAdmissionData.TryProjectLoadout splits the reserved payload into
  equipment units and residual backpack, validating references,
  transport limits and backpack capacity before any write.
- NetworkSpawnManager uses that projection at all five admission gates
  and, on a fresh avatar, equips through the existing Equipment
  initializer before filling the backpack.

A true backpack overflow is still rejected without side effects.

Tests:
- PhysicalProfileSlotTests: full backpack plus equipment, true overflow
- PlayerEquipmentPlayModeTests: RaidAdmissionCapacity_ cases

Player-Fixed: You can start a raid with a full backpack while wearing equipment.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- message-term → Combat: weapons
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 46be0d2c21bc74fa1b33cc4bde892bb32c40b33d

[46be0d2c21bc74fa1b33cc4bde892bb32c40b33d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/46be0d2c21bc74fa1b33cc4bde892bb32c40b33d)

Date: 2026-11-07T21:01:22-03:00 · Classification: Bug Fixes (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(raid): match reserved loadouts exactly on raid admission
```

#### Original body

```text
Raid admission compared the reserved loadout against the profile with a
lossy check, so loadouts with split stacks, shared item references or
modified equipment could be rejected. Admission now delegates to
RaidAdmissionLoadoutMatcher, which compares combined quantities per
item, the modifier multiset, exact Equipment slots and modifiers, and
the active weapon set, while keeping the existing profile and
reservation guards.

Tests:
- RaidAdmissionLoadoutMatcherTests
- RaidAdmissionSlotAggregationTests reuses the shared matcher helper

Player-Fixed: Joining a raid no longer fails when your loadout has split stacks or modified gear.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionLoadoutMatcher.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionLoadoutMatcher.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- message-term → Combat: weapon
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionLoadoutMatcher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionLoadoutMatcher.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionLoadoutMatcherTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 970dba9ad8b937d7993400dc9d77404756168d56

[970dba9ad8b937d7993400dc9d77404756168d56](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/970dba9ad8b937d7993400dc9d77404756168d56)

Date: 2026-11-07T01:00:42-03:00 · Classification: Other Changes (unknown) · Systems: Inventory & Loot, Build & Tooling

Flags: primary or unclassified

#### Original subject

```text
Remove legacy agent configurations
```

#### Original body

```text
Delete obsolete Claude and Codex agent configuration files and record the completed inventory schema v3 cutover in the merchant trade execution tracker.
```

#### Changed paths

```text
.claude/agents/architect.md
.claude/agents/explorer.md
.claude/agents/implementer.md
.claude/agents/reviewer.md
.claude/agents/validator.md
.codex/agents/grimhold_architect.toml
.codex/agents/grimhold_explorer.toml
.codex/agents/grimhold_implementer.toml
.codex/agents/grimhold_reviewer.toml
.codex/agents/grimhold_validator.toml
.codex/config.toml
odd/tasks/merchant-atomic-trade.md
```

#### Classification evidence

- message-term → Inventory &amp; Loot: inventory
- path → Build &amp; Tooling: .claude/agents/architect.md
- path → Build &amp; Tooling: .claude/agents/explorer.md
- path → Build &amp; Tooling: .claude/agents/implementer.md
- path → Build &amp; Tooling: .claude/agents/reviewer.md
- path → Build &amp; Tooling: .claude/agents/validator.md
- path → Build &amp; Tooling: .codex/agents/grimhold\_architect.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_explorer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_implementer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_reviewer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_validator.toml

#### Diagnostics

- **ambiguous-kind:** No supported explicit type or anchored historical action. Inspect this commit diff; use an action and object or a Conventional Commit type in future messages.

### 828cd871e995dabd9e267aea343c24684872e763

[828cd871e995dabd9e267aea343c24684872e763](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/828cd871e995dabd9e267aea343c24684872e763)

Date: 2026-11-07T00:33:23-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(commerce): accept schema v3 inventories in merchant trades
```

#### Original body

```text
RemoteShopTransactionService refused every trade and buy-back unless
the local inventory was exactly schema v2, silently returning
PersistenceFailed before calling the backend, so v3 characters could
not trade at all.

- Accept schema 2 and 3; any other version still fails, now with a
  warning that names the unsupported version.
- Apply v3 acknowledgements from the backend snapshot (loadout with
  backend-issued instances, stash, currency, prepared equipment and
  revision) and record the shop receipt in the same commit, since the
  local plan cannot reproduce issued instances or gem sale value.
- Log the backend error code when a trade is rejected.

Player-Fixed: Characters on the item-instance inventory can buy and sell again.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/RemoteShopTransactionService.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
odd/tasks/merchant-atomic-trade.md
```

#### Classification evidence

- type → Bug Fixes: fix
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 1d8238334613ec4406413ccbd590a1a95bbbda9f

[1d8238334613ec4406413ccbd590a1a95bbbda9f](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1d8238334613ec4406413ccbd590a1a95bbbda9f)

Date: 2026-11-06T18:08:53-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, World & Visibility, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(enemies): implement Catacombs Specter FSM, combat, and loot
```

#### Original body

```text
Add the Catacombs Specter (Espectro de Catacumbas) as the third hostile
enemy archetype of the MVP.

The Specter features an authoritative 9-state FSM with alternating
incorporeity (strict physical damage immunity during hovering and chase;
vulnerability during attack channel, detonation, and 1.5s recovery;
magic damage always applies in visible states). Attacks place a static
1.5m telegraph area that detonates after channeling, subject to wall
linecast occlusion. Reactive displacement triggers on non-lethal magic
damage with a 4s internal cooldown, repositioning to an annular band
(3-5m) around the highest accumulated damage target on its Hate List.
Loot table awards Spectral Essence, Soul Fragments, and Chaos Coins,
suppressed during forced Collapse reinforcements.

Author NetworkEnemySpecter.prefab as a Prefab Variant of
NetworkEnemy.prefab, indexed at Fusion Prefab Table index 13 and wired
into Systems.prefab.

Cover with 27 unit test cases in SpecterRulesAndCombatTests.

Player-Added: Added the Catacombs Specter enemy with alternating physical immunity, ground telegraph attacks, and spectral relocation.
Refs: US-78, TASK-272, TASK-273, TASK-274, TASK-275
```

#### Changed paths

```text
Project Grimhold/Assets/Editor/AuthorSpecterPrefab.cs
Project Grimhold/Assets/Editor/AuthorSpecterPrefab.cs.meta
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab.meta
Project Grimhold/Assets/Prefabs/Systems.prefab
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset.meta
Project Grimhold/Assets/Scripts/Character/CharacterBase.cs
Project Grimhold/Assets/Scripts/Character/EnemyCharacter.cs
Project Grimhold/Assets/Scripts/Character/Specter.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterAIController.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterAIController.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterAttackOcclusion.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterAttackOcclusion.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterHateList.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterHateList.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterRules.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterRules.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterStateType.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterStateType.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterTeleportRules.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterTeleportRules.cs.meta
Project Grimhold/Assets/Scripts/Character/Specter/SpecterVisualPresenter.cs
Project Grimhold/Assets/Scripts/Character/Specter/SpecterVisualPresenter.cs.meta
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Tests/EditMode/Enemies.meta
Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs
Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs.meta
odd/tasks/catacombs-specter-enemy.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Character/Specter/SpecterAttackOcclusion.cs
- path → Combat: Project Grimhold/Assets/Scripts/Character/Specter/SpecterAttackOcclusion.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs.meta
- message-term → Combat: combat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset.meta
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → World &amp; Visibility: odd/tasks/catacombs-specter-enemy.md
- message-term → World &amp; Visibility: Catacombs
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/AuthorSpecterPrefab.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/AuthorSpecterPrefab.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Enemies.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Enemies/SpecterRulesAndCombatTests.cs.meta
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 3321d75a889aaa26e275af4b1f2b49681cb0a1c2

[3321d75a889aaa26e275af4b1f2b49681cb0a1c2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3321d75a889aaa26e275af4b1f2b49681cb0a1c2)

Date: 2026-11-06T18:28:26-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, Networking & Sessions, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(commerce): confirm multi-line merchant carts in one backend trade
```

#### Original body

```text
The merchant rejected any cart with more than one line before calling
the backend, so players could only sell or buy one item per trade.

Remove the single-line guards in TownMerchantTradeSession and
RemoteShopTransactionService and submit every normal ticket, mixed or
single-line, through the atomic /shop/trade endpoint. The dry run,
local commit, revision, buyback ledger and reconciliation flow are
unchanged; buy-back still confirms alone.

- Update the commerce architecture contract and track the work in
  odd/tasks/merchant-atomic-trade.md.
- Requires the backend /shop/trade endpoint to be deployed first.

Player-Fixed: The merchant accepts carts with several items to buy and sell.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownMerchantTradeSession.cs
Project Grimhold/Assets/Scripts/Stash/RemoteShopTransactionService.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/TownMerchantTradeSessionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
Project Grimhold/Assets/Tests/PlayMode/TownMerchantTradePlayModeTests.cs
Project Grimhold/Docs/Architecture/CommerceArchitecture.md
odd/tasks/merchant-atomic-trade.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Player/Presentation/TownMerchantTradeSession.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/TownMerchantTradeSessionTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/TownMerchantTradeSessionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/CommerceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/TownMerchantTradeSessionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/TownMerchantTradePlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### ec13a1145b7456bab5fd12251b58b2be9f7b1e8c

[ec13a1145b7456bab5fd12251b58b2be9f7b1e8c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ec13a1145b7456bab5fd12251b58b2be9f7b1e8c)

Date: 2026-11-06T18:28:15-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): add atomic multi-line shop trade endpoint
```

#### Original body

```text
Add POST /character/me/inventory/shop/trade so a Town merchant cart
with several purchases and sales confirms as one operation. Sales are
applied first, then purchases in ticket order, so funds and capacity
are validated on the final state. The whole ticket is written in one
revision-guarded update with a single 'trade' receipt bound to the
ordered lines; any failing line rejects the ticket without mutation.

- Share the per-line sale and purchase helpers with /shop/buy and
  /shop/sell without changing their behavior.
- Validate tickets strictly: no declared prices, 16 purchase and 30
  sale lines, unique loot ids per side and unique sale slots.
- Add 'trade' to the shop receipt type enum.
- Document the request, replay and error contract in the README.
```

#### Changed paths

```text
backend/README.md
backend/src/config/shopCatalog.js
backend/src/models/Character.js
backend/src/routes/inventory.routes.js
backend/src/services/InventoryService.js
backend/src/validators/inventory.validators.js
backend/tests/integration/shop.trade.test.js
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/src/validators/inventory.validators.js
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: backend/README.md
- path → Persistence &amp; Backend: backend/src/config/shopCatalog.js
- path → Persistence &amp; Backend: backend/src/models/Character.js
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/validators/inventory.validators.js
- path → Persistence &amp; Backend: backend/tests/integration/shop.trade.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/tests/integration/shop.trade.test.js

#### Diagnostics

- No classification ambiguity detected.

### a65052aee75645a27fcdf7df16aa503b588d16b1

[a65052aee75645a27fcdf7df16aa503b588d16b1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a65052aee75645a27fcdf7df16aa503b588d16b1)

Date: 2026-11-06T17:45:36-03:00 · Classification: Maintenance (explicit) · Systems: Combat, Inventory & Loot, Art & Animation

Flags: secondary

#### Original subject

```text
chore(unity): refresh editor-dependent data for Unity 6000.6.8f1
```

#### Original body

```text
- Regenerate the Off Hand attack sets with the project generator; only
  _retargetSourceSignature changes, because the dependency hash it
  records depends on the editor version.
- Skip the Void Bell standalone chime fallback outside Play Mode, where
  AudioSource.PlayClipAtPoint destroys its temporary source with
  Destroy and Unity 6000.6 reports an error.
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Dagger_OffHand.asset
Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/MagicSword_OffHand.asset
Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Rapier_OffHand.asset
Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Sword1H_OffHand.asset
Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Wand_OffHand.asset
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
```

#### Classification evidence

- type → Maintenance: chore
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Dagger\_OffHand.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/MagicSword\_OffHand.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Rapier\_OffHand.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Sword1H\_OffHand.asset
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Wand\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Dagger\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/MagicSword\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Rapier\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Sword1H\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Wand\_OffHand.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Dagger\_OffHand.asset
- path → Art &amp; Animation: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/MagicSword\_OffHand.asset
- path → Art &amp; Animation: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Rapier\_OffHand.asset
- path → Art &amp; Animation: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Sword1H\_OffHand.asset
- path → Art &amp; Animation: Project Grimhold/Assets/Scriptable Objects/Loot/AttackAnimationSets/Wand\_OffHand.asset

#### Diagnostics

- No classification ambiguity detected.

### 02094eacb71564950737ef37c5237def38547688

[02094eacb71564950737ef37c5237def38547688](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/02094eacb71564950737ef37c5237def38547688)

Date: 2026-11-06T17:45:36-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(stash): accept null optional records in inventory hydration
```

#### Original body

```text
New accounts could not log in or create a character: the backend
returns "pendingReservation": null and "lastAppliedExtractionReceipt":
null for a fresh inventory, and Newtonsoft cannot assign null to those
struct fields, so InventoryClient reported INVALID_INVENTORY and login
showed "Could not load your inventory. Please try again."

Ignore JSON null for both optional records so they keep their default
struct, matching the previous JsonUtility behavior. Populated v2/v3
payloads are unchanged.

- Add a regression test with the exact HTTP 200 body returned by the
  deployed backend for a new character.

Player-Fixed: New accounts can log in and create their first character again.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 55f44fc6dd1daf96538d9a7005ea06f07ae77d87

[55f44fc6dd1daf96538d9a7005ea06f07ae77d87](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/55f44fc6dd1daf96538d9a7005ea06f07ae77d87)

Date: 2026-11-06T16:47:52-03:00 · Classification: Documentation (explicit) · Systems: Abilities, Build & Tooling, Documentation

Flags: secondary

#### Original subject

```text
docs(tooling): route agent Unity Editor access through Unity CLI
```

#### Original body

```text
Replace the removed CoplayDev Unity MCP with the official Unity CLI in
every live instruction: repository and Unity AGENTS files, project
skills, Claude/Codex/Pi agent definitions and the release version
presentation doc.

- Record Unity 6000.6.8f1 with com.unity.pipeline 0.10.0-exp.1 and
  forbid invoking or reinstalling Unity MCP; other MCP servers stay.
- Point Editor work to the unity-cli skill and the CLI's own command
  search instead of guessed commands.
- Give the Pi unity-operator subagent bash so it can run the CLI.
- Bump grimhold-task-execution to 1.2.

Historical trackers, walkthroughs, changelogs and audits keep their
original Unity MCP evidence.
```

#### Changed paths

```text
.agents/skills/grimhold-task-execution/SKILL.md
.agents/skills/grimhold-task-execution/references/tool-routing.md
.agents/skills/hacknplan-task-design/references/workflow.md
.claude/agents/implementer.md
.claude/agents/validator.md
.codex/agents/grimhold_explorer.toml
.codex/agents/grimhold_validator.toml
.pi/subagents/unity-operator.md
AGENTS.md
Project Grimhold/AGENTS.md
Project Grimhold/Docs/ReleaseVersionPresentation.md
```

#### Classification evidence

- type → Documentation: docs
- path → Abilities: .agents/skills/grimhold-task-execution/SKILL.md
- path → Abilities: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Abilities: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/SKILL.md
- path → Build &amp; Tooling: .agents/skills/grimhold-task-execution/references/tool-routing.md
- path → Build &amp; Tooling: .agents/skills/hacknplan-task-design/references/workflow.md
- path → Build &amp; Tooling: .claude/agents/implementer.md
- path → Build &amp; Tooling: .claude/agents/validator.md
- path → Build &amp; Tooling: .codex/agents/grimhold\_explorer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_validator.toml
- path → Build &amp; Tooling: AGENTS.md
- path → Build &amp; Tooling: Project Grimhold/AGENTS.md
- scope → Build &amp; Tooling: tooling
- path → Documentation: Project Grimhold/Docs/ReleaseVersionPresentation.md

#### Diagnostics

- No classification ambiguity detected.

### 5b4ad2940246ba32e3acefea663b553572f1bf69

[5b4ad2940246ba32e3acefea663b553572f1bf69](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5b4ad2940246ba32e3acefea663b553572f1bf69)

Date: 2026-11-06T16:35:53-03:00 · Classification: Maintenance (explicit) · Systems: Combat, Inventory & Loot, UI, Art & Animation, Input, Build & Tooling, Tests

Flags: secondary

#### Original subject

```text
chore(unity): upgrade to Unity 6000.6.8f1 and Unity CLI pipeline
```

#### Original body

```text
Move the project from Unity 6000.5.1f1 to 6000.6.8f1, as agreed by the
team, and replace the Unity MCP bridge with the Unity CLI Pipeline
package for agent-driven Editor access.

- Bump ProjectVersion and the editor-bundled packages (2D, URP 17.6.0,
  Input System 1.20.1, Test Framework 1.8.0, Timeline 6.6.0, uGUI 2.6.0
  and others) with the matching packages-lock.json.
- Add com.unity.pipeline 0.10.0-exp.1 and com.unity.modules.tetgen;
  remove com.coplaydev.unity-mcp.
- Keep the serialization the new versions write: URP global settings
  (asset version 11), Project Auditor params, Aseprite importer fields
  and the regenerated PlayerInputActions.cs.
- Drop the Unity MCP server from .pi/mcp.json and its tool permissions
  from .claude/settings.json.
```

#### Changed paths

```text
.claude/settings.json
.pi/mcp.json
Project Grimhold/Assets/Art/UI/Charcoal/EquipmentCategories/equipment-categories.aseprite.meta
Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
Project Grimhold/Assets/Input/PlayerInputActions.cs
Project Grimhold/Assets/UniversalRenderPipelineGlobalSettings.asset
Project Grimhold/Packages/manifest.json
Project Grimhold/Packages/packages-lock.json
Project Grimhold/ProjectSettings/ProjectAuditorSettings.asset
Project Grimhold/ProjectSettings/ProjectVersion.txt
```

#### Classification evidence

- type → Maintenance: chore
- path → Combat: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Art/UI/Charcoal/EquipmentCategories/equipment-categories.aseprite.meta
- path → UI: Project Grimhold/Assets/Art/UI/Charcoal/EquipmentCategories/equipment-categories.aseprite.meta
- path → UI: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/Charcoal/EquipmentCategories/equipment-categories.aseprite.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/UI/ScreenDamage/screen-damage-source.aseprite.meta
- path → Input: Project Grimhold/Assets/Input/PlayerInputActions.cs
- message-term → Input: Input System
- path → Build &amp; Tooling: Project Grimhold/Packages/manifest.json
- path → Build &amp; Tooling: Project Grimhold/Packages/packages-lock.json
- message-term → Tests: Test

#### Diagnostics

- No classification ambiguity detected.

### 6e28570a7c65ce94ee250b9e98fb63a5d21b7624

[6e28570a7c65ce94ee250b9e98fb63a5d21b7624](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6e28570a7c65ce94ee250b9e98fb63a5d21b7624)

Date: 2026-11-06T15:08:10-03:00 · Classification: Features (explicit) · Systems: Combat, Abilities, Inventory & Loot, Networking & Sessions, UI, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): roll dungeon weapon variants in enemy and breakable drops
```

#### Original body

```text
Table-rolled enemy drops pass their roll seed to the container, which rolls one variant per weapon unit when it commits the pre-spawn content. Breakable drops roll a variant for table-rolled single-weapon pickups; exact drops and stacks stay plain.

Refs: TASK-283
```

#### Changed paths

```text
.agents/skills/create-game-assets/SKILL.md
.agents/skills/create-game-assets/agents/openai.yaml
.agents/skills/create-game-assets/assets/art-direction-brief.md
.agents/skills/create-game-assets/assets/asset-manifest.json
.agents/skills/create-game-assets/references/art-direction.md
.agents/skills/create-game-assets/references/provenance.md
.agents/skills/create-game-assets/references/raster-pipeline.md
.agents/skills/create-game-assets/references/three-d-pipeline.md
.agents/skills/create-game-assets/scripts/asset_report.py
.agents/skills/create-game-assets/scripts/build_preview_sheet.py
.agents/skills/create-game-assets/scripts/requirements.txt
.agents/skills/game-ui-ux/SKILL.md
.agents/skills/game-ui-ux/references/layout-and-flow.md
.agents/skills/ui-ugui/SKILL.md
.agents/skills/ui-ugui/references/scrollview-setup.md
.codex/agents/grimhold_architect.toml
.codex/agents/grimhold_explorer.toml
.codex/agents/grimhold_implementer.toml
.codex/agents/grimhold_reviewer.toml
.codex/agents/grimhold_validator.toml
.codex/config.toml
AGENTS.md
Project Grimhold/Assets/Scripts/Gameplay/BreakableObject.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
- message-term → Combat: weapon
- path → Abilities: .agents/skills/create-game-assets/SKILL.md
- path → Abilities: .agents/skills/create-game-assets/agents/openai.yaml
- path → Abilities: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Abilities: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Abilities: .agents/skills/create-game-assets/references/art-direction.md
- path → Abilities: .agents/skills/create-game-assets/references/provenance.md
- path → Abilities: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Abilities: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Abilities: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Abilities: .agents/skills/game-ui-ux/SKILL.md
- path → Abilities: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Abilities: .agents/skills/ui-ugui/SKILL.md
- path → Abilities: .agents/skills/ui-ugui/references/scrollview-setup.md
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → UI: .agents/skills/game-ui-ux/SKILL.md
- path → UI: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → UI: .agents/skills/ui-ugui/SKILL.md
- path → UI: .agents/skills/ui-ugui/references/scrollview-setup.md
- message-term → World &amp; Visibility: dungeon
- path → Art &amp; Animation: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Art &amp; Animation: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/SKILL.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/agents/openai.yaml
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/provenance.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/SKILL.md
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/SKILL.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/references/scrollview-setup.md
- path → Build &amp; Tooling: .codex/agents/grimhold\_architect.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_explorer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_implementer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_reviewer.toml
- path → Build &amp; Tooling: .codex/agents/grimhold\_validator.toml
- path → Build &amp; Tooling: AGENTS.md
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/BreakableObjectPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### fed049db01ebc549243d41d78ed46c008a0c5368

[fed049db01ebc549243d41d78ed46c008a0c5368](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/fed049db01ebc549243d41d78ed46c008a0c5368)

Date: 2026-11-06T00:03:33-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): roll dungeon weapon variants in table-rolled chests
```

#### Original body

```text
Each weapon unit of a chest resolved on first interaction rolls one deterministic variant from the chest seed, using provisional Balance values. Explicit content stays plain, and a catalog without the gem pool generates plain units. Adds a worst-case size guard for the extraction RPC.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs.meta
- message-term → Combat: weapon
- path → Extraction: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationDefaults.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → World &amp; Visibility: dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponInstanceGenerationDefaultsTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/LootContainerPreSpawnPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 32db5ec13ae61dd1eceec7201981585818922959

[32db5ec13ae61dd1eceec7201981585818922959](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/32db5ec13ae61dd1eceec7201981585818922959)

Date: 2026-11-05T23:50:01-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): carry weapon modifiers through extraction
```

#### Original body

```text
The extraction RPC adds per-unit modifiers, the off-hand slots and the four hand words. A shared builder derives the stash items and prepared equipment for both the authority publish and the local commit. Equipment publishes as ID-free amount-1 units with modifiers; commit bodies stay quantity-only. Docs describe the as-built contract.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
Project Grimhold/Assets/Scripts/Player/Extraction/ExtractionPayloadBuilder.cs
Project Grimhold/Assets/Scripts/Player/Extraction/ExtractionPayloadBuilder.cs.meta
Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs.meta
Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: weapon
- path → Extraction: Project Grimhold/Assets/Scripts/Player/Extraction/ExtractionPayloadBuilder.cs
- path → Extraction: Project Grimhold/Assets/Scripts/Player/Extraction/ExtractionPayloadBuilder.cs.meta
- path → Extraction: Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs
- path → Extraction: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs.meta
- path → Extraction: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/ExtractionPayloadModifiersTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 617eb51ddde8f0f816636c379e853a4eed65b616

[617eb51ddde8f0f816636c379e853a4eed65b616](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/617eb51ddde8f0f816636c379e853a4eed65b616)

Date: 2026-11-05T23:36:46-03:00 · Classification: Tests (explicit) · Systems: Combat, Inventory & Loot, Build & Tooling, Tests

Flags: secondary

#### Original subject

```text
test(loot): set network word ceilings to the measured sizes
```

#### Original body

```text
Container 1230, enemy 1291 and pickup 24 words with weapon instance modifiers, per the owner's decision to re-measure and fix new ceilings.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
```

#### Classification evidence

- type → Tests: test
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 84f8f95f3e210c962f5a5a72a26b23047d16b6aa

[84f8f95f3e210c962f5a5a72a26b23047d16b6aa](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/84f8f95f3e210c962f5a5a72a26b23047d16b6aa)

Date: 2026-11-05T23:35:08-03:00 · Classification: Tests (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, Documentation, Tests

Flags: secondary

#### Original subject

```text
test(loot): pin weapon modifier restore across state copy
```

#### Original body

```text
Container weapon units, pickup modifiers and equipment hand modifiers survive CopyStateFrom. HostMigrationRecoveryArchitecture documents the restore contract; real multi-process Host Migration stays a manual check.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
Project Grimhold/Docs/Architecture/HostMigrationRecoveryArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Tests: test
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → Networking &amp; Sessions: Host Migration
- path → Documentation: Project Grimhold/Docs/Architecture/HostMigrationRecoveryArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 59e9b4ef98b7c3156d11f556df4f6c87bde0bed2

[59e9b4ef98b7c3156d11f556df4f6c87bde0bed2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/59e9b4ef98b7c3156d11f556df4f6c87bde0bed2)

Date: 2026-11-05T23:32:02-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): keep weapon modifiers in player corpses
```

#### Original body

```text
The expedition snapshot now captures every weapon unit that carries instance modifiers (Inventory slots and the four hands). Corpse container load, verification and rollback keep those units with their modifiers and create the rest of each provenance bucket plain.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/ExpeditionWeaponUnit.cs
Project Grimhold/Assets/Scripts/Player/ExpeditionWeaponUnit.cs.meta
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Scripts/Player/PlayerExpeditionLootSnapshot.cs
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/ExpeditionWeaponUnit.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/ExpeditionWeaponUnit.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/PlayerExpeditionLootSnapshot.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 689a2ce3fdab35eb30e7bc2f8365aa51b08d9b9d

[689a2ce3fdab35eb30e7bc2f8365aa51b08d9b9d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/689a2ce3fdab35eb30e7bc2f8365aa51b08d9b9d)

Date: 2026-11-05T22:02:19-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): keep weapon modifiers in world pickups
```

#### Original body

```text
Make a dropped or spawned weapon pickup carry its unit's modifiers
(TASK-283, ItemInstanceArchitecture.md section 6.3).
NetworkLootPickup replicates the packed Modifiers of the single weapon
unit it holds. A spawn override is rejected unless the modifiers belong
to one weapon unit (amount 1, weapon category) and decode against the
catalog. TryResolveRaidOriginTransfer returns them with the origin
transfer, so Interact hands the exact unit, modifiers included, to the
receiver. Dropping from the Inventory already resolves the unit's
modifiers, so a dropped gemmed weapon is picked up with its gems.

Tests: PlayerLootReceiverWeaponModifiersPlayModeTests adds pickup carry,
stackable rejection and drop-keeps-modifiers (16/16). PlayMode
loot/equipment/presenter/stash fixtures 203/224 with exactly the 21
pre-existing failures. Full EditMode 5776/5802: the 25 known failing
classes plus PlayerMovementRulesTests execution order (expects -9, gets
-7); PlayerCombatNetworkController is unchanged by this commit.

Refs: TASK-283
```

#### Changed paths

```text
.claude/agents/architect.md
.claude/agents/explorer.md
.claude/agents/implementer.md
.claude/agents/reviewer.md
.claude/agents/validator.md
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
odd/tasks/equipment-item-instances-handoff.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances-handoff.md
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: .claude/agents/architect.md
- path → Build &amp; Tooling: .claude/agents/explorer.md
- path → Build &amp; Tooling: .claude/agents/implementer.md
- path → Build &amp; Tooling: .claude/agents/reviewer.md
- path → Build &amp; Tooling: .claude/agents/validator.md
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### ec5dbdc43ee19674b096d9ca90204c849bb33ce9

[ec5dbdc43ee19674b096d9ca90204c849bb33ce9](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ec5dbdc43ee19674b096d9ca90204c849bb33ce9)

Date: 2026-11-05T15:35:34-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, UI, World & Visibility, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(ui): show every Raid container weapon in its own cell
```

#### Original body

```text
Give each weapon unit in a chest or enemy drop its own cell and tooltip,
and withdraw exactly the selected unit (TASK-283,
ItemInstanceArchitecture.md section 6.3).

- RaidLootPanelPresenter.RefreshContainer keeps stacks in aggregate
  cells and gives every weapon unit a cell addressed by its unit
  position and observed modifiers (RaidInventorySlotData
  SourceUnitIndex and SourceUnitModifiers).
- A modified unit's tooltip shows its scaling, installed gems, gem bonus
  and effective damage, using the local participant's confirmed
  attributes from RaidAvatarParticipantLink. Plain units keep the
  template tooltip. Raid Inventory cells also show their modifiers.
- Selecting or dragging a unit cell sends TryRequestUnitTransfer. Take
  All captures one step per stack and one per weapon unit, so every
  unit moves alone with its modifiers.
- DragPayload carries the unit address, which is valid only for
  container sources.

Known and unchanged: the Raid container panel authors 6 cells while
corpse containers have capacity 30, so corpse panels still report the
existing six-versus-thirty shortage.

Tests: RaidInventoryPresenterInteractionTests gains per-unit cells and
gem tooltip, selecting one unit and Take All with modifiers (59/59).
Full EditMode 5777/5802 with the 25 known pre-existing failures.
PlayMode loot/equipment/presenter/stash fixtures 200/221 with exactly
the 21 pre-existing failures.

Refs: TASK-283
Player-Added: Weapons inside Dungeon chests and enemy drops now each have their own slot, showing their gems and bonus damage, and you take exactly the one you pick.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Presentation/DragPayload.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootTakeAllState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: Inventory
- scope → UI: ui
- message-term → UI: ui
- message-term → World &amp; Visibility: Dungeon
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### b84b02df49f09e567c3f3cd3a945482f956d6267

[b84b02df49f09e567c3f3cd3a945482f956d6267](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b84b02df49f09e567c3f3cd3a945482f956d6267)

Date: 2026-11-05T15:18:00-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, UI, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): address individual weapon units in container withdrawals
```

#### Original body

```text
Let a player withdraw exactly the weapon unit they selected from a
chest, enemy drop or corpse (TASK-283, ItemInstanceArchitecture.md
section 6.3). This is the network part of per-unit container cells; the
UI follows.

Inventory slot addresses stay bounded to the 30 receiver slots, so
container units (up to 34) get their own address.
LootTransferRequest and LootTransferRequestIdentity gain SourceUnitIndex
and SourceUnitModifiers (ForSourceUnit factories). RPC_RequestTransfer
carries both, and PlayerLootTransferNetworkController exposes
TryRequestUnitTransfer.

- The guard is the unit's observed modifiers, not the container
  revision: unrelated changes do not stale the request, and a taken or
  replaced unit is rejected with ContainerUnavailable instead of
  substituting another unit.
- A unit address is only valid for withdrawals; on a deposit it is
  rejected with MissingAuthority. The receiver never extracts by a unit
  address.
- The container resolves and removes exactly the addressed unit.

Tests: PlayerLootReceiverWeaponModifiersPlayModeTests adds addressed
withdrawal, stale modifiers and deposit misuse (13/13). Full EditMode
5777/5802 with the 25 known pre-existing failures. PlayMode
loot/equipment/presenter/transfer fixtures 151/172 with exactly the 21
pre-existing failures.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Core/LootTransferRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferClientRequestState.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/LootTransferRequestIdentity.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootTransferNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → UI: UI
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 9999122921fa9ea1177f6fc0b553a8fd8991f047

[9999122921fa9ea1177f6fc0b553a8fd8991f047](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9999122921fa9ea1177f6fc0b553a8fd8991f047)

Date: 2026-11-05T15:03:17-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): store weapon units with modifiers in Raid containers
```

#### Original body

```text
Keep every weapon's modifiers when it enters a chest, enemy drop or
corpse (TASK-283, ItemInstanceArchitecture.md section 6.3).
NetworkLootContainer now replicates up to 34 ContainerWeaponUnit
records (30 Inventory slots plus 4 hands), each holding the template,
the Raid origin and the packed modifiers in two words. The records are
written together with the aggregate quantities and the origin state.

- Depositing one weapon stores its exact modifiers. Withdrawing one
  weapon takes the lowest-address unit of the lowest origin and returns
  it with its modifiers.
- Several weapon units only move together as plain units, so a request
  for more units than there are plain ones is rejected with
  InsufficientAmount and nothing changes. Modifiers on non-weapons or on
  multi-unit transfers are rejected with InvalidLoot.
- World and enemy containers count each weapon unit as one of their
  entries (OccupiedSlotCount). Full-inventory containers keep counting
  types, bounded by the 34 units.
- Natural content and exact corpse loads create plain units. Corpse
  modifiers, per-unit container cells and pickups follow.

Tests: PlayerLootReceiverWeaponModifiersPlayModeTests gains deposit,
withdrawal, multi-unit rejection and entry-capacity cases (10/10). Full
EditMode 5777/5802 with the 25 known pre-existing failures. PlayMode
loot/equipment fixtures 92/113 with exactly the 21 pre-existing
failures.

Docs: ItemInstanceArchitecture.md records the per-unit container cells
decision.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/ContainerWeaponUnit.cs
Project Grimhold/Assets/Scripts/Gameplay/ContainerWeaponUnit.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Gameplay/ContainerWeaponUnit.cs
- path → Combat: Project Grimhold/Assets/Scripts/Gameplay/ContainerWeaponUnit.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### d5e73de3acc9478dec920a80bece5a739e046a6e

[d5e73de3acc9478dec920a80bece5a739e046a6e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d5e73de3acc9478dec920a80bece5a739e046a6e)

Date: 2026-11-05T11:26:22-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(combat): apply equipped weapon modifiers to Raid damage
```

#### Original body

```text
Make the Raid weapon's scaling and installed gems drive combat damage
(TASK-283, ItemInstanceArchitecture.md sections 6.2 and 6.3). Until now
the Raid always projected a plain weapon from the template.

- PlayerWeaponEquipmentNetworkController replicates one packed modifier
  word per hand slot. SetCatalogIndexPlusOne clears it, and the
  operation that equips a unit writes that unit's modifiers.
- Damage is floor((Base + GemDamageBonus) * (1 + sum)), computed from the
  hand unit's modifiers through WeaponDamageCalculator. Empty modifiers
  keep the template's plain projection, so plain weapons deal the same
  damage as before.
- The prepared strategy cache and the applied replicated state include
  the modifiers. Swapping two units of the same template with
  different gems rebuilds the attack.
- Inventory and container equips preview the incoming unit's modifiers
  for the strategy check. Displaced and unequipped weapons return to
  the inventory as the exact unit with their modifiers.
- Raid admission equips the unit each prepared slot references.
  Another unit of the same template with different modifiers stays in
  the inventory (PlayerLootReceiver.TryFindInventoryUnit).

Tests: PlayerEquipmentPlayModeTests gains addressed equip of a
Ruby-socketed sword (+5 damage, unequip returns its modifiers) and
prepared equipment of the referenced unit; the fixture catalogs include
the Ruby gem. PlayerEquipment 38/40, with the 2 failures being the known
damage oracles. Full EditMode 5777/5802 with the 25 known pre-existing
failures. PlayMode loot/equipment fixtures 84/105 with exactly the 21
pre-existing failures.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- scope → Combat: combat
- message-term → Combat: combat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: Inventory
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerEquipmentPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 4e55f297c6f7c1e1660402f70f84cf912afd0774

[4e55f297c6f7c1e1660402f70f84cf912afd0774](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4e55f297c6f7c1e1660402f70f84cf912afd0774)

Date: 2026-11-05T11:10:40-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): replicate weapon modifiers in the Raid inventory
```

#### Original body

```text
Give each physical Raid inventory slot its weapon modifiers (TASK-283,
ItemInstanceArchitecture.md section 6.3). PlayerLootReceiver now
replicates a packed modifier word per slot next to LootInventorySlot.
Every slot write goes through the joint LootInventoryRules overloads,
so quantity, origin and modifiers never diverge.

- Raid admission writes each admitted weapon unit's modifiers. Stacks
  and holes stay empty.
- RaidLootOriginTransfer carries the modifiers of the transferred unit,
  resolved by the source's State Authority. One weapon unit carries its
  own modifiers, and the receiver writes them on reception. Several
  weapon units (for example two swords taken together) move only as
  plain units, one by one, so existing multi-unit transfers keep
  working. Modifiers on stackable loot are rejected.
- Extraction removes only the unit whose modifiers match the resolved
  transfer, so a gemmed weapon is never consumed in place of another.
- TryGetInventorySlotModifiers exposes a slot's modifiers for later
  equipment, world-loot and extraction wiring.
- The new networked field is restored by CopyStateFrom like the slots.

Equipment hands, combat damage, pickups, containers, corpses and the
extraction payload are not wired yet.

Tests: PlayerLootReceiverWeaponModifiersPlayModeTests (admission,
addressed and unaddressed resolution, transfer between receivers,
rejection without mutation, CopyStateFrom). PlayerLootReceiverSlots
25/25 unchanged. Full EditMode 5777/5802 with the 25 known pre-existing
failures. PlayMode loot/equipment fixtures 82/103 with exactly the 21
pre-existing failures.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Scripts/Gameplay/RaidLootOriginTransfer.cs
Project Grimhold/Assets/Tests/PlayMode/Loot.meta
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs.meta
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Combat: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs.meta
- message-term → Combat: weapon
- message-term → Extraction: Extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/RaidLootOriginTransfer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs.meta
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Loot/PlayerLootReceiverWeaponModifiersPlayModeTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 702d34ac53ab4cd9d51472bcde8453d1bd84efc0

[702d34ac53ab4cd9d51472bcde8453d1bd84efc0](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/702d34ac53ab4cd9d51472bcde8453d1bd84efc0)

Date: 2026-11-05T10:53:22-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(networking): admit Raid participants with ID-free weapon modifiers
```

#### Original body

```text
Carry equipment modifiers into Raid admission without backend instance
IDs (TASK-283, ItemInstanceArchitecture.md section 6.3). This is the
admission part of the Raid transport; runtime replication follows.

- Backend SessionTicketService signs v3 admission snapshots as per-unit
  { lootId, amount, modifiers? } items and prepared equipment as
  null | { lootId, modifiers? }. Instance IDs and Town slots are
  stripped before signing. pendingReservation keeps the full instances.
- Unity decodes and verifies the ID-free signed snapshot
  (RaidAdmissionSnapshotMapper, RaidPreparedEquipmentJsonConverter,
  admission data codecs) and rejects malformed modifiers or any durable
  identity in the snapshot.
- RaidAdmissionPayloadChunking raises the reassembly bound from 8 KiB to
  16 KiB, keeping 384-byte chunks and every malformed, duplicate and
  oversized rejection. A maximum legal loadout (30 inventory and 4 hand
  weapons with dual scaling and two gems) measures 10,423 bytes.

Tests: RaidAdmissionInstanceTests (new: projection, max-capacity
decode, malformed/identity rejection), 16 KiB boundary and
max-capacity cases in RaidAdmissionPayloadChunkingTests, and backend
admission cases in inventory.instances. npm test 517/517, integration
171/171, concurrency 19/19. Full EditMode 5777/5802; the 25 failures
are the known pre-existing set.

Docs: SessionLifecycleAndSpawning.md documents the admission bound.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs
Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs.meta
Project Grimhold/Assets/Scripts/Backend/SessionTicketDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
Project Grimhold/Assets/Scripts/Networking/AuthoritativeRaidAdmissionTicket.cs
Project Grimhold/Assets/Scripts/Networking/AuthoritativeRaidAdmissionTicketCodec.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionDataCodec.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionPayloadChunking.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionSnapshotMapper.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionSnapshotMapper.cs.meta
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionVerifier.cs
Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionPayloadChunkingTests.cs
Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
backend/src/services/SessionTicketService.js
backend/tests/inventory.instances.test.js
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootEntry.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Inventory &amp; Loot: backend/tests/inventory.instances.test.js
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Backend/SessionTicketDataTransferObjects.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/AuthoritativeRaidAdmissionTicket.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/AuthoritativeRaidAdmissionTicketCodec.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/NetworkSpawnManager.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionDataCodec.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionPayloadChunking.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionSnapshotMapper.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionSnapshotMapper.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionVerifier.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionPayloadChunkingTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- path → Networking &amp; Sessions: backend/src/services/SessionTicketService.js
- scope → Networking &amp; Sessions: networking
- message-term → Networking &amp; Sessions: networking
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/RaidPreparedEquipmentJsonConverter.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/SessionTicketDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Persistence &amp; Backend: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/tests/inventory.instances.test.js
- message-term → Persistence &amp; Backend: Backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionPayloadChunkingTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionInstanceTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionPayloadChunkingTests.cs
- path → Tests: backend/tests/inventory.instances.test.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### e4018eeb5531f34dc0d124bdbcac20eacc69cbc1

[e4018eeb5531f34dc0d124bdbcac20eacc69cbc1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e4018eeb5531f34dc0d124bdbcac20eacc69cbc1)

Date: 2026-11-05T10:16:50-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): pack weapon modifiers into one network word
```

#### Original body

```text
Lay the foundation for Raid transport of weapon instance modifiers
(TASK-283, ItemInstanceArchitecture.md section 6.3).
WeaponInstanceModifiersNetworkData now packs a weapon unit's modifiers
into one 32-bit Fusion word instead of seven: primary and secondary
attribute and grade, socket count, and two gem catalog indices + 1
(28 bits, 4 reserved). Decoding validates reserved bits, catalog
bounds (< 128 entries), scaling, sockets and that installed gems are
catalog gems.

LootInventoryRules gains joint slot/modifier overloads for receive,
extract and split. Weapons are received individually, depleted slots
clear their modifiers, and non-weapon slots reject non-empty modifiers.
Nothing replicates or consumes them yet; component wiring follows.

Contract: section 6.3 records the packed layout, the container
capacity rule (each weapon unit is one of the 6 container entries;
player corpses hold every carried weapon) and that network ceilings are
re-measured after integration.

Tests: packed width, round trip, boundaries and malformed words in
WeaponGemSocketTests; modifier lockstep, weapon non-stacking and
rejection atomicity in LootInventoryRulesTests; a one-word sizing probe
in RaidLootOriginWordCountTests. Focused 74/74. Full EditMode
5770/5795; the 25 failures are the known pre-existing set.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### f644df3a7dd1daf28301ca5ddc77ad52618fdc6b

[f644df3a7dd1daf28301ca5ddc77ad52618fdc6b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f644df3a7dd1daf28301ca5ddc77ad52618fdc6b)

Date: 2026-11-05T02:32:25-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(inventory): add Town gem incrustation to Inventory and Stash
```

#### Original body

```text
Let players incrust a gem into an unequipped weapon in the Town
Inventory or Stash (TASK-283, ItemInstanceArchitecture.md section 5).
Incrustation is permanent, so it always asks for confirmation and
waits for the backend before anything changes.

- Dropping a gem stack on a weapon with a free socket opens the
  confirmation "¿Incrustar {gema} en {arma}? Es permanente." A weapon
  without free sockets shows "Esta arma no tiene ranuras libres", and
  the gem returns to its slot. Dropping a gem on other slots still
  moves or swaps it.
- The "Incrustar" context action on gems highlights eligible weapons
  across Inventory and Stash pages. Picking one opens the same
  confirmation, and cancel exits the mode.
- Confirming sends the request remote-first through the shared
  StashTransferCoordinator queue, locks both screens while it is
  pending, and applies the returned authoritative lists and revision
  through the existing hydration. The tooltip then shows the gem and the
  new effective damage. Rejections show Spanish feedback and change
  nothing locally. Raid and equipped weapons are excluded.
- New GemInstallConfirmation.prefab (message, Confirm, Cancel) with
  GemInstallConfirmationView, nested in RaidInventoryUI and
  StashInventory. Systems.prefab and the recovery-weapon modal are
  unchanged.
- StashTransferCoordinator move, transfer and order now accept
  inventory schema 3 as well as 2, so migrated characters can keep
  moving items in Town.

Tests: TownGemInstallSessionTests (eligibility, exact request,
confirmation, cancel, feedback mapping), StashTransferCoordinatorTests
(v3 moves and orders, v2 preservation, install queueing),
LocalLoadoutInventoryReadSourceTests, LobbyStashPresenterPlayModeTests
with the real prefabs. Focused EditMode 100/100, PlayMode 102/102.
Full EditMode 5752/5777; the 25 failures are the known pre-existing
set.

Docs: RaidInventoryUIArchitecture.md documents the incrustation flow,
its limits and error mapping.

Refs: TASK-283
Player-Added: In Town, drag a gem onto a weapon in your Inventory or Stash, or use "Incrustar" on the gem, to permanently add its damage bonus to the weapon.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/UI/GemInstallConfirmation.prefab
Project Grimhold/Assets/Prefabs/UI/GemInstallConfirmation.prefab.meta
Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
Project Grimhold/Assets/Scripts/Player/Presentation/GemInstallConfirmationView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/GemInstallConfirmationView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallSession.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallSession.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallUIBinding.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallUIBinding.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownLootGemInstallContextActionProvider.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownLootGemInstallContextActionProvider.cs.meta
Project Grimhold/Assets/Scripts/Stash/IRemoteGemInstallService.cs
Project Grimhold/Assets/Scripts/Stash/IRemoteGemInstallService.cs.meta
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/StashTransferCoordinator.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelView.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownLootGemInstallContextActionProvider.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownLootGemInstallContextActionProvider.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallSession.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Player/Presentation/TownGemInstallSession.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → UI: Project Grimhold/Assets/Prefabs/UI/GemInstallConfirmation.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/GemInstallConfirmation.prefab.meta
- path → UI: Project Grimhold/Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/StashInventory.prefab
- path → UI: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- message-term → UI: tooltip
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownGemInstallSessionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### be00d8adb647d66b24830c15b16c36e05a5e5528

[be00d8adb647d66b24830c15b16c36e05a5e5528](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/be00d8adb647d66b24830c15b16c36e05a5e5528)

Date: 2026-11-05T00:19:42-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(equipment): compute Town weapon damage and tooltips from instances
```

#### Original body

```text
Make WeaponInstanceModifiers the damage source for player weapons
(ItemInstanceArchitecture.md section 6.2, TASK-283). Town statistics and
the equipment controller now resolve instance scaling and installed-gem
bonuses through WeaponDamageCalculator:
floor((Base + GemDamageBonus) * (1 + sum)). The legacy
WeaponOffensiveScaling path is no longer used at runtime.

- Town inventory, Stash and prepared-equipment presentation carry each
  item's EquipmentInstance and the character's confirmed attributes.
  LobbyStashPresenter, RaidInventoryPresenter and RaidLootPanelPresenter
  keep instance-to-entry alignment, including duplicate templates.
- Equipment tooltips in Town use the instance overload: scaling grades,
  installed gem names, gem bonus and effective damage equal to the
  statistics value. Missing attributes do not default to zero.
- Merchant offers and remote party members in Raid preparation
  inspection keep definition-only tooltips. No instance or attributes
  exist for them, and local damage is never shown as theirs.
- In the Raid, weapons use a plain instance derived from the template
  until modifiers are replicated (T7), so Raid damage is unchanged. No
  networked fields were added.

Tests: TownCharacterStatisticsBuilderTests (instance damage, gem
counts, tooltip/statistics equality, plain preservation),
EquipmentTooltipPresentationBuilderTests (real Stash prefab projection,
merchant preservation), LocalLoadoutInventoryReadSourceTests, and
production catalog plain-damage parity in WeaponDamageCalculatorTests.
Focused 100/100. Full EditMode 5729/5754; the 25 failures are the known
pre-existing set.

Docs: PlayerCombatArchitecture.md documents the F3 runtime and the
deferred Raid transport.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponScalingContributionsResolver.cs
Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentation.cs
Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownCharacterStatisticsBuilder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownRaidPreparationInspectionView.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownCharacterStatisticsBuilderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponScalingContributionsResolver.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Combat/PlayerWeaponEquipmentNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponScalingContributionsResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentation.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventoryPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidInventorySlotData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/RaidLootPanelPresenter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- scope → Inventory &amp; Loot: equipment
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentation.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → UI: tooltips
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownCharacterStatisticsBuilderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/TownCharacterStatisticsBuilderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/LocalLoadoutInventoryReadSourceTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 90ec4367205eb3e383eca6c9e07ff83383f535a5

[90ec4367205eb3e383eca6c9e07ff83383f535a5](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/90ec4367205eb3e383eca6c9e07ff83383f535a5)

Date: 2026-11-04T23:52:40-03:00 · Classification: Features (explicit) · Systems: Combat, Abilities, Inventory & Loot, UI, Persistence & Backend, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(stash): persist equipment item instances in Unity
```

#### Original body

```text
Implement the Unity persistence and backend-client part of
ItemInstanceArchitecture.md (TASK-283). Town gameplay, damage and UI
wiring follow separately.

- EquipmentInstance carries the backend instance ID and optional
  WeaponInstanceModifiers. StashItem and PreparedEquipmentLoadout carry
  it, equipment never merges, and same-template instance moves swap.
- Save schema v11 round-trips instances. Schemas 1-10 stay readable:
  equipment stacks expand into quantity-1, ID-less plain instances,
  and the snapshot records RequiresEquipmentHydration. Equip, unequip,
  Stash equip, remote prepared-equipment updates and gem install are
  rejected until a backend hydration provides instance IDs (contract
  section 9; the cache-only bootstrap path, e.g. Editor domain reload,
  can publish a Store before hydration). Future cache versions still
  decode as UnsupportedVersion.
- Backend DTOs parse v3 inventories and prepared equipment
  { lootId, instance }, with strict lowercase attribute names and E-S
  grades. Equip requests carry instanceId, and InventoryClient gains
  POST /character/me/inventory/gems/install (InstallGemRequest).
- The client always sends inventory protocol header 3 and selects v2 or
  v3 behavior from the character's inventorySchemaVersion.
- Absent prepared-ability IDs still encode as empty strings.

Tests: EquipmentInstancePersistenceTests and
EquipmentInstanceBackendTests (new). InventoryProtocolSlotTests,
PhysicalProfileSlotTests and StashSplitTests now expect header 3, and
PreparedEquipmentTests expects the ID-less plain instance for legacy
equipment. Full EditMode 5719/5744; the 25 failures are the known
pre-existing set.

Docs: LocalPlayerPersistenceArchitecture.md documents the cache
hydration guard and protocol header 3.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs.meta
Project Grimhold/Assets/Scripts/Backend/InstallGemRequest.cs
Project Grimhold/Assets/Scripts/Backend/InstallGemRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs
Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs.meta
Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs
Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs
Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs.meta
Project Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/PreparedEquipmentLoadout.cs
Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/StashItem.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs
- path → Combat: Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs.meta
- message-term → Combat: damage
- message-term → Abilities: ability
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/EquipmentInstance.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/PreparedEquipmentLoadout.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs.meta
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: equipment
- message-term → UI: UI
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/EquipmentInstanceData.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InstallGemRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InstallGemRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/PreparedEquipmentJsonConverter.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/WeaponScalingModifierJsonConverter.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/EquipmentInstanceBackendTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/EquipmentInstancePersistenceTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 6d65daf5f2c98c9f12e83fa3913f3707e172774e

[6d65daf5f2c98c9f12e83fa3913f3707e172774e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6d65daf5f2c98c9f12e83fa3913f3707e172774e)

Date: 2026-11-04T15:58:40-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Persistence & Backend, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): treat the inventory protocol header as a minimum version
```

#### Original body

```text
With only the v2 inventory gate enabled, accept protocol header 3 as
well as 2. Instance-capable Unity clients always send 3 and select v2
or v3 behavior from the character's inventorySchemaVersion, so they do
not need to discover the protocol before hydration. With
INVENTORY_INSTANCE_PROTOCOL_GATE enabled, only 3 is accepted, as
before. Unknown headers (for example 4) are still rejected with 426
CLIENT_UPGRADE_REQUIRED.

Tests: inventory.protocol covers header 3 under the v2 gate on every
gated route. inventory.protocol and integration/inventory.split now use
header 4 as the rejected value. npm test 515/515, integration 171/171,
concurrency 19/19.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
backend/src/middleware/inventoryProtocol.js
backend/tests/integration/inventory.split.test.js
backend/tests/inventory.protocol.test.js
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: backend/src/middleware/inventoryProtocol.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.split.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.protocol.test.js
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: backend/src/middleware/inventoryProtocol.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.split.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.protocol.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: backend/tests/integration/inventory.split.test.js
- path → Tests: backend/tests/inventory.protocol.test.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### f2fd344a3df4275afed8c92381843fbd0a9f6ca1

[f2fd344a3df4275afed8c92381843fbd0a9f6ca1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f2fd344a3df4275afed8c92381843fbd0a9f6ca1)

Date: 2026-11-04T15:54:12-03:00 · Classification: Features (explicit) · Systems: Combat, Extraction, Inventory & Loot, Networking & Sessions, Persistence & Backend, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): add equipment item instances and gem incrustation
```

#### Original body

```text
Implement the backend part of ItemInstanceArchitecture.md (TASK-283).
Behind the new INVENTORY_INSTANCE_PROTOCOL_GATE (default false),
inventory schema v3 stores every equipment item as a backend-issued
instance of quantity 1, and weapons carry modifiers (scaling and 0-2
gem sockets). Schema v2 characters keep their current behavior.

- ItemInstances: the only instance ID issuer (24-hex ObjectId), plain
  instances and modifier validation mirroring Unity
  WeaponInstanceModifiers.TryValidate (lowercase attribute names,
  grades E-S).
- scripts/migrate_item_instances.js: explicit v2 to v3 migration with
  dry-run. Equipment stacks become N plain instances (first keeps its
  slot, the rest take free slots, then overflow addresses), prepared
  equipment and reservations keep instances, and stored extraction
  results become ID-free amount-1 entries. Idempotent and resumable;
  run it in a maintenance window with backups.
- Operations on v3: moves and equip carry the instance, equipment
  never merges or splits (409 INSTANCE_NOT_SPLITTABLE), purchases issue
  distinct plain instances, every equipment instance is sellable (even
  for 0) at template plus installed gem value, and equipment buy-back
  is disabled, including pre-migration ledger entries
  (409 BUYBACK_NOT_ELIGIBLE). The arming_sword fallback is a plain
  instance.
- POST /character/me/inventory/gems/install installs one gem into the
  lowest empty socket of an unequipped weapon in stash or loadout in a
  single revision-guarded update with transactionId replay. Rejections
  (INSTANCE_MISMATCH, GEM_NOT_AVAILABLE, GEM_NO_SOCKETS,
  GEM_NO_EMPTY_SOCKET, REVISION_CONFLICT) mutate nothing.
- Extraction results accept { lootId, amount, modifiers? }, validate
  modifiers by rules, reject host-sent instance IDs, and issue fresh
  IDs on commit.
- Shop catalog prices and eligibility now mirror the Unity
  LootDefinitions (owner decision). This fixes the shop parity test and
  changes healthpotion from 20/10 to 40/20. It also adds 41 previously
  absent offers, including the three gems at 140/70.

Tests: inventory.instances, item.instances.parity, migrate.item.instances,
integration/item.instances and concurrency/item.instances.real, plus
v2 preservation cases. npm test 495/495, integration 171/171,
concurrency 19/19. The integration 'Duplicate Receipts' assertion is an
intermittent race in that test.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
backend/README.md
backend/scripts/migrate_item_instances.js
backend/scripts/seed-test-accounts.js
backend/src/config/env.js
backend/src/config/shopCatalog.js
backend/src/middleware/inventoryProtocol.js
backend/src/models/AuthoritativeExtractionResult.js
backend/src/models/Character.js
backend/src/routes/inventory.routes.js
backend/src/services/CharacterService.js
backend/src/services/ExtractionCommitService.js
backend/src/services/InventoryEntries.js
backend/src/services/InventoryService.js
backend/src/services/ItemInstances.js
backend/src/services/RaidResultService.js
backend/src/services/SessionTicketService.js
backend/src/validators/inventory.validators.js
backend/tests/concurrency/inventory.real.test.js
backend/tests/concurrency/item.instances.real.test.js
backend/tests/integration/concurrency.test.js
backend/tests/integration/item.instances.test.js
backend/tests/integration/persistence.test.js
backend/tests/integration/shop.authoritative.test.js
backend/tests/inventory.buyback.test.js
backend/tests/inventory.instances.test.js
backend/tests/item.instances.parity.test.js
backend/tests/migrate.item.instances.test.js
backend/tests/setup.js
backend/tests/shop.catalog.parity.test.js
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: weapons
- path → Extraction: backend/src/models/AuthoritativeExtractionResult.js
- path → Extraction: backend/src/services/ExtractionCommitService.js
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: backend/src/middleware/inventoryProtocol.js
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryEntries.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/src/validators/inventory.validators.js
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.real.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.buyback.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.instances.test.js
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: equipment
- path → Networking &amp; Sessions: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/README.md
- path → Persistence &amp; Backend: backend/scripts/migrate\_item\_instances.js
- path → Persistence &amp; Backend: backend/scripts/seed-test-accounts.js
- path → Persistence &amp; Backend: backend/src/config/env.js
- path → Persistence &amp; Backend: backend/src/config/shopCatalog.js
- path → Persistence &amp; Backend: backend/src/middleware/inventoryProtocol.js
- path → Persistence &amp; Backend: backend/src/models/AuthoritativeExtractionResult.js
- path → Persistence &amp; Backend: backend/src/models/Character.js
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/services/CharacterService.js
- path → Persistence &amp; Backend: backend/src/services/ExtractionCommitService.js
- path → Persistence &amp; Backend: backend/src/services/InventoryEntries.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/services/ItemInstances.js
- path → Persistence &amp; Backend: backend/src/services/RaidResultService.js
- path → Persistence &amp; Backend: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/src/validators/inventory.validators.js
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.real.test.js
- path → Persistence &amp; Backend: backend/tests/concurrency/item.instances.real.test.js
- path → Persistence &amp; Backend: backend/tests/integration/concurrency.test.js
- path → Persistence &amp; Backend: backend/tests/integration/item.instances.test.js
- path → Persistence &amp; Backend: backend/tests/integration/persistence.test.js
- path → Persistence &amp; Backend: backend/tests/integration/shop.authoritative.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.buyback.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.instances.test.js
- path → Persistence &amp; Backend: backend/tests/item.instances.parity.test.js
- path → Persistence &amp; Backend: backend/tests/migrate.item.instances.test.js
- path → Persistence &amp; Backend: backend/tests/setup.js
- path → Persistence &amp; Backend: backend/tests/shop.catalog.parity.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Tests: backend/scripts/seed-test-accounts.js
- path → Tests: backend/tests/concurrency/inventory.real.test.js
- path → Tests: backend/tests/concurrency/item.instances.real.test.js
- path → Tests: backend/tests/integration/concurrency.test.js
- path → Tests: backend/tests/integration/item.instances.test.js
- path → Tests: backend/tests/integration/persistence.test.js
- path → Tests: backend/tests/integration/shop.authoritative.test.js
- path → Tests: backend/tests/inventory.buyback.test.js
- path → Tests: backend/tests/inventory.instances.test.js
- path → Tests: backend/tests/item.instances.parity.test.js
- path → Tests: backend/tests/migrate.item.instances.test.js
- path → Tests: backend/tests/setup.js
- path → Tests: backend/tests/shop.catalog.parity.test.js
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### b1f30da609139032e9e99643521ed56765f4eeac

[b1f30da609139032e9e99643521ed56765f4eeac](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b1f30da609139032e9e99643521ed56765f4eeac)

Date: 2026-11-04T15:24:08-03:00 · Classification: Documentation (explicit) · Systems: Combat, Extraction, Inventory & Loot, Persistence & Backend, Documentation

Flags: secondary

#### Original subject

```text
docs(architecture): define equipment item instances for gem incrustation
```

#### Original body

```text
Add ItemInstanceArchitecture.md, the approved contract for TASK-283:
every equipment item (weapon, shield, armor piece) becomes a unique,
backend-issued instance of quantity 1, and only weapons carry modifiers
(scaling plus 0-2 gem sockets). The contract covers backend inventory
schema v3 behind INVENTORY_INSTANCE_PROTOCOL_GATE, the v2 to v3
migration that splits equipment stacks into plain instances, Town-only
gem incrustation through POST /character/me/inventory/gems/install, save
schema v11, instance-driven damage
floor((Base + GemDamageBonus) * (1 + sum)), Raid transport of modifiers
without instance IDs, and extraction rule validation with fresh IDs on
commit. Equipment buy-back is disabled until Game Design redefines it,
and on v3 every equipment instance is sellable, even for 0.

Align PlayerCombat, LocalPlayerPersistence, Commerce, Extraction and
LootInteraction architecture with the contract, replacing the
"instances are not stored until unique item identity exists" statements.

Record the ODD execution tracker in
odd/tasks/equipment-item-instances.md.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/CommerceArchitecture.md
Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
odd/tasks/equipment-item-instances.md
```

#### Classification evidence

- type → Documentation: docs
- path → Combat: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md
- message-term → Combat: weapon
- path → Extraction: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- message-term → Extraction: Extraction
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: odd/tasks/equipment-item-instances.md
- message-term → Inventory &amp; Loot: equipment
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- message-term → Persistence &amp; Backend: backend
- path → Documentation: Project Grimhold/Docs/Architecture/CommerceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ItemInstanceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerCombatArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### 1c1f79b2cc532b2e7613ff56915f5c08ac095872

[1c1f79b2cc532b2e7613ff56915f5c08ac095872](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1c1f79b2cc532b2e7613ff56915f5c08ac095872)

Date: 2026-11-04T14:57:14-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, World & Visibility, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add provisional gems to container loot tables
```

#### Original body

```text
Make the three provisional MVP gems obtainable in the Dungeon so gem
incrustation (TASK-283) can be exercised in play. RES_ATTUNEMENT_GEM was
not in any table before.

- DefaultLootContainerContentTable: RES_ATTUNEMENT_GEM, RES_RUBY_GEM and
  RES_SAPPHIRE_GEM at weight 3, amount 1 (the weight of other rare
  entries).
- EnemyLootContainerContentTable: the same three gems at weight 1,
  amount 1.

Weights are provisional until balance.

Tests: WeaponCatalogTests.LootTables_IncludeProvisionalGems; the table
entry counts become 45 (Default) and 43 (Enemy).

Refs: TASK-283
Player-Added: Ruby, Sapphire and Attunement gems can now be found in Dungeon chests and enemy drops.
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/EnemyLootContainerContentTable.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/EnemyLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → World &amp; Visibility: Dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### baabc056fbc9812afcd58428c25e215ceaefcefa

[baabc056fbc9812afcd58428c25e215ceaefcefa](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/baabc056fbc9812afcd58428c25e215ceaefcefa)

Date: 2026-11-06T01:35:11-03:00 · Classification: Bug Fixes (explicit) · Systems: UI

Flags: primary or unclassified

#### Original subject

```text
fix(player): repair corrupted NetworkPlayer prefab from merge conflict
```

#### Original body

```text
Restore valid YAML definitions for the US-75 RaidMissionTracker HUD elements (PopupDetail and HeaderText) and remove orphaned components from prior HUD consolidation that caused broken PPtr imports. This resolves the Fusion object provider failure when spawning player avatars during raid admission.

Player-Fixed: Raid launch from Town spawns player avatars reliably without crashing back to town.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
```

#### Classification evidence

- type → Bug Fixes: fix
- message-term → UI: HUD

#### Diagnostics

- No classification ambiguity detected.

### f2cd9d7c9d843082400e174838b64e2fc462065f

[f2cd9d7c9d843082400e174838b64e2fc462065f](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f2cd9d7c9d843082400e174838b64e2fc462065f)

Date: 2026-11-03T20:02:24-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, Networking & Sessions, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(missions): allow reaccepting abandoned contracts and enforce physical slot limits
```

#### Original body

```text
Reset abandoned mission states to active upon re-acceptance in LocalProfileStore. Enforce physical inventory stack limits in RaidLoadoutRules.TryValidate and calculate projected physical slot consumption in TryCreateLoadoutReservation to prevent raid admission crashes when loadouts exceed physical slot capacity.

Player-Fixed: Abandoned contracts can now be accepted again from the Guild NPC.

Player-Fixed: Prevented raid launch failures caused by inventories exceeding physical stack limits.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/MissionPersistenceTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 6e42d41593971ee726bb6b4a308bd76f0212b697

[6e42d41593971ee726bb6b4a308bd76f0212b697](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6e42d41593971ee726bb6b4a308bd76f0212b697)

Date: 2026-11-03T20:01:36-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, World & Visibility, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add crafting and mission resources to container loot tables
```

#### Original body

```text
Register all 21 canonical RES_* resource items into DefaultLootContainerContentTable and EnemyLootContainerContentTable so crafting and mission resources can spawn from dungeon chests and enemies. Update WeaponCatalogTests with the expanded catalog count.

Player-Added: Crafting and mission resources like black iron now drop from chests and enemies in raids.
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Tables/EnemyLootContainerContentTable.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Tables/EnemyLootContainerContentTable.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → World &amp; Visibility: dungeon
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/WeaponCatalogTests.cs

#### Diagnostics

- No classification ambiguity detected.

### f9895d8e5d2e8362f4de9e7b572e19e9e01309dd

[f9895d8e5d2e8362f4de9e7b572e19e9e01309dd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/f9895d8e5d2e8362f4de9e7b572e19e9e01309dd)

Date: 2026-11-03T20:01:02-03:00 · Classification: Bug Fixes (explicit) · Systems: UI, Art & Animation

Flags: primary or unclassified

#### Original subject

```text
fix(ui): restore character tables and glyphs for alagard and liberation sans
```

#### Original body

```text
Repopulate character and glyph lookup tables in alagard SDF and LiberationSans SDF fallback font assets to fix broken glyph rendering in menus and HUD labels.

Player-Fixed: Restored missing and unrendered font characters in UI panels and menus.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Fonts/alagard/alagard SDF.asset
Project Grimhold/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
```

#### Classification evidence

- type → Bug Fixes: fix
- scope → UI: ui
- message-term → UI: ui
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Fonts/alagard/alagard SDF.asset

#### Diagnostics

- No classification ambiguity detected.

### 718e66908c806c5a2b9f83ba34bf51897d4b51ba

[718e66908c806c5a2b9f83ba34bf51897d4b51ba](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/718e66908c806c5a2b9f83ba34bf51897d4b51ba)

Date: 2026-11-03T20:00:32-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Networking & Sessions, UI, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): wire town guild npc and mission tracker hud into scenes and prefabs
```

#### Original body

```text
Place TownMissionNPC in Lobby-Town with interaction and directional view, bind TownMissionGuild UI on SocialPlayer, and attach RaidMissionTracker with progress/completion popups and toggle key on NetworkPlayer. Automatically dismiss acceptance feedback on timeout or contract selection.

Player-Added: Players can interact with the Mission Guild NPC in Town to browse, accept, and claim contracts.

Player-Added: In-raid HUD displays active contracts and objective progress notifications.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/SocialPlayer.prefab
Project Grimhold/Assets/Prefabs/TownMissionNPC.prefab
Project Grimhold/Assets/Prefabs/TownMissionNPC.prefab.meta
Project Grimhold/Assets/Prefabs/UI/Missions/TownMissionGuild.prefab
Project Grimhold/Assets/Prefabs/UI/Missions/TownMissionGuild.prefab.meta
Project Grimhold/Assets/Scenes/Lobby-Town.unity
Project Grimhold/Assets/Scripts/Missions/Engine/IMissionClaimService.cs.meta
Project Grimhold/Assets/Scripts/Missions/Engine/MissionProgressEngine.cs
Project Grimhold/Assets/Scripts/Missions/Engine/RaidZoneTrigger.cs
Project Grimhold/Assets/Scripts/Missions/Engine/RaidZoneTrigger.cs.meta
Project Grimhold/Assets/Scripts/Networking/TownMissionNpcInteractable.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerView.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildPresenter.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildView.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildView.cs.meta
Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionEventProducersTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Missions/RankEResourceRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs
Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs.meta
```

#### Classification evidence

- type → Features: feat
- message-term → Inventory &amp; Loot: interaction
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scenes/Lobby-Town.unity
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownMissionNpcInteractable.cs.meta
- message-term → Networking &amp; Sessions: Lobby
- path → UI: Project Grimhold/Assets/Prefabs/UI/Missions/TownMissionGuild.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/Missions/TownMissionGuild.prefab.meta
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: hud
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionEventProducersTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RankEResourceRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 49bda06136a06fc7d15d3f6fd95604de93580108

[49bda06136a06fc7d15d3f6fd95604de93580108](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/49bda06136a06fc7d15d3f6fd95604de93580108)

Date: 2026-11-02T23:32:56-03:00 · Classification: Documentation (explicit) · Systems: Unassigned

Flags: secondary

#### Original subject

```text
docs(odd): complete mission system guild and contracts tracking
```

#### Original body

```text
Refs: US-75, #650
```

#### Changed paths

```text
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Documentation: docs

#### Diagnostics

- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### ccd10ad98a8173a5f468c7d4894702f0b330c05e

[ccd10ad98a8173a5f468c7d4894702f0b330c05e](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/ccd10ad98a8173a5f468c7d4894702f0b330c05e)

Date: 2026-11-02T23:30:32-03:00 · Classification: Features (explicit) · Systems: UI, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): implement active mission tracker in raid hud
```

#### Original body

```text
- Add MissionProgressChanged event to LocalProfileStore for reactive progress updates.
- Add RaidMissionTrackerPresenter and collapsible RaidMissionTrackerView.
- Present active raid contracts, objectives, and progress in real time without polling.
- Highlight completion and show 'Completada (Reclamar en Pueblo)' when phase completes.
- Wire RaidMissionTrackerPresenter into LocalPlayerHudBinder.
- Add comprehensive EditMode tests in RaidMissionTrackerPresenterTests.

Refs: US-75, TASK-261, #650, #653
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidMissionTrackerView.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Features: feat
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/LocalPlayerHudBinder.cs
- message-term → UI: hud
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionTrackerPresenterTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 103c6b821ffe4e9d08a9b3fe02ceeb0498a4cf2d

[103c6b821ffe4e9d08a9b3fe02ceeb0498a4cf2d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/103c6b821ffe4e9d08a9b3fe02ceeb0498a4cf2d)

Date: 2026-11-02T23:25:22-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Networking & Sessions, UI, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): implement town mission npc interactable and guild contracts ui
```

#### Original body

```text
- Add TownMissionNpcInteractable network behaviour for physical NPC interactions.
- Add TownMissionGuildPresenter and TownMissionGuildView with Available/Active tabs.
- Enforce strict 3 active contracts cap and safe abandonment.
- Enforce Stash capacity verification on claim preventing reward loss when full.
- Integrate TownMissionGuildPresenter into TownInventoryBinder lifecycle.
- Add comprehensive EditMode tests in TownMissionGuildPresenterTests.

Refs: US-75, TASK-259, TASK-260, #650, #651, #652
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Networking/TownMissionNpcInteractable.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildPresenter.cs
Project Grimhold/Assets/Scripts/Player/Presentation/TownMissionGuildView.cs
Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/TownInventoryBinder.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/TownMissionNpcInteractable.cs
- message-term → UI: ui
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/TownMissionGuildPresenterTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### e82801c1d12f336c9364bfc9706fa81382a7faec

[e82801c1d12f336c9364bfc9706fa81382a7faec](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e82801c1d12f336c9364bfc9706fa81382a7faec)

Date: 2026-11-02T23:14:43-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): implement authoritative raid event producers and duo coop rules
```

#### Original body

```text
- Add RaidZoneTrigger for zone exploration events with 35m duo proximity credit.
- Wire DamageResolver for fatal PvE (EliminacionPvE and EliminacionEspecifica) and companion credit within 35m.
- Wire NetworkLootContainerInteractable chest opening for interactor and companion within 35m.
- Wire NetworkLootPickup for individual item acquisition events.
- Wire PlayerPvpCombatCreditNetworkController for authoritative PvP kill mission progress.
- Add manual simulation buttons in MissionPlayModeDebugger.
- Add comprehensive edit mode unit tests in RaidMissionEventProducersTests.

Refs: US-75, TASK-262, #650, #654
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Missions/Debugging/MissionPlayModeDebugger.cs
Project Grimhold/Assets/Scripts/Missions/Engine/RaidZoneTrigger.cs
Project Grimhold/Assets/Scripts/Player/PlayerPvpCombatCreditNetworkController.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionEventProducersTests.cs
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Combat/DamageResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/PlayerPvpCombatCreditNetworkController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainerInteractable.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RaidMissionEventProducersTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 3a17806f543200f7f7e242eadb6a20d8a3b2a186

[3a17806f543200f7f7e242eadb6a20d8a3b2a186](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3a17806f543200f7f7e242eadb6a20d8a3b2a186)

Date: 2026-11-02T23:00:58-03:00 · Classification: Features (explicit) · Systems: Extraction, Inventory & Loot, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): implement dynamic resource rules and atomic claim deduction
```

#### Original body

```text
Add dynamic personal inventory possession evaluation for resource gathering contracts (CON_E_005), single-expedition extraction validation (CON_E_006) preserving RaidGenerationId, and atomic material deduction from player Loadout on reward claim via IMissionClaimService.

Reviewers must verify:
- ObjectiveProgressState records ExpeditionId for ExtraccionDeObjetos.
- LocalProfileStore.TryClaimMission consumes strictly from personal inventory and rejects claims when items are only in Stash.
- Concurrent active contracts recalculate satisfaction immediately upon shared resource deduction.

Added tests:
- RankEResourceRulesTests (8 EditMode tests covering E05-01..03, E06-01..03, MAT-01 and MAT-02).

Refs: US-75, TASK-271, #650, #695
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Missions/Engine/IMissionClaimService.cs
Project Grimhold/Assets/Scripts/Missions/Engine/MissionProgressEngine.cs
Project Grimhold/Assets/Scripts/Missions/State/MissionLifecycleRules.cs
Project Grimhold/Assets/Scripts/Missions/State/ObjectiveProgressState.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RankEResourceRulesTests.cs
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Features: feat
- message-term → Extraction: extraction
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RankEResourceRulesTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### aba4e90a18c8e8c257bccac4d2c58dd8d90647bb

[aba4e90a18c8e8c257bccac4d2c58dd8d90647bb](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/aba4e90a18c8e8c257bccac4d2c58dd8d90647bb)

Date: 2026-11-02T22:36:37-03:00 · Classification: Features (explicit) · Systems: Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(missions): configure canonical rank e contracts and guild reputation persistence
```

#### Original body

```text
Configure the 6 canonical Rank E daily contract ScriptableObjects (CON_E_001 to CON_E_006) with their objective families, quantities, and reward packages (XP, Gold, Guild Reputation) per design Doc 12 and Rank E Contracts spec. Extend LocalProfileSnapshot with GuildReputation, GuildRank (E for 0..99, D for 100+), bump LocalProfileSaveCodec to SchemaVersion 10 with backward compatibility, and atomic reputation accumulation and rank validation in LocalProfileStore.

Refs: US-75, TASK-263, TASK-270, #650, #655, #687
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_01.asset
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_02.asset
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_03.asset
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_04.asset
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_05.asset
Project Grimhold/Assets/Scriptable Objects/Missions/Mission_RankE_06.asset
Project Grimhold/Assets/Scripts/Missions/Definitions/MissionDefinitionCatalog.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs
odd/tasks/mission-system-guild-and-contracts.md
```

#### Classification evidence

- type → Features: feat
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- message-term → Persistence &amp; Backend: persistence
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Missions/RankEContractsAndGuildReputationTests.cs

#### Diagnostics

- No classification ambiguity detected.

### bada9b0a4fac2f79a9ad338a591a7efb14f33051

[bada9b0a4fac2f79a9ad338a591a7efb14f33051](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/bada9b0a4fac2f79a9ad338a591a7efb14f33051)

Date: 2026-11-04T14:32:58-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, UI, Persistence & Backend, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(equipment): add provisional gems and weapon gem installation
```

#### Original body

```text
Add the pure part of TASK-283: gems that can be installed in weapon
instance sockets, their flat damage bonus and its presentation. Inventory
drag and drop, stack consumption, persistence, replication and runtime
combat use remain blocked: weapon instances are not yet carried by
Inventory, Stash, Equipment or the backend (PlayerCombatArchitecture
section 3), so WeaponInstanceModifiers is still not an active runtime
source.

- GemDefinition holds a positive DamageBonus. LootDefinition gains an
  optional GemDefinition, valid only on Resource loot.
- WeaponInstanceModifiers.TryInstallGem fills the lowest empty socket
  permanently. It rejects null or non-gem loot, weapons without sockets
  and full weapons with a WeaponGemInstallFailure, leaving the weapon
  unchanged.
- WeaponGemDamageBonusResolver sums installed gems through the loot
  catalog and fails on gems the catalog cannot resolve.
- WeaponDamageCalculator adds the gem bonus to base damage before
  scaling: floor((Base + Gems) * (1 + sum of contributions)). The
  existing overload delegates with a zero bonus.
- The instance tooltip overload now takes the loot catalog, lists
  installed gem names per socket ("Ranuras: 2 (Ruby Gem, vacía)"), shows
  "Bono de gemas: +N" and reports invalid configuration for unresolvable
  gems.

Provisional content until GD-25: GEM_ATTUNEMENT, GEM_RUBY and
GEM_SAPPHIRE each grant +5 damage regardless of damage type.
RES_ATTUNEMENT_GEM references its gem; new RES_RUBY_GEM and
RES_SAPPHIRE_GEM copy its values and sprite and are registered in
LootDefinitionCatalog and RaidLootValueCatalog (placeholder 100). Saving
RES_ATTUNEMENT_GEM also reserialized its stale _equipmentVisualDefinition
and _defenseSprites fields to the current schema.

Tests: WeaponGemInstallationTests (new); gem cases in
WeaponDamageCalculatorTests, LootDefinitionTests, MvpItemCatalogTests
and EquipmentTooltipPresentationBuilderTests, including a check that the
tooltip damage equals the calculator result.

Refs: TASK-283
```

#### Changed paths

```text
Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES_ATTUNEMENT_GEM.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES_RUBY_GEM.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES_RUBY_GEM.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES_SAPPHIRE_GEM.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES_SAPPHIRE_GEM.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Gems.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_ATTUNEMENT.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_ATTUNEMENT.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_RUBY.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_RUBY.asset.meta
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_SAPPHIRE.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM_SAPPHIRE.asset.meta
Project Grimhold/Assets/Scriptable Objects/RaidLootValueCatalog.asset
Project Grimhold/Assets/Scripts/Combat/WeaponDamageCalculator.cs
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Scripts/Player/Equipment/GemDefinition.cs
Project Grimhold/Assets/Scripts/Player/Equipment/GemDefinition.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Combat/WeaponDamageCalculator.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs.meta
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES\_ATTUNEMENT\_GEM.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES\_RUBY\_GEM.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES\_RUBY\_GEM.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES\_SAPPHIRE\_GEM.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/RES\_SAPPHIRE\_GEM.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_ATTUNEMENT.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_ATTUNEMENT.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_RUBY.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_RUBY.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_SAPPHIRE.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Gems/GEM\_SAPPHIRE.asset.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/RaidLootValueCatalog.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/GemDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/GemDefinition.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemDamageBonusResolver.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemInstallFailure.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- scope → Inventory &amp; Loot: equipment
- message-term → Inventory &amp; Loot: equipment
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → UI: tooltip
- message-term → Persistence &amp; Backend: persistence
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Combat/WeaponDamageCalculatorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemInstallationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootDefinitionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/MvpItemCatalogTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 0c88bc81ab092de384b7c394c433ee4569f08c47

[0c88bc81ab092de384b7c394c433ee4569f08c47](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0c88bc81ab092de384b7c394c433ee4569f08c47)

Date: 2026-11-04T02:43:38-03:00 · Classification: Documentation (explicit) · Systems: UI, Art & Animation, Tests

Flags: secondary

#### Original subject

```text
docs(odd): record sprite integration and pending Editor checks
```

#### Original body

```text
Record the four retained work units and conflict-free New-Testing merge.
Preserve the owner stash and intentional minimap cache exclusion.

Distinguish passing pre-merge tests from unavailable post-merge Unity
checks; the Editor session must be reconnected to complete those checks.
```

#### Changed paths

```text
odd/tasks/consumables-utilities-delivery.md
```

#### Classification evidence

- type → Documentation: docs
- message-term → UI: minimap
- message-term → Art &amp; Animation: sprite
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 50a6daa375aead0fbf6e71b18715eba75959fe60

[50a6daa375aead0fbf6e71b18715eba75959fe60](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/50a6daa375aead0fbf6e71b18715eba75959fe60)

Date: 2026-11-04T02:32:33-03:00 · Classification: Refactoring (explicit) · Systems: Abilities, Inventory & Loot, UI, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
refactor(ui): consolidate player HUD and retain inventory corrections
```

#### Original body

```text
Preserve the authored migration of downed, ability and quick-slot
presentation into the nested local HUD, with player references updated
together. Retain the inventory icon and label layout corrections and
the selected generated font glyph cache.

Resolve HUD geometry in a temporary Canvas before checking the driven
minimap size and compass separation. Keep all assertions and cover both
1920x1080 and 1440x1080 layouts. The expanded relevant EditMode selection
passes all seventy-nine cases.

Player-Improved: Inventory item icons and labels are easier to read.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Fonts/alagard/alagard SDF.asset
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudStylePassPrefabTests.cs
```

#### Classification evidence

- type → Refactoring: refactor
- message-term → Abilities: ability
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- message-term → Inventory &amp; Loot: inventory
- path → UI: Project Grimhold/Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab
- path → UI: Project Grimhold/Assets/Prefabs/UI/RaidInventorySlot.prefab
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudStylePassPrefabTests.cs
- scope → UI: ui
- message-term → UI: ui
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Fonts/alagard/alagard SDF.asset
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudStylePassPrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/RaidHudStylePassPrefabTests.cs

#### Diagnostics

- No classification ambiguity detected.

### dd878762fb7ce202579c8691d27fc79501704f04

[dd878762fb7ce202579c8691d27fc79501704f04](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/dd878762fb7ce202579c8691d27fc79501704f04)

Date: 2026-11-04T02:32:33-03:00 · Classification: Bug Fixes (explicit) · Systems: Combat, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(consumables): align the throwing dagger tip with its flight
```

#### Original body

```text
Adjust only the projectile visual basis from -90 to -45 degrees for
the supplied diagonal artwork. Preserve its existing sprite selection,
physics root and trajectory.

Add eight directional prefab alignment cases. The owner confirmed the
result in game; all cases pass in Unity.

Player-Fixed: The throwing dagger points toward its direction of travel.
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Consumables/ThrowingDaggerProjectile.prefab
Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs.meta
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Combat: Project Grimhold/Assets/Prefabs/Consumables/ThrowingDaggerProjectile.prefab
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs.meta
- message-term → Combat: projectile
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Consumables/ThrowingDaggerProjectilePrefabTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### 9a1c35cf7def67ab73e8b3223259fd1617efc0b8

[9a1c35cf7def67ab73e8b3223259fd1617efc0b8](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9a1c35cf7def67ab73e8b3223259fd1617efc0b8)

Date: 2026-11-04T02:32:33-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, Art & Animation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): synchronize cached pickup world sprites
```

#### Original body

```text
Refresh a resolved pickup when its renderer still shows a different
sprite. Cached definition identity alone no longer skips presentation.

Add a real Fusion spawn regression for preset and overridden content,
including quantity, provenance and sorting preservation. All seven
focused pickup PlayMode cases pass after the observed sprite failure.

Player-Fixed: Dropped items display their correct world sprite instead of the prefab placeholder.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Tests/PlayMode/NetworkLootPickupPartialPickupPlayModeTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/NetworkLootPickupPartialPickupPlayModeTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → Art &amp; Animation: sprites
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/NetworkLootPickupPartialPickupPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### fd1e2a4093e44d515e322a9e6d874cfd08029e55

[fd1e2a4093e44d515e322a9e6d874cfd08029e55](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/fd1e2a4093e44d515e322a9e6d874cfd08029e55)

Date: 2026-11-04T02:32:33-03:00 · Classification: Features (explicit) · Systems: Combat, Abilities, Inventory & Loot, UI, Art & Animation, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(art): integrate consumable and utility sprites
```

#### Original body

```text
Import all thirty supplied variants with 16 PPU, Point filtering and
no compression. Bind the ten existing item definitions to their world
sprites and UI icons, and update the explosive bomb visual.

Add ten asset cases for exact references, import settings and catalog
registration. The serialized ally damage multiplier matches its existing
default of one. Interactable variants are imported, not behavior-wired.

Player-Improved: Consumables and utilities have distinct world sprites and inventory icons.
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Consumables & Utilities.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/ArcaneDetector/ArcaneDetector/UTL_ARCANE_DETECTOR-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/ExplosiveBomb/ExplosiveBomb/CON_EXPLOSIVE_BOMB-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion.meta
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion.meta
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/HealthPotion/HealthPotion/CON_HEALTH_POTION-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick.meta
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick.meta
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/Lockpick/Lockpick/CON_LOCKPICK-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaFragment/ManaFragment/CON_MANA_FRAGMENT-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/ManaPotion/ManaPotion/CON_MANA_POTION-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass.meta
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass.meta
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/RuneCompass/RuneCompass/UTL_RUNE_COMPASS-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/SelfRevive/SelfRevive/CON_SELF_REVIVE-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/SmokeBomb/SmokeBomb/CON_SMOKE_BOMB-Interactable.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Base.png
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Base.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Icon.png
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Icon.png.meta
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Interactable.png
Project Grimhold/Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Interactable.png.meta
Project Grimhold/Assets/Prefabs/Consumables/ExplosiveBomb.prefab
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_EXPLOSIVE_BOMB.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_HEALTH_POTION.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_LOCKPICK.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_MANA_FRAGMENT.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_MANA_POTION.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_SELF_REVIVE.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_SMOKE_BOMB.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON_THROWING_DAGGER.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/UTL_ARCANE_DETECTOR.asset
Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/UTL_RUNE_COMPASS.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion.meta
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion.meta
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Base.png
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Base.png.meta
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Icon.png
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Icon.png.meta
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Interactable.png
- path → Combat: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Interactable.png.meta
- path → Combat: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_HEALTH\_POTION.asset
- message-term → Combat: damage
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_EXPLOSIVE\_BOMB.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_HEALTH\_POTION.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_LOCKPICK.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_MANA\_FRAGMENT.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_MANA\_POTION.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_SELF\_REVIVE.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_SMOKE\_BOMB.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/CON\_THROWING\_DAGGER.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/UTL\_ARCANE\_DETECTOR.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scriptable Objects/Loot/Definitions/UTL\_RUNE\_COMPASS.asset
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta
- message-term → Inventory &amp; Loot: inventory
- message-term → UI: UI
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ArcaneDetector/ArcaneDetector/UTL\_ARCANE\_DETECTOR-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ExplosiveBomb/ExplosiveBomb/CON\_EXPLOSIVE\_BOMB-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/HealthPotion/HealthPotion/CON\_HEALTH\_POTION-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/Lockpick/Lockpick/CON\_LOCKPICK-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaFragment/ManaFragment/CON\_MANA\_FRAGMENT-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ManaPotion/ManaPotion/CON\_MANA\_POTION-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/RuneCompass/RuneCompass/UTL\_RUNE\_COMPASS-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SelfRevive/SelfRevive/CON\_SELF\_REVIVE-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/SmokeBomb/SmokeBomb/CON\_SMOKE\_BOMB-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Base.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Base.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Icon.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Icon.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Interactable.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Consumables &amp; Utilities/ThrowingDagger/ThrowingDagger/CON\_THROWING\_DAGGER-Interactable.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta
- scope → Art &amp; Animation: art
- message-term → Art &amp; Animation: sprites
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/ConsumableUtilitySpriteAssetsTests.cs.meta

#### Diagnostics

- No classification ambiguity detected.

### b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c

[b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b4dfeabab36f58accbd4eb9b5efe8e5b686cf65c)

Date: 2026-11-04T01:27:42-03:00 · Classification: Documentation (explicit) · Systems: Extraction, Inventory & Loot, UI, Persistence & Backend, Documentation

Flags: secondary

#### Original subject

```text
docs(inventory): document physical slots and TASK-281 split
```

#### Original body

```text
Record the approved Town/Stash physical-slot model and the TASK-281
split as implemented.

- LocalPlayerPersistenceArchitecture: slot model replaces the TASK-218
  compact exception; codec schema 10, inventorySchemaVersion, protocol
  gate, migration and rollback floors; Town StackMax stays deferred;
  Town Origin under D2 = O1 pending Game Design doc 07 reconciliation.
- RaidInventoryUIArchitecture: slot projection, Stash slot-range paging,
  Loadout fixed at 30 visible slots without paging.
- LootInteractionArchitecture: Raid and Town split contracts; UI stays
  in TASK-282.
- ExtractionArchitecture: extraction placement into Loadout slots.
- docs/proposal_task281_town_stash_physical_slots.md (approved rev 4),
  docs/defect_raid_loot_eligibility_other_team.md (open GD conflict) and
  the ODD tracker odd/tasks/stash-physical-slots.md.
```

#### Changed paths

```text
Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
docs/defect_raid_loot_eligibility_other_team.md
docs/proposal_task281_town_stash_physical_slots.md
odd/tasks/stash-physical-slots.md
```

#### Classification evidence

- type → Documentation: docs
- path → Extraction: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Inventory &amp; Loot: docs/defect\_raid\_loot\_eligibility\_other\_team.md
- scope → Inventory &amp; Loot: inventory
- message-term → Inventory &amp; Loot: inventory
- message-term → UI: UI
- path → Persistence &amp; Backend: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/ExtractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Documentation: Project Grimhold/Docs/Architecture/RaidInventoryUIArchitecture.md
- path → Documentation: docs/defect\_raid\_loot\_eligibility\_other\_team.md
- path → Documentation: docs/proposal\_task281\_town\_stash\_physical\_slots.md

#### Diagnostics

- No classification ambiguity detected.

### fc872660c13a0c839d39c3edeab09c9d5ca85a00

[fc872660c13a0c839d39c3edeab09c9d5ca85a00](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/fc872660c13a0c839d39c3edeab09c9d5ca85a00)

Date: 2026-11-04T01:27:32-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, Networking & Sessions, UI, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(stash): adopt physical slots in Town inventory and Stash
```

#### Original body

```text
Move the Unity Town Loadout and Stash to the physical-slot model
served by the backend (D1, D3) and add the authoritative Stash split
for TASK-281 without UI.

- StashItem.Slot; save codec schema 10 persists slots, duplicate
  lootIds and holes, plus the backend inventorySchemaVersion. Schemas
  1-9 decode losslessly with slot = list index.
- Hydration no longer merges entries; every backend request sends
  X-Grimhold-Inventory-Protocol: 2. With a v1 backend document the
  coordinator reports every mutation as unavailable before any remote
  call.
- Slot rules in ProfileInventoryRules, LocalProfileStore and
  MerchantTradePreview: capacity by occupied slots, overflow
  reducing-only, receive into the lowest matching then lowest free
  slot, removal leaves a hole, consumption in ascending slot order,
  Quick Slot counts sum every stack, exact reservation rollback.
  A pending reservation is still reported before input errors.
- Slot-addressed transfer, sell, Sort and slot move through
  RemoteInventoryService and StashTransferCoordinator.
- LocalProfileStore.TrySplit and StashTransferCoordinator.SplitAsync
  (remote first, serialized queue, reconcile without retry).
- Raid admission aggregates reservation stacks by lootId
  (RaidAdmissionData, FusionSessionLauncher); its duplicate-ID and
  entry limits are unchanged.
- Lobby renders each entry at its slot, Stash pages by slot range,
  the Loadout keeps its 30 visible cells and cell drags use slot moves.

Tests: PhysicalProfileSlotTests, StashSplitTests,
InventoryProtocolSlotTests, RaidAdmissionSlotAggregationTests and
slot cases in StashTransferCoordinatorTests, StashReservationInvariant
and LobbyStashPresenter PlayMode; existing Stash, Equipment, Commerce,
persistence and shop fixtures updated to explicit slots.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Backend/AuthenticationClient.cs
Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
Project Grimhold/Assets/Scripts/Backend/ProgressionClient.cs
Project Grimhold/Assets/Scripts/Commerce/MerchantTradePreview.cs
Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
Project Grimhold/Assets/Scripts/Stash/ApplicationStashServiceBootstrapper.cs
Project Grimhold/Assets/Scripts/Stash/IPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/IPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/IRemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerLoadoutService.cs.meta
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotPlayerStashService.cs.meta
Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs.meta
Project Grimhold/Assets/Scripts/Stash/InMemoryPlayerLoadoutService.cs
Project Grimhold/Assets/Scripts/Stash/InMemoryPlayerStashService.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
Project Grimhold/Assets/Scripts/Stash/RemoteShopTransactionService.cs
Project Grimhold/Assets/Scripts/Stash/StashDisplayOrder.cs
Project Grimhold/Assets/Scripts/Stash/StashItem.cs
Project Grimhold/Assets/Scripts/Stash/StashTransferCoordinator.cs
Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/IRemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ISlotRemoteInventoryService.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/LocalLoadoutInventoryReadSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/RemoteInventoryService.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Stash/TownInventoryProjection.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/FusionSessionLauncher.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Networking/RaidAdmissionData.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashPresenter.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Networking &amp; Sessions: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- message-term → Networking &amp; Sessions: Lobby
- path → UI: Project Grimhold/Assets/Scripts/Stash/LobbyStashUI.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/AuthenticationClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/CharacterClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryDataTransferObjects.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryProtocol.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventoryRequestJson.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotMoveRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotOrderRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/InventorySlotSplitRequest.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Backend/ProgressionClient.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSaveCodec.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileSnapshot.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/LocalProfileStore.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Scripts/Stash/ProfileInventoryRules.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Persistence &amp; Backend: Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
- message-term → Persistence &amp; Backend: backend
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Backend/InventoryProtocolSlotTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Commerce/MerchantTradePreviewTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/PreparedEquipmentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Networking/RaidAdmissionSlotAggregationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalProfileSlotTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/PhysicalSlotTestSeed.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/ProfileInventoryRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RaidReservationLifecycleTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RecoveryWeaponConsentTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/RemoteShopTransactionServiceTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCapacityTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashCorruptLoginRecoveryTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashDisplayOrderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashHydrationCapacityTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashReservationInvariantTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashSplitTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferCoordinatorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Stash/StashTransferEvaluationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/LocalProfilePersistenceEditModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/ShopTransactionPersistenceEditModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayModeMerchantComposition.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/LobbyStashPresenterPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/Presentation/RaidInventoryPresenterInteractionTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0

[43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/43fb6ad3c41e36f3d63761cfa7a8a87ec25289d0)

Date: 2026-11-04T01:27:16-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): add one-time inventory slot migration script
```

#### Original body

```text
Add backend/scripts/migrate_inventory_slots.js to move legacy aggregate
inventories to physical slots once, explicitly, during the cutover
window. Reads and ordinary writes never migrate data.

- Per list (stash, loadout, pendingReservation.items) in stored order:
  fold legacy duplicate lootIds into the first occurrence, assign
  slots 0..n-1, keep over-capacity entries in overflow slots and
  report unknown lootIds without dropping them.
- One revision-guarded write per character sets
  inventorySchemaVersion 2; per-lootId totals must match or the
  character is skipped unchanged. Version 2 is skipped, so reruns are
  no-ops. --dry-run writes nothing and prints a report.

Tests: migrate.inventory.slots (planner) and integration (dry run,
migration, rerun, revision conflict, aborted character unchanged).
```

#### Changed paths

```text
backend/scripts/migrate_inventory_slots.js
backend/tests/integration/migrate.inventory.slots.test.js
backend/tests/migrate.inventory.slots.test.js
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: backend/scripts/migrate\_inventory\_slots.js
- path → Inventory &amp; Loot: backend/tests/integration/migrate.inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/migrate.inventory.slots.test.js
- message-term → Inventory &amp; Loot: inventory
- path → Persistence &amp; Backend: backend/scripts/migrate\_inventory\_slots.js
- path → Persistence &amp; Backend: backend/tests/integration/migrate.inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/migrate.inventory.slots.test.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/tests/integration/migrate.inventory.slots.test.js
- path → Tests: backend/tests/migrate.inventory.slots.test.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### e36cafcb12bcddae3cae576069b83d0e63768cb6

[e36cafcb12bcddae3cae576069b83d0e63768cb6](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e36cafcb12bcddae3cae576069b83d0e63768cb6)

Date: 2026-11-04T01:27:15-03:00 · Classification: Features (explicit) · Systems: Extraction, Inventory & Loot, Networking & Sessions, Persistence & Backend, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(backend): persist physical inventory slots behind a protocol gate
```

#### Original body

```text
Move Town Loadout and Stash persistence from one aggregate entry per
lootId to physical slots (approved decisions D1 and D3), and add the
authoritative Stash/Loadout split for TASK-281 behind a feature flag.

- Character items gain an optional slot; inventory.inventorySchemaVersion
  1 = legacy aggregate lists, 2 = physical slots. Reads never merge or
  save; the read-time lootId normalizer is removed.
- X-Grimhold-Inventory-Protocol: 2 middleware on every inventory,
  reservation, extraction and admission route. INVENTORY_PROTOCOL_GATE
  (default off) rejects 426 CLIENT_UPGRADE_REQUIRED and v1 documents
  409 INVENTORY_MIGRATION_REQUIRED.
- v2 rules: capacity by occupied slots (Loadout 30, Stash 60), overflow
  slots reducing-only, receive into the lowest matching then lowest
  free slot, removal leaves a hole, sourceSlot-addressed transfer and
  sell, slotOrder Sort as the only compaction, POST /slots/move
  (move, merge, swap), exact reservation rollback, extraction placed
  into slots and rejected above capacity before claiming the result.
- POST /slots/split behind INVENTORY_SPLIT_ENABLED (default off,
  403 FEATURE_DISABLED); revision-guarded, rejected during a pending
  reservation.
- Raid admission tickets stay aggregated by lootId.

Tests: inventory.protocol, inventory.slots, inventory.split (unit,
integration, concurrency) and inventory.slots.expand; read-time
normalization expectations in inventory.service replaced.

Docs: backend/README.md (protocol, flags, DTOs, error codes).
```

#### Changed paths

```text
backend/README.md
backend/scripts/seed-test-accounts.js
backend/src/config/env.js
backend/src/middleware/inventoryProtocol.js
backend/src/models/Character.js
backend/src/routes/character.routes.js
backend/src/routes/inventory.routes.js
backend/src/routes/raid.routes.js
backend/src/services/CharacterService.js
backend/src/services/ExtractionCommitService.js
backend/src/services/InventoryEntries.js
backend/src/services/InventoryLootIdNormalizer.js
backend/src/services/InventoryService.js
backend/src/services/RaidResultService.js
backend/src/services/SessionTicketService.js
backend/src/validators/inventory.validators.js
backend/tests/concurrency/inventory.real.test.js
backend/tests/concurrency/inventory.split.real.test.js
backend/tests/integration/inventory.slots.expand.test.js
backend/tests/integration/inventory.slots.test.js
backend/tests/integration/inventory.split.test.js
backend/tests/inventory.protocol.test.js
backend/tests/inventory.service.test.js
backend/tests/inventory.slots.test.js
backend/tests/inventory.split.test.js
backend/tests/setup.js
```

#### Classification evidence

- type → Features: feat
- path → Extraction: backend/src/services/ExtractionCommitService.js
- message-term → Extraction: extraction
- path → Inventory &amp; Loot: backend/src/middleware/inventoryProtocol.js
- path → Inventory &amp; Loot: backend/src/routes/inventory.routes.js
- path → Inventory &amp; Loot: backend/src/services/InventoryEntries.js
- path → Inventory &amp; Loot: backend/src/services/InventoryLootIdNormalizer.js
- path → Inventory &amp; Loot: backend/src/services/InventoryService.js
- path → Inventory &amp; Loot: backend/src/validators/inventory.validators.js
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.real.test.js
- path → Inventory &amp; Loot: backend/tests/concurrency/inventory.split.real.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.slots.expand.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/integration/inventory.split.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.protocol.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.service.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.slots.test.js
- path → Inventory &amp; Loot: backend/tests/inventory.split.test.js
- message-term → Inventory &amp; Loot: inventory
- path → Networking &amp; Sessions: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/README.md
- path → Persistence &amp; Backend: backend/scripts/seed-test-accounts.js
- path → Persistence &amp; Backend: backend/src/config/env.js
- path → Persistence &amp; Backend: backend/src/middleware/inventoryProtocol.js
- path → Persistence &amp; Backend: backend/src/models/Character.js
- path → Persistence &amp; Backend: backend/src/routes/character.routes.js
- path → Persistence &amp; Backend: backend/src/routes/inventory.routes.js
- path → Persistence &amp; Backend: backend/src/routes/raid.routes.js
- path → Persistence &amp; Backend: backend/src/services/CharacterService.js
- path → Persistence &amp; Backend: backend/src/services/ExtractionCommitService.js
- path → Persistence &amp; Backend: backend/src/services/InventoryEntries.js
- path → Persistence &amp; Backend: backend/src/services/InventoryLootIdNormalizer.js
- path → Persistence &amp; Backend: backend/src/services/InventoryService.js
- path → Persistence &amp; Backend: backend/src/services/RaidResultService.js
- path → Persistence &amp; Backend: backend/src/services/SessionTicketService.js
- path → Persistence &amp; Backend: backend/src/validators/inventory.validators.js
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.real.test.js
- path → Persistence &amp; Backend: backend/tests/concurrency/inventory.split.real.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.slots.expand.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/integration/inventory.split.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.protocol.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.service.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.slots.test.js
- path → Persistence &amp; Backend: backend/tests/inventory.split.test.js
- path → Persistence &amp; Backend: backend/tests/setup.js
- scope → Persistence &amp; Backend: backend
- message-term → Persistence &amp; Backend: backend
- path → Tests: backend/scripts/seed-test-accounts.js
- path → Tests: backend/tests/concurrency/inventory.real.test.js
- path → Tests: backend/tests/concurrency/inventory.split.real.test.js
- path → Tests: backend/tests/integration/inventory.slots.expand.test.js
- path → Tests: backend/tests/integration/inventory.slots.test.js
- path → Tests: backend/tests/integration/inventory.split.test.js
- path → Tests: backend/tests/inventory.protocol.test.js
- path → Tests: backend/tests/inventory.service.test.js
- path → Tests: backend/tests/inventory.slots.test.js
- path → Tests: backend/tests/inventory.split.test.js
- path → Tests: backend/tests/setup.js
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### b815be1341cce259b562fcc6de08eea2fa5590d7

[b815be1341cce259b562fcc6de08eea2fa5590d7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/b815be1341cce259b562fcc6de08eea2fa5590d7)

Date: 2026-11-04T01:26:59-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, UI, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add authoritative Raid stack split
```

#### Original body

```text
Add the State Authority split for the Raid inventory (TASK-281, Raid
half). A split moves part of one addressed physical stack into an
empty addressed slot; the new stack keeps the source item and Raid
origin, so the total and every origin bucket are conserved (INV-AC-03).

- LootInventoryRules.ValidateSplit/TrySplit: amount in [1, qty-1],
  empty distinct destination, occupied slots below the gameplay
  capacity; rejection writes nothing.
- PlayerLootReceiver.TrySplitStack: State Authority only, rejects a
  stale revision or extracted player, advances the revision once.
- Rejections reuse existing LootTransferFailureReason values.
- The Input Authority request path and UI stay in TASK-282.

Tests:
- LootInventorySlotRulesTests: 13 -> 5 + 8, mixed origins, invalid
  requests and capacity are atomic.
- PlayerLootReceiverSlotsPlayModeTests: mixed-origin split, stale and
  reused revision, occupied destination, whole-stack amount.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootInventoryRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/PlayerLootReceiver.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → UI: UI
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/LootInventorySlotRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/PlayerLootReceiverSlotsPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 9727c4f970c91b260ca6e1162ed2a0594ef2f636

[9727c4f970c91b260ca6e1162ed2a0594ef2f636](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9727c4f970c91b260ca6e1162ed2a0594ef2f636)

Date: 2026-11-03T16:32:27-03:00 · Classification: Features (explicit) · Systems: Combat, Inventory & Loot, UI, World & Visibility, Persistence & Backend, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(equipment): add weapon gem sockets and Dungeon variant generator
```

#### Original body

```text
Extend WeaponInstanceModifiers with 0-2 gem sockets, each empty or
holding one installed gem LootId, as described in Doc 09 sections 20.2
and 22. Add a pure, seed-deterministic WeaponInstanceGenerator that
rolls legal Dungeon variants per Doc 09 section 8.2: the primary scaling
matches the template's natural attribute, the secondary uses a
different attribute, and grades range from E to S.

Generation probabilities live in WeaponInstanceGenerationRules (basis
point chances, E-S grade weights, socket count weights, gem pool). Doc
09 section 8.1 assigns them to Loot/Balance, so no defaults are
authored. WeaponInstanceModifiersNetworkData is a blittable
INetworkStruct that encodes gems as catalog index + 1. It is
intentionally not replicated: PlayerCombatArchitecture.md section 3
keeps instance modifiers out of LootEntry, Equipment and persistence
until unique Item Instances exist end to end.

EquipmentTooltipPresentationBuilder gains an instance overload that
shows dual scaling grades ("FUE A · VIT D"), socket occupancy
("Ranuras: 2 (vacías)") and the effective damage from
WeaponDamageCalculator. The definition-only overload output is unchanged.

Chest integration and inventory/stash display remain blocked by the
Template/Instance migration.

Tests:
- WeaponGemSocketTests: socket limits, gems beyond the socket count,
  network data round trip and rejection paths.
- WeaponInstanceGeneratorTests: 1,000 weapons per STR/DEX/INT with
  legal combinations and socket limits, determinism, rules validation.
- EquipmentTooltipPresentationBuilderTests: dual scaling, socket
  occupancy, effective damage, invalid and non-weapon instances.

Validation: 104/104 focused EditMode tests pass. Full EditMode suite:
5516/5541; the 25 failures are in unrelated prefab, atlas, animator,
catalog and RPC tests. No baseline run without this change was made.

Refs: TASK-280
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
```

#### Classification evidence

- type → Features: feat
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Combat: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Combat: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- message-term → Combat: weapon
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponGemSocket.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerationRules.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceGenerator.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiers.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Equipment/WeaponInstanceModifiersNetworkData.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- scope → Inventory &amp; Loot: equipment
- message-term → Inventory &amp; Loot: equipment
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/EquipmentTooltipPresentationBuilder.cs
- path → UI: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → World &amp; Visibility: Dungeon
- message-term → Persistence &amp; Backend: persistence
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponGemSocketTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Equipment/WeaponInstanceGeneratorTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/EquipmentTooltipPresentationBuilderTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 8acc80236d68d6a526ab268b703bb4374c601283

[8acc80236d68d6a526ab268b703bb4374c601283](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8acc80236d68d6a526ab268b703bb4374c601283)

Date: 2026-11-02T20:38:54-03:00 · Classification: Documentation (explicit) · Systems: Unassigned

Flags: secondary

#### Original subject

```text
docs(odd): record T7 void bell cadence preservation
```

#### Original body

```text
Refs: US-76, TASK-264, #656, #657
```

#### Changed paths

```text
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Documentation: docs

#### Diagnostics

- **unassigned-system:** No recognized exact path/scope token or bounded system term. Inspect the listed paths before assigning a system.

### 9c5cf4ae4857ff3957d277f4319744033db7d03d

[9c5cf4ae4857ff3957d277f4319744033db7d03d](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9c5cf4ae4857ff3957d277f4319744033db7d03d)

Date: 2026-11-02T20:37:57-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): prevent void bell chime spam by preserving interval cadence
```

#### Original body

```text
Preserve the active VoidBellChimeTimer across intermittent sprint key releases instead of clearing it immediately to TickTimer.None, enforcing the authoritative 2.0s chime cadence interval and preventing rapid Shift-tap chime and aggro spam. Add an automated PlayMode assertion ensuring chime sequence stability during rapid sprint restarts.

Player-Fixed: Sprinting with the Void Bell no longer repeatedly chimes when pressing and releasing sprint rapidly.
Refs: US-76, TASK-264, #656, #657
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 7ef8f7a27b0b7e964d2239ae071781ab4a99871c

[7ef8f7a27b0b7e964d2239ae071781ab4a99871c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/7ef8f7a27b0b7e964d2239ae071781ab4a99871c)

Date: 2026-11-02T20:22:13-03:00 · Classification: Documentation (explicit) · Systems: Tests

Flags: secondary

#### Original subject

```text
docs(odd): record T6 rune compass tracking and test isolation
```

#### Original body

```text
Refs: US-76, TASK-265, #656, #658
```

#### Changed paths

```text
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Documentation: docs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 8480bedab3e96c387caef697f775f23942fa78f2

[8480bedab3e96c387caef697f775f23942fa78f2](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/8480bedab3e96c387caef697f775f23942fa78f2)

Date: 2026-11-02T20:21:05-03:00 · Classification: Features (explicit) · Systems: Abilities, Extraction, Inventory & Loot, Art & Animation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(utilities): render rune compass pointer as dynamic tracking sprite
```

#### Original body

```text
Render the Rune Compass visual indicator via a crisp 2D sprite orbiting the player at 1.35m radius, continuously rotating and tracking the nearest Extraction Sanctuary in real-time throughout its 5.0s display duration. Pass sanctuary coordinates through RPC_ReceiveCompassActivation to support dynamic recalculation, and gate PassiveAndUtilityDebugOverlay against automated test runner scenes.

Player-Improved: Activating a Rune Compass now projects a dynamic celestial needle sprite that stays pointed toward the nearest Extraction Sanctuary in real time as you move.
Refs: US-76, TASK-265, #656, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs.meta
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
```

#### Classification evidence

- type → Features: feat
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- message-term → Extraction: Extraction
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
- message-term → Art &amp; Animation: sprite
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### c79d5e225afbc0c4ab22c22620be96830d0c96fd

[c79d5e225afbc0c4ab22c22620be96830d0c96fd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/c79d5e225afbc0c4ab22c22620be96830d0c96fd)

Date: 2026-11-02T17:59:23-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, UI

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): add rune compass directional visual and prevent notice overwrite
```

#### Original body

```text
Prevent RaidHudPresenter from instantly overwriting the compass notice with generic Item used confirmation text. Implement RuneCompassPointerEffect to present a celestial directional arrow and crystal chime on the player pointing toward the nearest sanctuary, and log orientation in F10 overlay and console.

Refs: US-76, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/CardinalDirectionHelper.cs
Project Grimhold/Assets/Scripts/Gameplay/Utilities/RuneCompassPointerEffect.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerConsumableNetworkController.cs
- path → UI: Project Grimhold/Assets/Scripts/Player/Presentation/RaidHudPresenter.cs

#### Diagnostics

- No classification ambiguity detected.

### a1a97759e15381cca528f43576826e9857008abd

[a1a97759e15381cca528f43576826e9857008abd](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a1a97759e15381cca528f43576826e9857008abd)

Date: 2026-11-02T17:24:09-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, UI, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): restore arcane detector pickup prefab and scene instance
```

#### Original body

```text
Restore valid YAML hierarchy and component definitions on LootPickup_ArcaneDetector prefab so Unity AssetDatabase imports it cleanly, and instantiate both Rune Compass and Arcane Detector test pickups into the Gameplay scene near Sanctuary 1.

Refs: US-76, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab
Project Grimhold/Assets/Scenes/Gameplay.unity
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab
- message-term → Inventory &amp; Loot: pickup
- path → UI: Project Grimhold/Assets/Placeholder/UI/LootIconMissing.png.meta
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 6ac67a53ae11b03e80d008fac0a789e38bd6e20b

[6ac67a53ae11b03e80d008fac0a789e38bd6e20b](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6ac67a53ae11b03e80d008fac0a789e38bd6e20b)

Date: 2026-11-02T16:45:57-03:00 · Classification: Features (explicit) · Systems: Abilities, Inventory & Loot, Audio, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): add utility test pickups and passive debug overlay
```

#### Original body

```text
- Polish Void Bell chime presentation with world-space pulse ring,
  domain reload safety, and standalone audio fallback.
- Provide F10 debug overlay and console logging for passive treasure
  modifiers and container cooldown retention.
- Create Rune Compass and Arcane Detector pickup prefabs, wire them to
  RaidLootTestSpawner, and place them in Gameplay scene.

Refs: US-76, TASK-264, TASK-265, #656, #657, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_ArcaneDetector.prefab.meta
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab
Project Grimhold/Assets/Prefabs/Loot/LootPickup_RuneCompass.prefab.meta
Project Grimhold/Assets/Scenes/Gameplay.unity
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs.meta
Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
```

#### Classification evidence

- type → Features: feat
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs
- path → Abilities: Project Grimhold/Assets/Scripts/Gameplay/Debugging/PassiveAndUtilityDebugOverlay.cs.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_ArcaneDetector.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab
- path → Inventory &amp; Loot: Project Grimhold/Assets/Prefabs/Loot/LootPickup\_RuneCompass.prefab.meta
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → Audio: audio
- path → Build &amp; Tooling: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Tests: Project Grimhold/Assets/Editor/Gameplay/RaidLootTestSpawnerEditor.cs
- path → Tests: Project Grimhold/Assets/Scripts/Gameplay/Debugging/RaidLootTestSpawner.cs
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a

[56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/56ddcb8b3519d7b2645ad0119d1a773ab3f0bf5a)

Date: 2026-11-02T15:49:10-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): resolve player cooldown owner over corpse container
```

#### Original body

```text
NetworkPlayer carries both a dormant corpse NetworkLootContainer and PlayerConsumableNetworkController on the same root GameObject. Once NetworkLootContainer implemented IUtilityCooldownProvider and IUtilityCooldownReceiver, generic sibling interface lookups resolved the co-located corpse container instead of the live player controller during drops, pickups, container transfers, and death conversion.

- Prefer PlayerConsumableNetworkController on sibling endpoint lookups in LootTransferTransaction, PlayerLootDropNetworkController, NetworkLootPickup, and PlayerCorpseGenerationController while preserving direct container-to-container/player cooldown transfers.
- Spawn concrete GreenSlime melee enemy prefab and cast NetworkBool in UtilityExpeditionPlayModeTests.
- Record live Unity MCP EditMode (15/15) and PlayMode (8/8) verification in the US-76 ODD tracker.

Refs: US-76, TASK-264, TASK-265
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootPickup.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerLootDropNetworkController.cs
- message-term → Inventory &amp; Loot: pickups
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs

#### Diagnostics

- No classification ambiguity detected.

### 5367bc9dbadb5e262480edd2ca581757a89f9d52

[5367bc9dbadb5e262480edd2ca581757a89f9d52](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/5367bc9dbadb5e262480edd2ca581757a89f9d52)

Date: 2026-11-02T15:14:09-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(loot): gate equipment visual validation and stabilize US-76 tests
```

#### Original body

```text
Gate EquipmentVisualDefinition validation in LootDefinition.TryValidate
to armor categories so non-armor definitions deserialized without sprite
mappings do not invalidate LootDefinitionCatalog, world drops or loot
container initialization, and guard unspawned receivers in passive
treasure evaluation.

- Gate _equipmentVisualDefinition.TryValidate behind IsArmorCategory in
  LootDefinition while preserving strict validation on armor items.
- Guard PlayerInventoryPassiveEffectsController against unspawned
  PlayerLootReceiver instances before reading networked sequence state.
- Restore locked New-Testing budgets in RaidLootOriginWordCountTests and
  document the two container cooldown TickTimer words in
  LootInteractionArchitecture.md.
- Update InventoryPassiveEffectsTests and UtilityExpeditionPlayModeTests
  with productive catalog validation, required motor colliders, deferred
  Render rejection waits, runner input provision and Sanctuary service
  registration.

Player-Fixed: Dropping items in a Raid and opening loot chests or defeated player containers work properly again.
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Loot/LootDefinition.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### e91525252cac0520492f462594d40387463aacb5

[e91525252cac0520492f462594d40387463aacb5](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/e91525252cac0520492f462594d40387463aacb5)

Date: 2026-11-02T13:54:16-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, Networking & Sessions, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(utilities): retain utility cooldowns across container transfers
```

#### Original body

```text
Preserve active Rune Compass and Arcane Detector TickTimer cooldowns
when depositing into or withdrawing from world, enemy, or player corpse
containers so cooldowns cannot be bypassed via container transfers.

Reviewers must verify:
- NetworkLootContainer replicates RuneCompassCooldown and
  ArcaneDetectorCooldown within approved Fusion word budgets and keeps
  Host Migration restore state intact.
- LootTransferTransaction transfers active utility cooldowns between
  source and destination endpoints and clears the source timer once zero
  units remain.
- PlayerCorpseGenerationController transfers active utility cooldowns
  into the defeated player's corpse container upon conversion.

Tests updated:
- UtilityExpeditionRulesTests (container deposit/withdrawal cooldown
  transfer across players)
- UtilityExpeditionPlayModeTests (container deposit/withdrawal retention,
  corpse retention, and treasure passive runtime coverage)
- RaidLootOriginWordCountTests

Docs updated:
- LootInteractionArchitecture.md
- odd/tasks/treasure-passives-and-cooldowns.md

Player-Fixed: Storing a Rune Compass or Arcane Detector in a chest or corpse no longer resets its active cooldown.
Refs: TASK-265, #656, #658
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
Project Grimhold/Assets/Scripts/Player/PlayerCorpseGenerationController.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
odd/tasks/treasure-passives-and-cooldowns.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Abilities: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/LootTransferTransaction.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Gameplay/NetworkLootContainer.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- message-term → Networking &amp; Sessions: Host Migration
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/LootInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/RaidLootOriginWordCountTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Utilities/UtilityExpeditionRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/UtilityExpeditionPlayModeTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 6c6923e8962ce0dce0b0e10e6be973d0b4e25d96

[6c6923e8962ce0dce0b0e10e6be973d0b4e25d96](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/6c6923e8962ce0dce0b0e10e6be973d0b4e25d96)

Date: 2026-11-02T13:53:54-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, UI, World & Visibility, Audio, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(loot): integrate passive treasure effects and chime alerts
```

#### Original body

```text
Complete runtime and local presentation integration for MVP passive
treasures carried in Raid inventory without stacking duplicates, and
revoke active modifiers immediately upon drop, deposit, or death.

Reviewers must verify:
- Pilgrim's Light applies a 1.35x stamina regeneration multiplier.
- Void Bell applies a 1.15x sprint speed multiplier and advances a
  tick-driven 2.0s chime on State Authority while sprinting, alerting
  live IAggroReceiver enemies within 15m and presenting audio plus a
  provisional ring pulse to observers within 15m.
- Wanderer's Eye scales local NearRadius, FarFog distances, and avatar
  Light2D radii by 0.75x without mutating shared ScriptableObjects,
  while blocking the local minimap.

Tests updated:
- InventoryPassiveEffectsTests (stamina regen, 15m aggro deduplication,
  15m chime presentation radius, vision and Light2D scaling, death reset)

Docs updated:
- VisibilityArchitecture.md

Player-Added: Pilgrim's Light, Void Bell, and Wanderer's Eye now apply their passive effects while carried in raids.
Refs: TASK-264, #656, #657
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Audio/AudioManager.cs
Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionFogPresenter.cs
Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionQuery.cs
Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
Project Grimhold/Assets/Scripts/Player/PlayerCharacter.cs
Project Grimhold/Assets/Scripts/Player/PlayerStaminaNetworkController.cs
Project Grimhold/Assets/Scripts/Player/Presentation/LocalVisionSource.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Interaction/PlayerInventoryPassiveEffectsController.cs
- path → Inventory &amp; Loot: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- scope → Inventory &amp; Loot: loot
- message-term → Inventory &amp; Loot: loot
- message-term → UI: minimap
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionFogPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Gameplay/Visibility/VisionQuery.cs
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Audio: Project Grimhold/Assets/Scripts/Audio/AudioManager.cs
- message-term → Audio: audio
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Loot/InventoryPassiveEffectsTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 177ac662f35edbb3712ecec5dcc239678c023f31

[177ac662f35edbb3712ecec5dcc239678c023f31](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/177ac662f35edbb3712ecec5dcc239678c023f31)

Date: 2026-11-03T14:54:39-03:00 · Classification: Bug Fixes (explicit) · Systems: Abilities, Inventory & Loot, UI, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): finalize north-only flow and add inventory drafts
```

#### Original body

```text
Use the approved edge-on artwork and a single north opening animation.
Remove obsolete south clips and states while retaining source sheets.

Allow closing from either side at boundary contact, but reject real
doorway occupancy. Share availability with prompt selection so blocked
closing is not advertised. Preserve replicated state and timing.

Add three standalone Charcoal inventory layout prototypes, their
tradeoff and provenance documentation, and a design manifest. Record the
game-asset, game UI/UX and uGUI skill sources in the lock file. These
prototypes are illustrative and are not integrated with gameplay.

Validation:
- 113 relevant Unity EditMode tests passed.
- 31 door and vision PlayMode tests passed.
- Prototype JSON parsed; three inline scripts passed node --check.
No fresh browser, Host/Client, migration or player build validation.

Player-Improved: Doors now open consistently toward the north.
Player-Fixed: Doors close from either side when the doorway is clear.
Player-Improved: The close prompt appears only when closing is available.
```

#### Changed paths

```text
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim.meta
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
skills-lock.json
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Abilities: skills-lock.json
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: inventory
- message-term → UI: UI
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Art &amp; Animation: animation
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: skills-lock.json
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Documentation: documentation
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- message-term → Tests: tests

#### Diagnostics

- No classification ambiguity detected.

### 136f0f48c582b77881ba017a5ce0d7db0aa485da

[136f0f48c582b77881ba017a5ce0d7db0aa485da](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/136f0f48c582b77881ba017a5ce0d7db0aa485da)

Date: 2026-11-03T11:50:53-03:00 · Classification: Maintenance (explicit) · Systems: Abilities, UI, Art & Animation, Build & Tooling

Flags: secondary

#### Original subject

```text
chore: add game UI/UX and Unity uGUI skills
```

#### Original body

```text

```

#### Changed paths

```text
.agents/skills/create-game-assets/SKILL.md
.agents/skills/create-game-assets/agents/openai.yaml
.agents/skills/create-game-assets/assets/art-direction-brief.md
.agents/skills/create-game-assets/assets/asset-manifest.json
.agents/skills/create-game-assets/references/art-direction.md
.agents/skills/create-game-assets/references/provenance.md
.agents/skills/create-game-assets/references/raster-pipeline.md
.agents/skills/create-game-assets/references/three-d-pipeline.md
.agents/skills/create-game-assets/scripts/asset_report.py
.agents/skills/create-game-assets/scripts/build_preview_sheet.py
.agents/skills/create-game-assets/scripts/requirements.txt
.agents/skills/game-ui-ux/SKILL.md
.agents/skills/game-ui-ux/references/layout-and-flow.md
.agents/skills/ui-ugui/SKILL.md
.agents/skills/ui-ugui/references/scrollview-setup.md
```

#### Classification evidence

- type → Maintenance: chore
- path → Abilities: .agents/skills/create-game-assets/SKILL.md
- path → Abilities: .agents/skills/create-game-assets/agents/openai.yaml
- path → Abilities: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Abilities: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Abilities: .agents/skills/create-game-assets/references/art-direction.md
- path → Abilities: .agents/skills/create-game-assets/references/provenance.md
- path → Abilities: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Abilities: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Abilities: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Abilities: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Abilities: .agents/skills/game-ui-ux/SKILL.md
- path → Abilities: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Abilities: .agents/skills/ui-ugui/SKILL.md
- path → Abilities: .agents/skills/ui-ugui/references/scrollview-setup.md
- path → UI: .agents/skills/game-ui-ux/SKILL.md
- path → UI: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → UI: .agents/skills/ui-ugui/SKILL.md
- path → UI: .agents/skills/ui-ugui/references/scrollview-setup.md
- message-term → UI: UI
- path → Art &amp; Animation: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Art &amp; Animation: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/SKILL.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/agents/openai.yaml
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/art-direction-brief.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/assets/asset-manifest.json
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/art-direction.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/provenance.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/raster-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/references/three-d-pipeline.md
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/asset\_report.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/build\_preview\_sheet.py
- path → Build &amp; Tooling: .agents/skills/create-game-assets/scripts/requirements.txt
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/SKILL.md
- path → Build &amp; Tooling: .agents/skills/game-ui-ux/references/layout-and-flow.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/SKILL.md
- path → Build &amp; Tooling: .agents/skills/ui-ugui/references/scrollview-setup.md

#### Diagnostics

- No classification ambiguity detected.

### 0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7

[0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0b4131a1a38043f4da8f8f5a3cd685a3d8ad41d7)

Date: 2026-11-02T22:33:57-03:00 · Classification: Features (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Documentation

Flags: primary or unclassified

#### Original subject

```text
feat(doors): use hand-drawn south swing frames
```

#### Original body

```text
Replaces the generated intermediate frames of the south-opening door
with hand-drawn ones: the leaf now swings smoothly toward the camera
from a fixed hinge instead of showing the column-shift steps. The
closed and fully open frames, sprite rects, pivots and clips are
unchanged, so the existing South clips pick the new frames up by
sprite reference.

Docs: PlayerInteractionArchitecture.md door sheet description.

Player-Improved: Doors opening toward you now swing smoothly from their hinge.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Features: feat
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- message-term → Art &amp; Animation: sprite
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md

#### Diagnostics

- No classification ambiguity detected.

### 3391260959169b5d6dc9d80ad819e28124a19bbc

[3391260959169b5d6dc9d80ad819e28124a19bbc](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/3391260959169b5d6dc9d80ad819e28124a19bbc)

Date: 2026-11-02T21:32:49-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): hang the fully open south door from its hinge
```

#### Original body

```text
The previous south frame flipped the edge-on strip within its own rows,
so the leaf stayed north of the wall line with only its tip turned. The
south frame 0 is now the north edge-on strip mirrored around the hinge
row at the wall base: the north leaf rises from the hinge and the south
leaf hangs from it toward the camera with the same length.

Tests:
- DungeonDoorAnimationTests: south open frame is the north strip
  mirrored around the wall-base hinge row.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opened from the north now hang open toward the south from their hinge.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 93bfc02f7feb11599245c4e80e24f61ccf444ef1

[93bfc02f7feb11599245c4e80e24f61ccf444ef1](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/93bfc02f7feb11599245c4e80e24f61ccf444ef1)

Date: 2026-11-02T21:12:33-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): point the fully open south door toward the south
```

#### Original body

```text
The fully open south frame was identical to the north one, so doors
looked like they always opened to the same side. The south sheet's
frame 0 is now the north edge-on strip flipped vertically within its
own rows: the tip points south while the strip keeps the same
footprint on the wall base and the same hinge column.

Tests:
- DungeonDoorAnimationTests: south open frame equals the vertically
  mirrored north strip with an identical footprint.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opened from the north now visibly end open toward the south.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 60008f1041b10bfb05cdf60267f852c2cbb87a48

[60008f1041b10bfb05cdf60267f852c2cbb87a48](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/60008f1041b10bfb05cdf60267f852c2cbb87a48)

Date: 2026-11-02T15:34:47-03:00 · Classification: Bug Fixes (explicit) · Systems: Inventory & Loot, World & Visibility, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
fix(doors): keep the south-open door hinged at the wall
```

#### Original body

```text
The fully open south frame shifted the edge-on strip below the wall
base, so the door looked like it slid down instead of turning on its
hinge. The south sheet now reuses the north edge-on strip for frame 0,
keeping the hinge on the wall base in both directions; the opening
direction still reads from the intermediate frames and the sorting
order.

Tests:
- DungeonDoorAnimationTests: south open frame matches the north strip
  and stays anchored at the wall base.

Docs: PlayerInteractionArchitecture.md provisional south sheet note.

Player-Fixed: Doors opening toward you now swing from their hinge instead of sliding down.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Bug Fixes: fix
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 320c1cd3027e190dfc81eed063e67be512638444

[320c1cd3027e190dfc81eed063e67be512638444](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/320c1cd3027e190dfc81eed063e67be512638444)

Date: 2026-11-02T15:18:07-03:00 · Classification: Features (explicit) · Systems: UI, World & Visibility, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(level): place a test door in the Gameplay dungeon
```

#### Original body

```text
Places one DungeonDoor scene instance in the southern passage of the
southwest spawn room so doors can be validated in a real raid. The
passage was 3 u wide, so two Walls cells narrow only that opening to
2 u for the native-size door; final door placement belongs to Level
Design.

- Gameplay.unity: Dungeon_Graybox/Walls cells (-12,-16) and (-11,-16);
  DungeonDoor at (-9.02, -24.86). The large diff is tilemap and
  collider cache reindexing.
- DungeonGrayboxMinimapLayout.asset re-baked from the updated walls.
- odd/tasks/dungeon-doors.md: ODD execution tracker for TASK-276.

Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
Project Grimhold/Assets/Scenes/Gameplay.unity
odd/tasks/dungeon-doors.md
```

#### Classification evidence

- type → Features: feat
- path → UI: Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
- path → World &amp; Visibility: Project Grimhold/Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset
- path → World &amp; Visibility: odd/tasks/dungeon-doors.md
- message-term → World &amp; Visibility: dungeon
- message-term → Tests: test

#### Diagnostics

- No classification ambiguity detected.

### 74792e41ac23c03c157d8a5a40256e9761a4a316

[74792e41ac23c03c157d8a5a40256e9761a4a316](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/74792e41ac23c03c157d8a5a40256e9761a4a316)

Date: 2026-11-02T15:17:57-03:00 · Classification: Features (explicit) · Systems: World & Visibility, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(rendering): Y-sort characters against doors
```

#### Original body

```text
Characters and doors now sort by world Y instead of characters always
drawing over the door: a character north of a door base is drawn behind
it and one south of it in front.

- Renderer2D transparency sort mode: Custom Axis (0, 1, 0).
- Root SortingGroup (Characters/0, sortAtRoot off) on NetworkPlayer and
  NetworkEnemy (inherited by enemy variants); their particle children
  keep their own layers through sortAtRoot groups.
- TrainingDummy moves to Characters/0 with pivot sorting.
- The door SortAnchor is raised by the player root-to-feet offset
  (0.6875 u). Enemies with other root offsets keep small approximate
  bands (base 0.6875 u, ranged 0.25 u, slimes 0.1875 u). Walls are
  unchanged (Default/1).

Tests:
- DungeonDoorSortingTests: renderer sort axis, character groups,
  door anchor calibration against the measured player feet, door
  geometry and two-sided trigger reach.

Docs: VisibilityArchitecture.md rendering section (also corrects the
walls' sorting layer to Default/1).

Player-Improved: Characters now appear behind or in front of doors depending on where they stand.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Prefabs/Enemies/NetworkEnemy.prefab
Project Grimhold/Assets/Prefabs/NetworkPlayer.prefab
Project Grimhold/Assets/Prefabs/Sandbox/TrainingDummy.prefab
Project Grimhold/Assets/Settings/Renderer2D.asset
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
```

#### Classification evidence

- type → Features: feat
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/VisibilityArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Presentation/DungeonDoorSortingTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13

[9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9187b3537b3e31bdb1a31cfbbc3ff6197b0f5d13)

Date: 2026-11-02T15:17:47-03:00 · Classification: Features (explicit) · Systems: Combat, Movement, Inventory & Loot, World & Visibility, Audio, Art & Animation, Build & Tooling, Documentation, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(doors): add networked dungeon doors
```

#### Original body

```text
Adds the TASK-276 Dungeon door: a State Authority NetworkDoor that
players open and close with the contextual interaction, animated from
the delivered Catacumbas door sheet and blocking movement, projectiles,
vision and enemy navigation while closed.

- NetworkDoor (IInteractable): replicated State (Closed, Opening, Open,
  Closing), TransitionTimer (0.4 s) and OpenDirection. Only stable
  states accept interaction; presses during transitions are ignored and
  closing is rejected while a Character/Enemies body overlaps the
  doorway. The door opens away from the interactor (interactor north of
  the door -> South). Restore spawns keep the copied state.
- One non-trigger blocker on WorldCollision (2.125 x 1.0 u, same depth
  as the adjacent walls) drives movement, projectile and vision
  occlusion and refreshes the pathfinding region on State Authority.
  Only the dedicated Interactable trigger is registered in the
  EntityRegistry.
- Prompts "[F] Abrir puerta" / "[F] Cerrar puerta" through a door branch
  in LocalInteractionCandidateSource.
- DungeonDoorPresenter maps the replicated state to DungeonDoor.controller
  (7 sprite clips, transitions 0.4 s) in Render only, adopting the timer
  progress on late join/restore, and plays placeholder open/close audio.
- Door sprites imported at PPU 16, Point, uncompressed, shared pivot at
  the door base. Door-Open-South-Sheet.png is a provisional south-opening
  variant derived from the north sheet (hinge anchored at the wall) for
  Art to retouch.
- DungeonDoor.prefab: NetworkObject, Blocker, InteractionTrigger and a
  SortAnchor SortingGroup raised by the player root-to-feet offset.

Tests:
- DungeonDoorStateRulesTests, DungeonDoorOpenDirectionRulesTests,
  DungeonDoorAnimationTests (EditMode).
- DungeonDoorPlayModeTests: passage, projectile and vision occlusion,
  transitions, occupied-doorway rejection, navigation refresh,
  direction and Animator states.

Docs: PlayerInteractionArchitecture.md TASK-276 section.

Player-Added: Dungeon doors can be opened and closed with F; closed doors block movement, attacks and sight, and open away from you.
Refs: TASK-276
```

#### Changed paths

```text
Project Grimhold/Assets/Animations/Doors.meta
Project Grimhold/Assets/Animations/Doors/Door_Closed.anim
Project Grimhold/Assets/Animations/Doors/Door_Closed.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Closing_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Closing_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Open_South.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_North.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_North.anim.meta
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim
Project Grimhold/Assets/Animations/Doors/Door_Opening_South.anim.meta
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
Project Grimhold/Assets/Art/Props/Doors.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png.meta
Project Grimhold/Assets/Art/Props/Doors/Door-Side.png
Project Grimhold/Assets/Art/Props/Doors/Door-Side.png.meta
Project Grimhold/Assets/Prefabs/Doors.meta
Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab
Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab.meta
Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
Project Grimhold/Assets/Scripts/Scenario/Doors.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs.meta
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs
Project Grimhold/Assets/Scripts/Scenario/Doors/NetworkDoor.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
```

#### Classification evidence

- type → Features: feat
- message-term → Combat: projectiles
- message-term → Movement: movement
- path → Inventory &amp; Loot: Project Grimhold/Assets/Scripts/Player/Presentation/LocalInteractionCandidateSource.cs
- path → Inventory &amp; Loot: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- message-term → Inventory &amp; Loot: interaction
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → World &amp; Visibility: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab
- path → World &amp; Visibility: Project Grimhold/Assets/Prefabs/Doors/DungeonDoor.prefab.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorOpenDirection.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorPresenter.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorState.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Scripts/Scenario/Doors/DungeonDoorStateRules.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → World &amp; Visibility: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
- message-term → World &amp; Visibility: dungeon
- message-term → Audio: audio
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closed.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closed.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Closing\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Open\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_North.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_North.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/Door\_Opening\_South.anim.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller
- path → Art &amp; Animation: Project Grimhold/Assets/Animations/Doors/DungeonDoor.controller.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-Sheet.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Open-South-Sheet.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Side.png
- path → Art &amp; Animation: Project Grimhold/Assets/Art/Props/Doors/Door-Side.png.meta
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Art &amp; Animation: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- message-term → Art &amp; Animation: sprite
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → Documentation: Project Grimhold/Docs/Architecture/PlayerInteractionArchitecture.md
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorAnimationTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorOpenDirectionRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Scenario/DungeonDoorStateRulesTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs
- path → Tests: Project Grimhold/Assets/Tests/PlayMode/DungeonDoorPlayModeTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

### 31617bcabdf0c089f83dee1a1af1f902447dce00

[31617bcabdf0c089f83dee1a1af1f902447dce00](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/31617bcabdf0c089f83dee1a1af1f902447dce00)

Date: 2026-11-02T15:17:32-03:00 · Classification: Features (explicit) · Systems: Movement, Build & Tooling, Tests

Flags: primary or unclassified

#### Original subject

```text
feat(pathfinding): refresh navigation grid regions at runtime
```

#### Original body

```text
Runtime obstacles such as doors toggle their colliders after the grid
is built, so enemies kept pathing through closed doorways. The grid can
now re-evaluate only the nodes whose inflated detection box overlaps a
given world region, reusing the build-time obstacle query.

- PathfindingGrid.RefreshRegion(Bounds) is ignored before Build and
  leaves nodes outside the region untouched.
- PathNode.IsWalkable doc updated: Build or RefreshRegion only.

Tests:
- PathfindingGridRefreshRegionTests (disable/re-enable blocker,
  outside-region nodes untouched, ignored before build).
```

#### Changed paths

```text
Project Grimhold/Assets/Scripts/Pathfinding/PathNode.cs
Project Grimhold/Assets/Scripts/Pathfinding/PathfindingGrid.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
```

#### Classification evidence

- type → Features: feat
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Movement: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Build &amp; Tooling: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs
- path → Tests: Project Grimhold/Assets/Tests/EditMode/Editor/Movement/PathfindingGridRefreshRegionTests.cs.meta
- message-term → Tests: Tests

#### Diagnostics

- No classification ambiguity detected.

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


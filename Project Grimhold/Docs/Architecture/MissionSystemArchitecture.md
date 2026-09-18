# Mission System Architecture

## 1. Purpose

This document outlines the architecture for the Mission System in Project Grimhold. It follows the technical design laid out in the Mission System Implementation Plan and fulfills TASK-160. The architecture is designed to integrate seamlessly with existing systems (like `ProgressionArchitecture.md` and `LocalPlayerPersistenceArchitecture.md`) without introducing a parallel gameplay framework or a global event bus.

## 2. Separation of Layers

Following the pattern used in `Progression/` and `Scenario/Extraction/`, the system is strictly divided into three layers:

- **Pure C# Domain:** Defines the concept of a Mission, its static catalog, the lifecycle state machine, and the progress engine for objectives/phases. This layer has no dependencies on `UnityEngine` or Fusion and is 100% testable in EditMode.
- **Persistence Boundary:** Extension of `LocalProfileStore` / `ILocalProfileRepository`. Mission state is added to the local profile, sharing the same transactional pattern as Stash, Loadout, and Progression.
- **Network Boundary (Host/Client in Raid):** Thin `NetworkBehaviour` adapters that translate authoritative facts (e.g., a confirmed kill or chest interaction) into Mission contribution events. These adapters contain no mission rules themselves.
- **Network Boundary (Shared Mode in Town):** An interactable NPC (following the `TownStashNpcInteractable` pattern). The `NetworkBehaviour` validates interaction, while accepting or claiming a Mission is a transaction against the `LocalProfileStore`.

## 3. Mission State Ownership

Consistent with `LocalPlayerPersistenceArchitecture.md`, the local profile is the only persistent store for the MVP.

- **Static Definitions:** The static catalog of Missions (definitions) resides in ScriptableObjects (content), similar to `LootDefinitionCatalog`. It is not part of the profile.
- **Mutable State:** Mission progress (active missions, current phase, objective progress, pending claims, weekly rotation data) is added to `LocalProfileSnapshot` as a new section with its own `SchemaVersion` bump. This adheres to the explicit exclusion of physical backend persistence for this milestone.

## 4. Flow of Progress (Raid to Local Profile)

To report progress from a Host/Client Raid to the process of the owning client, the system reuses the established pattern for Kill XP (`ProgressionArchitecture.md`):

1. **State Authority Resolves Fact:** A fact (e.g., fatal elimination, valid chest opening) occurs under State Authority.
2. **Network Replication:** The State Authority delivers this fact synchronously to a `[Networked]` component (e.g., `PlayerMissionContributionLedger`) co-located on the owning player's `NetworkRaidParticipant`.
3. **Input Authority Evaluation:** The Input Authority (the process of the owning player) reads this replicated event, evaluates it locally against its active missions using the pure domain engine, and commits the result to its own `LocalProfileStore`.
4. **Co-op Contribution:** When objective rules allow (e.g., shared kill progress in a Duo), the producer delivers the event to the ledgers of all eligible participants present in the same Zone.

This approach guarantees that the engine does not decide if the event happened (it receives it already resolved) and prevents the need for a global authoritative mission service in Fusion.

## 5. Zone Identifiers

Currently, Project Grimhold only possesses specific zone identifiers for Extraction (`ExtractionZone`), and lacks a generic Zone ID for combat or exploration.

- The objective engine supports a Zone condition in its data contract (an optional string ID).
- Until an authoritative source for generic Zones exists, families like PvE Elimination or Interaction will not require a Zone condition.
- Joint presence evaluation in Duo will use the most specific available identifier (e.g., `RaidGenerationId` / current instance) as an approximation for "same Dungeon", with this limitation explicitly documented.

## 6. Duplication Prevention and Claim Idempotence

- **Contribution Deduplication:** A mechanism (such as sequence numbers or specific event IDs) ensures that the same authoritative event cannot be applied twice by the Input Authority to its local progress.
- **Claim Idempotence:** The Claim operation utilizes the same watermark/receipt pattern as consolidated XP in `LocalPlayerPersistenceArchitecture.md`. A repeated claim request with the same parameters will not duplicate rewards.

## 7. Integration Contracts

Other systems are responsible for confirming actions, not the Mission system:
- **Combat:** Confirms death -> Missions evaluates target.
- **Interaction:** Confirms chest open -> Missions evaluates objective.
- **Extraction:** Confirms extraction -> Missions evaluates objective.

Systems providing events (e.g., `DamageResolver`, `NetworkLootContainerInteractable`) are responsible for identifying the player, the target, and providing necessary context via a `MissionContributionEvent`.

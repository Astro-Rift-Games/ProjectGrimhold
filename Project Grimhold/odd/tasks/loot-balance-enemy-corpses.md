# Loot balance and enemy corpse contents

## Objective
Rebalance the global playtest loot table and initialize enemy corpse loot authoritatively before death, reusing the existing loot generation and container contracts.

## Problem and why
`NetworkEnemy.prefab` has an empty initial-content list. The registered enemy spawn path does not supply generated contents; death only exposes the already initialized container. The global table also has the wrong minimum stack count, old weights, and omits the existing Great Hammer definition. This blocks the requested loot playtest.

## Scope and constraints
- Work only on `New-Testing`; baseline `d6814d91242019a531a4514f5bc32d5f03be106a`, clean at exploration.
- Reuse `LootContainerContentTable`, its validation and roller, seed rules, catalog, pre-spawn `NetworkLootContainer` override, and `ContainerRaidLootOriginState`.
- Add a separate enemy content table; no rarity, family-specific tables, new item definitions, gameplay stat changes, corpse prefab, loot redesign, or unrelated cleanup.
- Preserve State Authority generation, existing Dungeon origin and first-acquisition behavior, current Luck rules, one same enemy NetworkObject/container, unavailable-while-alive lifecycle, and no chest-open reward.
- Inspect and verify all four enemy prefabs registered by `Systems.prefab`: Green Slime, NetworkEnemyRanged, Blue Slime, and Red Slime. Unity MCP is unavailable in this runtime; static serialized YAML can be inspected, but Editor/runtime wiring cannot be certified unless a viable Unity validation route is available.
- TDD: strict mode is off for this feature by explicit user direction; use ordinary focused functional checks.
- Candidate runner: Unity 6000.5.1f1 batchmode with Unity Test Framework 1.7.0; run only after confirming the Editor is not open and the command is safe for this checkout. Receipt-driven review was reported off by exploration, separate from TDD.
- Delivery constraint: user explicitly prohibited commits and branch creation unless requested. Stay on `New-Testing`; do not commit, create branches, push, or open a PR.

## Tasks and acceptance

### LBC-1 — Rebalance the default loot table
- Route: delegated direct. Trigger evidence: mapped 4+ code/config files and a serialized asset plus tests; the main implementation edits more than one non-trivial file.
- [x] Configure the serialized global table values, add the existing Great Hammer definition exactly once, and set all armor entries to weight 2/amount 1 (reviewed statically).
- [ ] Run the focused table/catalog EditMode tests; test assertions are updated for requested values and existing catalog identities.
- Acceptance: all values match the user request; definitions resolve through the existing catalog; no duplicate item definitions or out-of-scope stat changes.
- Checks: focused Loot table/catalog EditMode tests; serialized diff review.

### LBC-2 — Generate and preserve enemy corpse loot
- Route: delegated direct. Trigger evidence: spawn/container/seed/authority behavior spans multiple non-trivial types, assets, and tests; broad preparation required mapping.
- [x] Add a separate serialized Enemy Loot table with the requested contents and bounds (reviewed statically).
- [x] Implement State Authority validation/roll through the existing pipeline and pre-spawn content override; use the current Luck calculation, Dungeon provenance/first-acquisition path, and container capacity validation.
- [x] Configure the base enemy prefab and statically verify the four registered production prefabs inherit its config; preserve the existing interactable/no-opening-reward setup and unavailable initial state; no corpse prefab.
- [ ] Run focused EditMode and PlayMode tests updated for pre-death initialization, death exposure, same container/content on reopen, and table validation. Multi-peer replication and Host Migration remain pending runtime verification.
- Acceptance: each enemy has valid precomputed loot before it can die; death reveals that same container and content; clients observe authoritative state; normal transfers remain unchanged.
- Checks: focused EditMode and PlayMode loot/enemy tests; serialized prefab diff review; compile.

### LBC-3 — Align documentation and close verification
- Route: inline if only one documentation file requires a mechanical correction; otherwise delegated direct. Trigger evidence: implementation must be compared with both Loot and Enemy architecture documents, but no broad documentation expansion is intended.
- [x] Review `LootInteractionArchitecture.md` and `EnemyCombatArchitecture.md`. The existing enemy architecture already documents the same NetworkObject and pre-rolled contents accurately; no documentation edit is warranted.
- [ ] Complete validation and final full-diff audit after Unity compilation and focused test results are available.
- Acceptance: architecture describes the implemented same-NetworkObject pre-generated loot behavior; no future rarity systems documented.
- Checks: relevant automated Loot/Enemy tests, Unity compilation, full diff/serialized asset audit.

## Progress and evidence
- Exploration confirmed the main diagnosis and current enemy registry contains four prefabs. `EnemyCharacter.HandleDeath()` exposes its same co-located initialized container; current enemy spawn supplies no content override. Architecture already describes retaining pre-rolled loot.
- Implementation is complete locally for LBC-1 and LBC-2; files are changed but not committed. LBC-3 documentation review found no inaccurate contract to edit.
- Static evidence: serialized tables each contain 40 unique definition GUIDs with requested stack bounds and `allowEmpty: false`; all five enemy prefab paths resolve to the base config/table; tests cover configured values, same-container death exposure and reopen stability; `git diff --check` passed.
- Compile-error fix: `WeaponCatalogTests.AssertTableEntry` accessed the instance field `_catalog` while declared static (`CS0120`); changed the helper to an instance method. Unity recompilation is still pending.
- Pending checks: Unity compilation, focused EditMode/PlayMode suites, actual Editor import/Inspector and multi-peer/Host-Migration runtime checks. Unity MCP is unavailable and three Unity processes are active, so do not launch a competing batchmode Editor against this checkout.
- Engram mirror: pending. `mem_save` rejected the write because multiple active runtime sessions match this project; no session ID was guessed. Retry/resync when an unambiguous authorized memory write is available.
- No commit or branch was created, per explicit user instruction.

## Next step
After the user can safely run Unity compilation/tests in the active Editor, address any further reported errors. Do not create commits or branches unless the user explicitly asks.

## Relevant files
- `Assets/Scripts/Networking/NetworkSpawnManager.cs` — current enemy spawn path that must supply pre-generated contents.
- `Assets/Scripts/Gameplay/NetworkLootContainer.cs` — pre-spawn override and container initialization contract.
- `Assets/Scripts/Gameplay/EnemyCharacter.cs` — death exposes the existing container.
- `Assets/Prefabs/Systems.prefab` — productive enemy prefab registry.
- `Assets/Prefabs/Enemies/NetworkEnemy.prefab`, `Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab`, and `Assets/Prefabs/Enemies/Slimes/*.prefab` — base and variant container serialization.
- `Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset` — global playtest content table.
- `Docs/Architecture/LootInteractionArchitecture.md` and `Docs/Architecture/EnemyCombatArchitecture.md` — approved technical contracts.

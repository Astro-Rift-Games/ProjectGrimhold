# Defect record: extraction XP eligibility of loot introduced by another team

Status: **open, not scheduled**.

- Found while reviewing `docs/proposal_task281_town_stash_physical_slots.md`. The owner directed that it be registered independently and not fixed inside TASK-281.
- No HacknPlan item exists.
- Nothing has been changed.

## Observed behavior

`RaidLootEligibilityResolver` (`Project Grimhold/Assets/Scripts/Progression/RaidLootEligibilityResolver.cs:61-71`) marks extracted quantities as XP-eligible when their Raid provenance is either:

- `Dungeon`; or
- `Player(p)`, where `p` is not the extractor and belongs to a different initial team (`originParticipantId != extractorId && originTeam != extractorTeam`).

Consequence: a unit that a rival player brought from Town and the extractor later looted (for example from the rival's corpse) is classified as eligible extracted loot. Its Object Value then feeds extraction XP and the Results summary.

The behavior is intentional and approved in the repository:

- `Docs/Architecture/LootInteractionArchitecture.md:237`: "Dungeon quantities and quantities introduced by a different initial team are eligible, while the extractor's own quantities and those introduced by its initial teammates are not."
- `Docs/Architecture/ExtractionArchitecture.md:120-122`: "Dungeon origin plus players of another initial team. Loot that left Town with the player, or came from a teammate, is omitted".
- The test `Tests/EditMode/Editor/Progression/RaidLootEligibilityResolverTests.cs:9` (`Resolve_ClassifiesDungeonOwnTeammateAndEnemyQuantities`) pins it.

## Expected behavior per live Game Design

- **Doc 05 §8 (XP por Loot extraído).**
  - XP comes from "objetos elegibles de origen Dungeon".
  - "Transferir a otro jugador un objeto que ya ingresó desde Pueblo no lo convierte en Loot de origen Dungeon y no debe permitir generar XP de extracción artificialmente."
  - "Dos Unidades de igual ItemDefinition con distinto Origin mantienen Value idéntico, pero sólo la de origen Dungeon actual puede aportar XP de extracción."
  - Risk table: "Transferencia artificial de Valor de Objeto — Validar origen de instancias y XP generada por extracción."
- **Doc 07 §7 (Origen de una Unidad).**
  - "Objeto ingresado desde Pueblo y luego saqueado de un cadáver — Sigue sin ser origen Dungeon".
  - "El origen no se recalcula por propietario, ubicación, Contenedor ni Equipamiento."

Under the Game Design, only `Dungeon` provenance is eligible. Rival-team `Player(p)` provenance is not.

## Classification

- **Kind:** conflict between approved Architecture and live Game Design. It is not an implementation mistake against its own contract.
- **Impact:** extraction XP and the displayed eligible Value can include gear looted from rival players. This opens the farming vector that Doc 05 lists. Persistent data shape is not affected.
- **Independence:** unrelated to the Town/Stash slot model. Decision D2 = O1 (Raid provenance is expedition-relative) keeps `Player(p)` provenance, so this defect stays meaningful under O1.

## Decision needed before any fix

The Game Design owner confirms which rule applies:

1. Doc 05/07 as written: only Dungeon provenance is eligible. Fix the resolver, the test and both architecture sections.
2. Rival-team loot counts as a PvP reward. Doc 05/07 must be updated by their owner, and the architecture stays.

If option 1 is chosen, scope a fix touching:

- `RaidLootEligibilityResolver`;
- its EditMode tests;
- the `ExtractionArchitecture.md` and `LootInteractionArchitecture.md` sections above;
- a check of the Results summary (`ResultsLootKind.Extracted`).

It does not touch TASK-281, TASK-282 or persistence.

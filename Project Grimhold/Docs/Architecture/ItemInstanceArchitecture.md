# Equipment Item Instance Architecture

**Status:** Approved (TASK-283, 2026-11-04).
**Owns:** equipment instance identity, instance data, instance persistence/transport rules and Town
gem incrustation. Values marked *provisional* follow the project rule of placeholder values until
balance; Game Design corrections are a separate final step.

Doc 09 §20 defines the conceptual split: a shared **Template** (`LootDefinition` + its functional
definition) and a unique **Instance** that exists in Inventory, Stash, Equipment, the world,
containers or corpses. This document is the technical contract for that split.

---

## 1. Scope

| In scope | Out of scope |
| --- | --- |
| Every equipment item (Weapon, Shield, Helmet, Armor, Gloves, Boots) is an instance | Instances for Resource, Treasure, Consumable, Utility |
| Weapon modifiers: optional primary/secondary scaling, 0-2 gem sockets | Modifiers on shields or armor pieces |
| Town-only gem incrustation ("Incrustar gema") | Gem removal/extraction, artisan NPCs (GD-25) |
| Raid transport of modifiers, extraction re-issue | Server-side Raid registry, signed host results |
| Buy-back disabled for equipment | Buy-back redesign (Game Design follow-up) |

Training weapons for players entering a Raid without a weapon belong to a separate task; until it
lands, the existing `arming_sword` fallback is issued as a plain instance (§4.4).

## 2. Model

```text
EquipmentInstance
  instanceId   backend-issued opaque id (24 lowercase hex, Mongo ObjectId)   Town/backend only
  lootId       immutable Template reference
  modifiers    WeaponInstanceModifiers (weapons only; absent for shields/armor)
                 primary   { attribute, grade E..S } | none
                 secondary { attribute, grade E..S } | none
                 gemSocketCount 0..2
                 gems[socketCount]  gem lootId | empty
```

- Validation is `WeaponInstanceModifiers.TryValidate(naturalAttribute)`: primary matches the
  Template natural attribute, secondary requires a primary and uses a different attribute, grades
  are E-S, socket count 0-2, and no gem beyond the last socket. An installed gem must be a catalog
  gem (`LootDefinition.GemDefinition != null`).
- An instance always has quantity 1. Equipment entries never merge, stack or split.
- Instance identity has no provenance: Raid origin buckets stay Raid-only
  (`ExtractionArchitecture.md`, "Raid provenance never enters stash or backend storage").
- The instance ID exists only in Town and backend storage. Raid state carries modifiers, not IDs
  (§6), because extraction re-issues IDs (§7).

## 3. Ownership

| Concern | Owner |
| --- | --- |
| Issuing instance IDs | Backend only: purchase, migration, fallback, extraction commit |
| Durable instance data | Backend `Character.inventory` (stash, loadout, preparedEquipment, pendingReservation) |
| Local cache of the aggregate | `LocalProfileStore` / save codec (never an ID issuer) |
| Raid modifiers | Fusion state owned by the existing Raid owners (`PlayerLootReceiver`, equipment controller, pickups, containers, corpses) |
| Gem effect values | Unity `GemDefinition` (`DamageBonus`); backend keeps only the gem lootId set and sell values |

## 4. Backend persistence

### 4.1 Schema v3

`inventory.inventorySchemaVersion = 3` gates the instance format; the existing v1/v2 gate rules
apply unchanged otherwise. Rollout uses a separate cutover flag, `INVENTORY_INSTANCE_PROTOCOL_GATE`
(default false): when true the backend requires inventory protocol header `3` and schema `3`, and
older clients receive the existing upgrade/migration error codes; when false the v2 gate behaves as
today while letting v3 documents pass its schema check. The header is a minimum version: the v2 gate
accepts `2` or `3`. Instance-capable Unity clients always send `3` and select v2 or v3 behavior from
the character's `inventorySchemaVersion`, so they need no protocol discovery before hydration.

Wire representation: `attribute` is a lowercase name (`vitality`, `resistance`, `strength`,
`dexterity`, `intelligence`, `luck`), matching the backend progression convention; `grade` is
`E`..`S`. Unity maps them to its enums.

```text
item            { slot, lootId, amount, instance? }
instance        { instanceId, modifiers? }
modifiers       { primary?: {attribute, grade}, secondary?: {attribute, grade},
                  gemSocketCount, gems: [lootId | null] }
preparedEquipment.<slot>   null | { lootId, instance }      (v2: lootId string)
pendingReservation.items   item[] (instances preserved per entry)
```

- Equipment category ⇔ `instance` present ⇔ `amount == 1`. Validators reject any other shape
  (`INVALID_INVENTORY`).
- `receiveItem` never merges equipment; each unit takes the lowest free slot under the existing
  capacity/overflow rules (loadout 30, stash 60).

### 4.2 Migration v2 → v3

An explicit one-time script, following `scripts/migrate_inventory_slots.js`:

- Each equipment stack with `amount = N` becomes N entries with new IDs: the first keeps the
  original slot, the rest take the lowest free slots, then overflow addresses (`slot >= capacity`,
  reducing-only) when the list is full. Nothing is discarded.
- Each `preparedEquipment` lootId becomes `{ lootId, instance }` with a new ID.
- `pendingReservation` is migrated the same way.
- Stored extraction results stay ID-free (§7): equipment stacks become amount-1 entries and
  prepared-equipment slots become `null | { lootId, modifiers? }`; the commit issues fresh IDs for
  both result items and prepared equipment.
- Plain instances follow §4.4. The script is idempotent per character and saves with
  `revision + 1`, like the v2 migration.

### 4.3 Operations

| Operation | v3 rule |
| --- | --- |
| Move / order | Unchanged addressing; equipment moves as one entry with its instance |
| Split | Equipment rejects `409 INSTANCE_NOT_SPLITTABLE` |
| Equip / unequip | The entry (with its instance) moves between the list and `preparedEquipment`; equip requires `instanceId` |
| Buy | Each purchased equipment unit is a new plain instance |
| Sell | Every equipment instance is sellable, even for 0; price = Template sell value + sell value of each installed gem (*provisional*) |
| Buy-back | Disabled for equipment: sales push no ledger entry; `isBuybackEligible` returns false; a pre-migration equipment entry rejects `409 BUYBACK_NOT_ELIGIBLE` |
| Reservation / admission | Reserved entries keep their instances in `pendingReservation` and the admission snapshot |
| Gem incrustation | §5 |

### 4.4 Plain instances

A plain instance (purchase, migration, fallback) has no gems and no sockets. Its primary scaling is
the grade equal to the Template's legacy `WeaponOffensiveScaling` coefficient; a zero coefficient
means no scaling. All current weapon Templates have coefficient 0, so plain weapons keep their
current damage. Shields and armor have no modifiers.

## 5. Town gem incrustation

Player-facing name: **"Incrustar gema"**; context action **"Incrustar"**. Code uses *InstallGem*.

`POST /character/me/inventory/gems/install`

```text
{ transactionId, weaponList, weaponSlot, instanceId, gemList, gemSlot, gemLootId, expectedRevision }
```

One revision-guarded `findOneAndUpdate` (the `writeSlotLists` pattern):

1. The weapon entry at `weaponList/weaponSlot` (`stash` or `loadout`) matches `instanceId` and is a
   Weapon. Equipped weapons are not addressable (unequip first).
2. The gem entry at `gemList/gemSlot` matches `gemLootId`, which is in the backend gem set.
3. The weapon has at least one empty socket; the gem fills the lowest empty one, permanently.
4. The gem stack decrements by 1 (removed at 0) and the weapon instance is rewritten in the same
   update. Any rejection mutates nothing.

| Rejection | Code |
| --- | --- |
| Not a weapon / instance mismatch | `409 INSTANCE_MISMATCH` |
| Gem missing or not a gem | `409 GEM_NOT_AVAILABLE` |
| Weapon without sockets | `409 GEM_NO_SOCKETS` |
| All sockets occupied | `409 GEM_NO_EMPTY_SOCKET` |
| Revision changed | `409 REVISION_CONFLICT` |

The shared inventory codes also apply: `400 VALIDATION_FAILED`, `409 RESERVATION_PENDING`,
`409 INVENTORY_MIGRATION_REQUIRED` and `409 TRANSACTION_ID_REUSED`.

Replays with the same `transactionId` return the stored outcome (shop receipt pattern). Unity calls
it remote-first: the UI asks for confirmation ("¿Incrustar {gema} en {arma}? Es permanente."), shows
pending state, and applies the returned snapshot; it never mutates locally first. Unity's pure
`WeaponInstanceModifiers.TryInstallGem` mirrors the rules for previews and tests.

## 6. Unity runtime

### 6.1 Local persistence

Save schema **v11**: `ItemData` and prepared equipment entries gain the optional instance block of
§4.1. v10 and older remain readable; a v10 equipment stack decodes as plain instances without IDs
only as a transient cache state, replaced by the next backend hydration. Legacy equipment-ownership
repair never runs on v11. `StashItem` gains an optional `EquipmentInstance`.

### 6.2 Damage

`WeaponInstanceModifiers` becomes the active runtime source for player weapons, replacing the
legacy `WeaponOffensiveScaling` path in the equipment controller and Town statistics:

```text
EffectiveDamage = floor((BaseDamage + GemDamageBonus) * (1 + sum(AttributeValue/100 * Coefficient)))
```

`WeaponDamageCalculator` stays the only implementation. `WeaponGemDamageBonusResolver` sums
installed gems through the loot catalog. The instance tooltip shows the same value.

### 6.3 Raid transport

Raid state carries `WeaponInstanceModifiersNetworkData` per weapon unit, never instance IDs:

- `PlayerLootReceiver`: a modifiers array parallel to `LootInventorySlot`, kept in lockstep by
  `LootInventoryRules`; empty for non-weapon slots.
- Equipment controller: modifiers per weapon hand slot next to the catalog index.
- `NetworkLootPickup`, `NetworkLootContainer` and corpses keep the modifiers of every weapon unit
  they hold. Containers store weapon units individually, not in the catalog-index aggregate.
- Host Migration restores these networked fields with the existing `CopyStateFrom` path.
- Measured network budgets (`RaidLootOriginWordCountTests`) are updated with the new size.

## 7. Extraction and reservation

- The host publishes items as `{ lootId, amount, modifiers? }`; equipment entries have
  `amount = 1`. The backend validates modifiers against the Template rules (§2) and the gem set; it
  does not regenerate seeds. This keeps today's trust model: the host is trusted, and signed results
  remain future work.
- On commit, every equipment entry receives a **new** instance ID. Defeat keeps today's loss clear.
- Dungeon variants (Template + `WeaponInstanceGenerator` roll on the host, *provisional* rules)
  travel by the same path.

## 8. Superseded statements (applied on approval)

- `PlayerCombatArchitecture.md` §3: instances "are not stored in LootEntry, Equipment or persistence
  until unique item identity is integrated end to end" becomes a reference to this document.
- `LocalPlayerPersistenceArchitecture.md`: item shape, schema v11/v3, migration.
- `CommerceArchitecture.md`: buy-back eligibility (disabled for equipment), instance sell price.
- `ExtractionArchitecture.md` / `LootInteractionArchitecture.md`: modifiers in the result payload,
  ID re-issue on commit, modifiers in pickups/containers/corpses.

## 9. Open points (to confirm during implementation)

- **v10 local cache:** confirm in F2 whether any local-first Town path can mutate equipment before
  backend hydration; if so, v10 equipment must not be usable until hydrated.
- **Gem sell values on the backend:** the backend gem set and gem sell values come from
  `shopCatalog`; a cross-check test keeps them aligned with Unity `GemDefinition` assets.
- **Container representation (F5):** weapon units stored individually may change the container
  network budget; measured before choosing array sizes.

## 10. Delivery order

F1 backend v3 + migration + incrustation endpoint → F2 Unity codec v11 → F3 Town runtime and damage
→ F4 incrustation UI → F5 Raid transport and extraction → F6 Dungeon variants → Game Design
corrections. Work stays on `feat/equipment-item-instances` until F1-F5 are verified.

# Ability System Base Architecture

## Status and scope

This document defines the minimum technical boundary for the future Ability System. It establishes identity, sources of truth, Town preparation, Raid admission, and ownership. It does **not** implement abilities or prescribe detailed execution, cooldown, targeting, runtime Mana, Status Effects, Assist, input, UI, balance, toggles, or summons.

The names used for future roles in this document describe responsibilities, not existing runtime types. Later tasks may choose concrete type names while preserving these boundaries.

## Design constraints

The technical architecture preserves the current MVP rules from Game Design:

- Abilities belong to the character's persistent repertoire, not to an equipped weapon.
- The character has exactly two universal ability slots. The slots are equivalent, independent of Weapon Sets A/B, and each may be empty.
- A prepared build may contain zero, one, or two abilities; the same ability cannot occupy both slots.
- Attribute requirements gate preparation and use. They do not gate acquisition.
- Ability definitions own their configured requirements. The character's confirmed attributes supply the values used to evaluate them.
- Preparation changes occur in Town while the player is eligible to edit the build. The prepared snapshot is frozen for the Raid.

These rules do not decide how an ability targets, executes, spends a resource, enters cooldown, applies a Status Effect, or presents feedback.

## Architectural boundary

```text
Static content                       Persistent character aggregate
Ability identity -> definition       unlocked repertoire
                 -> single catalog   prepared slot 1 / prepared slot 2
          |                                      |
          +-------------- Town validation -------+
                                                 |
                                      Raid admission snapshot
                                      prepared identities + attributes
                                                 |
                                      NetworkRaidParticipant
                                      frozen Raid entitlement
                                                 |
                                      Future ability runtime boundary
                                      authoritative session state
```

The Ability System is a separate domain boundary from the current basic weapon attack flow. It may consume compatible shared gameplay services, but it does not route through `IAttack` or `PlayerCombatNetworkController`.

## Sources of truth

| State kind | Source of truth | Contents | Must not own |
| --- | --- | --- | --- |
| Static | One ability definition catalog | Stable identity and immutable authored configuration, including attribute requirements | Per-character unlocks, preparation, cooldowns, or active effects |
| Persistent | The character aggregate exposed transactionally through `LocalProfileStore` | Unlocked ability identities and the two prepared optional slot values | Raid execution state or network authority |
| Prepared | Projection of the confirmed persistent profile snapshot; not an independent source of truth | The ordered pair of universal slot identities selected for the next Raid | A second repertoire or mutable Raid state |
| Admission | The versioned Town-to-Raid admission payload | The prepared slot identities and the already-confirmed character attributes required to validate them | The full unlocked repertoire or mutable runtime state |
| Raid entitlement | `NetworkRaidParticipant` | The frozen prepared identities and frozen admitted attributes for that participation | Static definitions or Town persistence |
| Raid runtime | A future ability runtime owner associated with the active Raid avatar/participation | Only authoritative state required while abilities operate in the current Raid | Persistent unlocks, Town preparation, or basic weapon attack state |

Presentation may read confirmed state later, but it is never a source of truth and cannot mutate gameplay.

## Static content

### Stable identity

Every ability requires one stable, serializable identity that remains valid across persistence, admission, replication, and content lookup. Identity comparison is ordinal and independent of display name, asset path, localization, or list position.

At the static-definition reference level, stable ability identity is the only value transported across persistence and admission boundaries. Runtime replicated state is a separate concern owned by the future Raid ability runtime. Unity object references and complete ScriptableObject definitions do not cross those boundaries.

### Immutable definition

Each catalog entry resolves one identity to one authored definition. A definition is immutable shared configuration at runtime. It may expose the attribute requirements needed by this foundation; later tasks may add execution-specific configuration only when their owning designs and contracts are implemented.

A definition never stores whether a character unlocked or prepared it, and it never holds mutable cooldown, resource, targeting, or effect state.

### Single catalog

One catalog is the authoritative runtime lookup for all ability definitions. It must reject invalid identities, null definitions, and duplicate identities during validation. Consumers resolve an identity through this catalog rather than maintaining subsystem-specific definition lists.

If later network transport uses compact catalog indices, those indices are session transport details derived deterministically from the same catalog. Stable identities remain the persistence and admission contract.

## Persistent repertoire and preparation

### Unlocked repertoire

The character aggregate owns a set of unlocked ability identities. Unlocking is a persistent transaction. A duplicate unlock is not a second owned copy, and failing an attribute requirement does not remove an unlock.

`LocalProfileStore` remains the transactional application boundary. Ability mutations must build and validate a complete candidate snapshot, persist it through the active repository boundary, and publish the confirmed replacement only after acceptance. UI and Town network presentation do not mutate the repertoire or prepared slots directly.

The current process-local persistence limitations still apply until backend integration exists; this architecture does not invent a second ability save path.

### Two universal prepared slots

The persistent snapshot contains exactly two equivalent optional slot values:

```text
Prepared Ability Slot 1: Ability identity or empty
Prepared Ability Slot 2: Ability identity or empty
```

Slot position identifies which prepared value is transported; neither slot has different gameplay capabilities. A valid prepared snapshot satisfies all of these invariants:

1. Each non-empty identity resolves through the single catalog.
2. Each prepared identity belongs to the unlocked repertoire.
3. The two non-empty identities are different.
4. The confirmed character attributes satisfy every requirement of each prepared definition.
5. Zero, one, and two occupied slots are all valid.

Town owns preparation changes. A respec or other confirmed attribute mutation must revalidate both slots in the same persistent aggregate transaction and clear any now-invalid prepared ability without removing it from the unlocked repertoire. The existing Town Ready/Not Ready policy decides whether the mutation is currently permitted; the Ability System does not create a parallel readiness rule.

## Town-to-Raid flow

```text
Confirmed LocalProfileStore snapshot
  -> validate both prepared slots against repertoire, catalog, and confirmed attributes
  -> encode the two optional stable identities in versioned Raid admission data
  -> validate the admission snapshot against the same catalog and admitted attributes
  -> initialize the NetworkRaidParticipant frozen ability snapshot
  -> future avatar ability runtime binds to that frozen participant state
```

Only the two prepared identities cross admission. The complete unlocked repertoire stays persistent and is not copied into Raid runtime state. The existing admission payload already transports `CharacterAttributeState`; ability validation consumes that same frozen snapshot instead of creating an ability-specific attribute copy.

The admission payload must represent both empty slots explicitly and preserve slot order. Unknown identities, duplicate occupied slots, malformed values, or unmet admitted attribute requirements fail admission; they are not defaulted, substituted, or silently removed. Adding the concrete payload fields and codec version is later implementation work.

The Raid Host never queries another player's `LocalProfileStore`. It accepts only the validated admission snapshot for the frozen cohort.

## Raid ownership and authority

### Frozen participant state

`NetworkRaidParticipant` is the stable Raid `PlayerObject` and owns the admitted ability entitlement for the participation: the two frozen prepared identities plus the existing frozen attributes. This state survives avatar lifecycle changes and cannot be changed by Town profile commits after admission.

The participant does not execute abilities. It exposes the frozen snapshot to the future runtime boundary in the same way that other Raid consumers read admitted character state.

### Future runtime owner

A future ability runtime owner belongs to Raid gameplay composition, separate from `PlayerCombatNetworkController`. Its exact API and internal state are intentionally deferred. The boundary must preserve these rules:

- State Authority is the only writer of authoritative ability state and transitions.
- Input Authority may express intentions but does not commit outcomes.
- Ability selection and static configuration are initialized from the frozen participant snapshot and the single static catalog. Runtime execution may consume explicitly owned Raid services and resources without duplicating their state or ownership.
- Runtime state is never written back into `LocalProfileStore` as an unlock or preparation mutation.
- Configuration that can be resolved from an ability identity is derived locally and is not replicated as duplicated mutable state.
- Presentation observes confirmed/networked outcomes and does not drive simulation.

This foundation does not decide which runtime fields must be networked. Each later ability task must justify only the state required for its implemented behavior, prediction, proxies, and resimulation.

### Host Migration

For a fresh Raid, State Authority initializes the participant's frozen ability snapshot once after admission succeeds. For a Host Migration restore, copied Fusion state is authoritative: fresh initialization must not overwrite restored prepared identities or future ability runtime snapshots.

The replacement Host resolves definitions again through the same catalog, rebinds the restored participant/avatar relationship, and reconstructs only derived local configuration. Any future mutable runtime state that affects gameplay continuity must be part of the appropriate Fusion snapshot before that feature can claim Host Migration support.

Host Migration never reloads Town persistence, re-runs unlock acquisition, re-prepares slots, or changes ability identity from `PlayerRef`, `NetworkId`, arrival order, display data, or asset position.

## Relationship to basic weapon combat

The implemented `IAttack` strategies and `PlayerCombatNetworkController` form the current basic weapon attack flow. Their single active strategy, primary-attack input, weapon cooldown, and attack presentation sequence are not the runtime contract for character abilities.

Future abilities must not be adapted into `IAttack`, installed as the controller's active weapon strategy, or multiplexed through its cooldown and `AttackSequence`. Doing so would merge two independently prepared and independently owned domains into one state slot.

Abilities may reuse existing concrete gameplay services when their semantics match, for example:

- `IDamageResolver` for authoritative damage application;
- `IHealable` for compatible healing outcomes;
- existing movement or knockback contracts when the later ability's rules match those contracts.

Sharing a concrete service does not transfer ownership. The Ability System owns ability validation and runtime state; the damage, healing, movement, or knockback service owns its established operation.

## Failure policy

- Invalid or duplicate catalog content fails validation visibly.
- Persistent snapshots retain stable identities; missing definitions are configuration errors, not permission to invent fallback abilities.
- Rejected Town preparation is atomic and preserves the last confirmed snapshot.
- Invalid admission fails before Raid ability state is initialized.
- Missing runtime composition disables the affected ability boundary explicitly; it must not fall back to the weapon attack controller.
- No source normalizes an invalid prepared ability into a different identity.

## Deferred implementation

The following work belongs to later tasks and is deliberately not designed here:

- concrete execution phases and interruption rules;
- cooldown and resource-spending state;
- targeting and target validation;
- runtime Mana behavior;
- Status Effects, Assist, toggles, summons, and persistent spawned effects;
- ability input, UI, HUD, audio, VFX, and animation;
- balance values and concrete ability content;
- concrete admission codec fields, network properties, runtime components, and prefab wiring.

Those tasks must extend this boundary rather than add a parallel catalog, persistent repertoire, prepared loadout, admission path, participant snapshot, or weapon-controller integration.

## Acceptance traceability

| TASK 417 criterion | Architecture section |
| --- | --- |
| Stable identity, immutable definition, single catalog | Static content |
| Unlocked repertoire | Persistent repertoire and preparation |
| Exactly two optional, equivalent universal slots | Two universal prepared slots |
| Attribute requirements | Design constraints; Two universal prepared slots |
| Town-to-Raid transport | Town-to-Raid flow |
| Raid ownership | Raid ownership and authority |
| Static/persistent/prepared/runtime separation | Sources of truth |
| State Authority and Host Migration rules | Raid ownership and authority |
| `LocalProfileStore`, admission, and `NetworkRaidParticipant` relationship | Sources of truth; Town-to-Raid flow; Frozen participant state |
| Basic weapon combat boundary | Relationship to basic weapon combat |
| No dependency on targeting or Mana decisions | Status and scope; Deferred implementation |
| No abstractions outside the MVP | Architectural boundary; Deferred implementation |

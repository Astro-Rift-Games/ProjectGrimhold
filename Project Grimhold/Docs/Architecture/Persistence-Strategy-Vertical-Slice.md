# Persistence Strategy for the Vertical Slice

## 1. Current state

The current Town-Raid-Town flow separates remote identity, process-local character state and Raid
simulation:

- **Remote identity and authentication**: the existing backend authenticates username/password,
  returns a JWT and exposes the authenticated Character. Its remote `CharacterId` is adopted by
  Unity as `ProfileId`. `AccountId` remains backend-internal. The exact flow is defined in
  `RemoteIdentityAndAuthArchitecture.md`.
- **Character aggregate**: `LocalProfileStore` uses `LocalProfileRepository` and `LocalProfileFileStore`
  with a per-ProfileId JSON cache (current schema 8, supported older versions readable). The aggregate
  contains Currency, Stash, Loadout, prepared Equipment, reservations, extraction/shop/progression
  receipts and watermarks, Level, current Experience, and `CharacterAttributeState` including its
  available attribute points.
- **Process lifetime**: `ApplicationStashContext` is `DontDestroyOnLoad`, so the aggregate survives
  scene changes and `NetworkRunner` replacement while the application remains open.
- **Local persistence**: the file cache preserves local-only and unsent aggregate fields; neither
  files nor PlayerPrefs create a fallback identity or replace failed authentication/backend fetches.
- **Remote profile data**: login fetches Character/profile, inventory and progression. Inventory and
  progression must have equal revisions before backend hydration and local candidate persistence.
  The backend remains authoritative for its fields; cache replay is not backend confirmation.
- **Raid state**: Health, Raid inventory, Equipment runtime state, enemies and extraction progress
  remain temporary authoritative Fusion simulation state. Fusion does not own Stash, Loadout or
  the persistent character aggregate.

A later authenticated login replays the cache and hydrates backend-owned fields. Healthy pending
extraction commits survive hydration and use the existing retry path. Active missions, local
abilities and other unsent local state are not fully reconstructible from backend payloads.
`ProfileCommitted` acknowledges local aggregate acceptance, not backend durability.

Only the successful authenticated login flow explicitly permits confirmed corrupt-cache replacement.
Both inventory and progression must be present, valid and revision-consistent. Repository eligibility
requires confirmed decode failure of readable existing files, with no IO failure or unsupported
future version; missing files alone, configuration and identity errors never qualify. Valid main and
backup loading/repair are unchanged, including the unsupported-main protective block. A fresh
candidate must pass identity/codec validation and the atomic file-write operation before publication.
Write failure stops hydration/login; no temporary Ready state or upfront deletion is allowed. A
successful reset warns that unsent local state cannot be recovered. Synchronous cache IO adds login
latency; a successful atomic-write return is not a filesystem flush guarantee. See
`LocalPlayerPersistenceArchitecture.md` for the recovery admission contract.

## 2. Problem to solve

Stable identity and authentication are already implemented. The missing Vertical Slice boundary is
durable gameplay persistence: the backend must hydrate and confirm the complete character aggregate
that is currently cached locally, so progression and secured inventory survive application
restart without introducing a second identity or treating Fusion snapshots as metagame storage.

The present client-owned local mutations also provide no external validation against a modified
client. The backend integration must become the authoritative durable boundary for those mutations,
while acknowledging that full server-side Raid simulation and advanced anti-cheat remain outside
this slice.

## 3. Target architecture

The existing Node.js, Express and MongoDB backend becomes the sole durable source of truth for the
approved character aggregate. The responsibilities remain separated:

- **Backend** authenticates the account, resolves its Character, hydrates the complete character
  snapshot, validates aggregate mutations, enforces revision/idempotency rules and stores accepted
  state.
- **Unity application layer** uses the JWT in `ApplicationAuthContext`, exposes confirmed local state
  for presentation, submits mutation intentions or authoritative Raid results, and only publishes
  backend-confirmed replacements as durable state.
- **Photon Fusion** remains authoritative for the active Raid simulation and its result snapshots.
  It may transport `ProfileId`, admission data and receipts, but it never becomes the durable owner
  of the character aggregate.

### Durable aggregate scope

The backend snapshot must include the approved state currently represented by the local aggregate:

- the remote `CharacterId`, represented inside Unity as the same `ProfileId`;
- Currency and shop idempotency state;
- Stash and Loadout contents;
- prepared Equipment assignments;
- pending reservation state and the recovery metadata required by its eventual remote contract;
- applied extraction, shop and progression receipts plus their idempotency watermarks;
- Level and current consolidated Experience;
- the complete `CharacterAttributeState`: Vitality, Resistance, Strength, Dexterity, Intelligence,
  Luck and available attribute points.

This scope does not make temporary Raid state persistent. Position, current Health/Stamina, active
Raid inventory before resolution, enemy state, extraction countdowns and other live simulation
values remain outside the backend character aggregate.

### Integration boundary

Backend persistence is asynchronous and must remain outside the synchronous
`ILocalProfileRepository` contract, as established by
`LocalPlayerPersistenceArchitecture.md`. The integration must load the confirmed backend snapshot
after the existing authentication flow and before Town enables profile-dependent actions. Writes
must expose network failure, cancellation, revision conflict and rejection explicitly; none may
generate a local identity or silently fall back to legacy storage.

The exact HTTP DTOs, endpoints, concurrency token and retry protocol belong to the backend contract
and are not invented by this document.

## 4. General flows

### Town load

```text
LoginFlowController authenticates
-> backend returns JWT and CharacterId
-> Unity adopts CharacterId as ProfileId
-> backend returns the complete confirmed character snapshot
-> application profile services publish that snapshot
-> Town enables dependent presentation and actions
```

### Town mutation

```text
Unity submits a typed aggregate mutation with authentication and revision context
-> backend validates identity, ownership, invariants and revision
-> backend commits atomically
-> backend returns the confirmed snapshot/revision
-> Unity replaces its observable local state
```

### Expedition result

```text
Fusion State Authority resolves an immutable participant result/receipt
-> the owning authenticated Unity client submits it to the backend
-> backend validates identity, sequence and idempotency
-> backend applies eligible Loot and Progression exactly once
-> Unity publishes the confirmed character snapshot
-> the Raid receives only the acknowledgement required by its return/cleanup contract
```

Extraction restores accepted Raid loot to the Loadout; it does not transfer it automatically to the
Stash. The same result transaction must preserve the current progression receipt/watermark rules and
attribute-point grants.

## 5. Vertical Slice scope

### In scope

- Reuse of the existing username/password login, JWT and remote `CharacterId`.
- Backend hydration of the complete character aggregate.
- Durable Currency, Stash, Loadout, prepared Equipment, Level, Experience, receipts/watermarks,
  `CharacterAttributeState` and available attribute points.
- Idempotent processing of definitive expedition results, including extraction Loot and consolidated
  progression.
- Explicit rejection and synchronization of invalid mutations, stale revisions and duplicate
  receipts.
- Visible network failure and retry behavior that preserves one confirmed source of truth.

### Out of scope

- Strict server-side validation of every Raid simulation step or advanced anti-cheat.
- Skill trees or progression systems beyond the currently approved Level, Experience and attributes.
- Player economy, trading, auctions, clans, friends or MMR matchmaking.
- Backend reconstruction of a live Raid or backend-owned Host Migration.

The Vertical Slice may trust a result produced by the client-hosted Fusion State Authority, but that
is a technical trust limitation rather than proof that the client is cheat-resistant.

## 6. Dependencies and migration stages

- **Remote identity**: `LoginFlowController`, `ApplicationAuthContext` and
  `LocalProfileProvider` already establish JWT and `CharacterId` before initializing profile
  services. Persistence must consume this context and must not create another identity path.
- **Local character aggregate**: `LocalProfileStore` remains the synchronous transactional domain
  boundary until a separate asynchronous integration publishes server-confirmed snapshots.
- **Town preparation**: backend hydration must complete before Equipment preparation, Loadout
  reservation and attribute assignment are enabled.
- **Raid admission**: the current flow already transports the remote `ProfileId`, reserved Loadout,
  progression baseline and confirmed `CharacterAttributeState`. Backend hydration must precede that
  reservation/admission boundary.
- **Expedition resolution**: extraction and progression receipts must map to backend idempotency and
  revision rules before their acknowledgements can be considered durable.

Recommended stages:

1. Define backend snapshot, mutation, revision and receipt contracts.
2. Hydrate the full character aggregate after the existing authentication flow.
3. Persist Town Currency, shop, Stash, Loadout, prepared Equipment and attribute/progression
   mutations.
4. Persist definitive expedition Loot and progression results idempotently.
5. Validate restart hydration, retries, conflicts, disconnections and multi-client behavior.

## 7. Decisions and pending technical contracts

### Decisions

- HTTP/REST remains the backend transport for this slice; no persistent socket is required for
  metagame persistence.
- The backend is the sole durable authority for the character aggregate.
- Unity may cache confirmed state for presentation, but an in-memory commit is not durable success.
- The remote `CharacterId` and Unity `ProfileId` are the same character identity at the integration
  boundary.
- Fusion `PlayerRef`, `NetworkId` and avatar identity are never persistence identities.

### Pending technical contracts

- Define retry and user-recovery behavior when a result submission cannot be durably acknowledged.
- Define backend revision/conflict semantics for competing or stale Town mutations.
- Define remote recovery for a reservation left pending by application termination.
- Decide the exact asynchronous adapter/service around `LocalProfileStore` without pretending that
  its synchronous `ILocalProfileRepository` is a remote transport.

These items refine the persistence integration. They do not reopen the already implemented choice
of username/password authentication, JWT identity or remote `CharacterId`.

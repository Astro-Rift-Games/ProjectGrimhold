# Stash hardening (Unity only, no backend)

Objective: fix the Stash/Inventory defects found by the audit without touching `backend/` or the server contract. Branch `feat/stash-hardening` (from `New-Testing` @ `79677a5b`). Delivery: ask-on-risk. Runner: Unity Test Runner via UnityMCP (`run_tests`, EditMode/PlayMode), TDD enabled (Strict TDD Mode, user CLAUDE.md).
Engram mirror topic: `odd/stash-hardening/tasks`. Decisions topic: `stash/plan-decisions`.

## Specs
- S0 Scope: "sin backend" = `backend/` and the server contract are not modified; Unity keeps using the existing endpoints. Unity must validate every action against the Store rules before any remote call and match the backend's current semantics where they differ.
- S1 Store invariants: `LocalProfileStore` rejects Stash/Loadout transfers (`TryTransferToLoadout`, `TryTransferToStash`, `TryTransferAllToLoadout`, `TryTransferAllToStash`), `TryEquipFromStash`, `TryAssignPreparedEquipment` and `TryClearPreparedEquipment` while `PendingReservation != null`, and guards them with `IsAvailable`. A rejected operation moves nothing and publishes nothing.
- S2 Presenter coordination: one serialized queue for transfers and equips; a local pre-check (dry-run of the Store rules) runs before any remote call; equip/unequip go through `TownEquipmentMutationEndpoint`, with `TownStashPresenter` supplying the `CanMutate` predicate; `RemoteInventoryService` gets an interface as a test seam.
- S3 Hydration: `HydrateSnapshot` validates backend data and no longer mutates `repository.Snapshot` in place. Server data already over capacity is accepted; only operations that reduce the excess are allowed, so the 30-slot Loadout check in `LocalProfileSaveCodec` moves out of decode/encode into the Store rules.
- S4 Take All / Leave All: item-by-item sequence (remote move, then apply locally), continue after a failed item, refresh the UI after each item (GD doc 07, section 16: "Tomar Todo no constituye una transferencia masiva indivisible").
- S5 Equip from Stash: composed from `MoveToLoadout(1)` and `UpdatePreparedEquipment`. The displaced item returns to the Loadout (matches `InventoryService.updatePreparedEquipment`); capacity is evaluated on the final state. The Stash context menu "Equipar en..." carries its source so it works from the Stash panel.
- S6 Stash capacity: slots, one slot per distinct `LootId`; initial capacity 2x the Inventory (60), configurable (GD UI.1.3). StackMax is DEFERRED: no partial stacks, no derived capacity; the Architecture (`Contratos_Base_v1`) vs GD doc 14 section 6 contradiction stays recorded as debt.
- S7 Sorting: display-only ordering, item type then name (GD UI.1.3), not persisted.
- S8 Persistence track: keep `LocalProfileRepository` (file) as the productive repository because it recovers `PendingExtractionCommit`; fix `LocalPlayerPersistenceArchitecture.md` and `Persistence-Strategy-Vertical-Slice.md`; a corrupt local file no longer blocks login (discard it and hydrate from the backend).
- S9 Out of scope (needs backend): reservation rollback restoration, server-side validation, over-capacity data already stored remotely, modified clients, batch endpoint, Template/Instance identity, persisted stack layout, unawaited reservation calls in `SessionConnectionCoordinator`, mission item rewards lost on hydration.

## Tasks
- [x] U1 (S1) Store invariants + EditMode tests (RED from audit repros C1/C2) | route: inline | commit: `fix(stash): reject Stash and Equipment mutations while a reservation is pending` (hash recorded with U2)
- [ ] U2 (S2) Presenter coordination, dry-run evaluators on the Store and `IPlayerLoadoutService`, remote service interface | route: inline | commit: -
- [ ] U3 (S3) Hydration validation + codec capacity relaxation | route: inline | commit: -
- [ ] U4 (S4) Take All / Leave All sequential | route: inline | commit: -
- [ ] U5 (S5) Equip from Stash + context menu source | route: inline | commit: -
- [ ] U6 (S6) Stash capacity model, prefab, docs | route: inline | commit: -
- [ ] U7 (S7) Display sorting | route: inline | commit: -
- [ ] U8 (S8) Persistence docs + corrupt-file login | route: inline | commit: -

## Log
- L1 (2026-10-14, user, verbatim): "arma un plan omitiendo el backend"
- L2 (2026-10-14, user, verbatim): "Diferido, y las demás decisiones como las recomendás" (StackMax deferred; displaced item to Loadout; keep file repository and fix docs; accept over-capacity server data and allow only reducing operations)
- L3 (2026-10-14, user, verbatim): "si" (start U1 on a new branch `feat/stash-hardening` from `New-Testing`)
- L4 Plan change: the dry-run evaluators move from U1 to U2, where the presenter first consumes them; adding them to `IPlayerLoadoutService` also forces updating its three test fakes.
- L5 Audit evidence (store-level repros run with UnityMCP `execute_code`): C1 equip-from-Stash during a pending reservation, then rollback, loses a unit (3 to 2); C2 transfer during a pending reservation makes `TryCommitExtraction` return `PersistenceFailed`; A/A2 31 Loadout slots make `ReconcileRemoteState` and every later `Commit` fail; B a corrupt local file makes `LocalProfileRepository.Initialize` fail.
- U1 evidence (route inline). Risk: none (adds an enum value that no `switch` consumes; `TryClearPreparedEquipment` on an empty slot during a reservation used to be a silent `Success` and now reports `ReservationPending`, and the Stash UI only emits a release for occupied slots). RED: 11 new tests in `StashReservationInvariantTests` failed on behavior (7 operations returned `Success`/`InvalidInventory` instead of `ReservationPending`; the unavailable-profile test threw `NullReferenceException`). GREEN: 53/53 in 13 stash/persistence classes except 2 failures that already existed on `New-Testing`: `PreparedEquipmentTests.Store_ReservationCarriesEveryPreparedSlot` (Raid admission encoding) and `PreparationAndReservation_RejectPersistedWeaponWithUnmetRequirementsAtomically` (unhandled `LogError` added to `TryCreateLoadoutReservation` by `d4131be1`, a test without `LogAssert.Expect`); `DevelopmentProfileBootstrapTests` 26/26; PlayMode `LobbyStashUIPlayModeTests` + `TownInventoryPresenterPlayModeTests` 8/8. Full EditMode: 3796 tests, the runner caps failures at 25 and all 25 shown belong to Merchant, NetworkPlayerMelee/Ranged prefabs, Host Migration, ArmorSetCatalog and the two above; none touch this change.
- Docs: `LocalPlayerPersistenceArchitecture.md` (Loadout reservation boundary) documents the invariant.
- Next: U2.

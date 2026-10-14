#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Stash
{
    /// <summary>
    /// Covers the Stash action coordinator: local pre-check before any remote call, remote-first
    /// transfers, reconciliation on failure and one serialized queue for transfers and equips.
    /// </summary>
    public sealed class StashTransferCoordinatorTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProfileValue = "49494949494949494949494949494949";

        private static readonly LootId Bone = new("bone");
        private static readonly LootId Sword = new("rapier");

        private LootDefinitionCatalog _catalog;
        private GameObject _services;
        private LocalProfileStore _store;
        private IPlayerStashService _stash;
        private IPlayerLoadoutService _loadout;
        private FakeRemote _remote;
        private FakeReconciliation _reconciliation;
        private bool _canMutateEquipment;
        private ProfileId _profile;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
            _remote = new FakeRemote();
            _reconciliation = new FakeReconciliation();
            _canMutateEquipment = true;
            _profile = new ProfileId(ProfileValue);
        }

        [TearDown]
        public void TearDown()
        {
            if (_services != null)
            {
                UnityEngine.Object.DestroyImmediate(_services);
            }
        }

        [Test]
        public async Task Transfer_FullLoadout_IsRejectedBeforeAnyRemoteCall()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids.Take(LocalProfileSnapshot.MaxLoadoutSlots))
                {
                    snapshot.Loadout.Add(new StashItem(id, 1));
                }

                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1));
            });

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                ids[LocalProfileSnapshot.MaxLoadoutSlots],
                fromStash: true,
                LootTransferQuantityMode.FullStack);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_reconciliation.Calls, Is.Zero);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Transfer_Success_CallsTheRemoteBeforeApplyingLocally()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 5)));
            int stashAmountDuringRemoteCall = -1;
            _remote.OnCall = _ => stashAmountDuringRemoteCall = _store.GetStash().Single().Amount;

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:5" }));
            Assert.That(stashAmountDuringRemoteCall, Is.EqualTo(5));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Bone, 5)));
        }

        [Test]
        public async Task Transfer_SingleUnitAndLoadoutDirection_UseTheMatchingRemoteCall()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Loadout.Add(new StashItem(Bone, 4)));

            StashTransferOutcome single = await coordinator.TransferAsync(
                Bone,
                fromStash: false,
                LootTransferQuantityMode.SingleUnit);
            StashTransferOutcome full = await coordinator.TransferAsync(
                Bone,
                fromStash: false,
                LootTransferQuantityMode.FullStack);

            Assert.That(single.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(full.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toStash:bone:1", "toStash:bone:3" }));
            Assert.That(_store.GetStash().Single(), Is.EqualTo(new StashItem(Bone, 4)));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task Transfer_ItemNotOwned_IsRejectedWithoutARemoteCall()
        {
            StashTransferCoordinator coordinator = Create(null);

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_remote.Calls, Is.Empty);
        }

        [Test]
        public async Task Transfer_RemoteFailure_ReconcilesAndLeavesTheLocalProfileUntouched()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 2)));
            _remote.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.SingleUnit);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(outcome.RemoteError, Is.EqualTo("conflict"));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo("REVISION_CONFLICT"));
            Assert.That(outcome.RecoveryErrorCode, Is.Null);
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task Transfer_LocalRuleFailsAfterTheRemoteSucceeded_ReconcilesAndReportsDivergence()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 3)));
            _remote.OnCall = _ => _stash.TryConsumeLoot(_profile, Bone, 3);

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Diverged));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task Transfer_WithoutARemoteService_IsUnavailableAndChangesNothing()
        {
            StashTransferCoordinator coordinator = Create(
                snapshot => snapshot.Stash.Add(new StashItem(Bone, 1)),
                withRemote: false);

            StashTransferOutcome outcome = await coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(1));
        }

        [Test]
        public async Task Transfers_AreSerialized_TheSecondWaitsForTheFirstRemoteCall()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remote.Gates.Enqueue(gate.Task);

            Task<StashTransferOutcome> first = coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);
            Task<StashTransferOutcome> second = coordinator.TransferAsync(
                Sword,
                fromStash: true,
                LootTransferQuantityMode.FullStack);
            await YieldSeveralTimes();

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2" }));

            gate.SetResult(true);
            await Task.WhenAll(first, second);

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2", "toLoadout:rapier:1" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Equip_WhileATransferIsInFlight_WaitsForIt()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 1));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
            });
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remote.Gates.Enqueue(gate.Task);

            Task<StashTransferOutcome> transfer = coordinator.TransferAsync(
                Bone,
                fromStash: true,
                LootTransferQuantityMode.FullStack);
            Task<StashTransferOutcome> equip = coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: false);
            await YieldSeveralTimes();

            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);

            gate.SetResult(true);
            await Task.WhenAll(transfer, equip);

            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:1", "equipment" }));
        }

        [Test]
        public async Task Equip_WithTheGateClosed_IsBlockedWithoutLocalOrRemoteEffect()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Loadout.Add(new StashItem(Sword, 1)));
            _canMutateEquipment = false;

            StashTransferOutcome outcome = await coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: false);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Blocked));
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
            Assert.That(_store.GetLoadout().Single().Amount, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.Empty);
        }

        [Test]
        public async Task Equip_FromTheLoadout_CommitsLocallyThenSyncsTheEquipmentOnce()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Loadout.Add(new StashItem(Sword, 1)));

            StashTransferOutcome outcome = await coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: false);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "equipment" }));
            Assert.That(_remote.LastEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task Equip_FromTheStash_CommitsLocallyThenSyncsTheEquipment()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Sword, 1)));

            StashTransferOutcome outcome = await coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.LastEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_store.GetStash(), Is.Empty);
        }

        [Test]
        public async Task Equip_RemoteSyncFailure_ReconcilesAndReportsTheFailure()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Loadout.Add(new StashItem(Sword, 1)));
            _remote.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: false);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task Unequip_ReturnsTheUnitToTheLoadoutAndSyncsTheEquipment()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Sword, default);
            });

            StashTransferOutcome outcome = await coordinator.UnequipAsync(EquipmentSlot.WeaponSetAMainHand);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Sword, 1)));
            Assert.That(_remote.LastEquipment.WeaponSetAMainHand.IsValid, Is.False);
        }

        [Test]
        public async Task Unequip_WithTheGateClosed_IsBlocked()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Sword, default);
            });
            _canMutateEquipment = false;

            StashTransferOutcome outcome = await coordinator.UnequipAsync(EquipmentSlot.WeaponSetAMainHand);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Blocked));
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_remote.Calls, Is.Empty);
        }

        [Test]
        public async Task TakeAll_MovesEveryStackRemotelyThenLocally()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2", "toLoadout:rapier:1" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task TakeAll_WhenTheLoadoutFillsUp_MovesWhatFitsAndKeepsTheRest()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids)
                {
                    snapshot.Stash.Add(new StashItem(id, 1));
                }
            });

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Partial));
            Assert.That(outcome.Completed, Is.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(_store.GetStash().Single().LootId, Is.EqualTo(ids[LocalProfileSnapshot.MaxLoadoutSlots]));
        }

        [Test]
        public async Task TakeAll_NothingFits_IsRejectedBeforeAnyRemoteCall()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 2);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids.Take(LocalProfileSnapshot.MaxLoadoutSlots))
                {
                    snapshot.Loadout.Add(new StashItem(id, 1));
                }

                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1));
                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots + 1], 1));
            });

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(outcome.Completed, Is.Zero);
            Assert.That(outcome.Failed, Is.EqualTo(2));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task TakeAll_RemoteFailureOnOneStack_ContinuesAndKeepsEarlierMoves()
        {
            LootId[] ids = DistinctLootIds(3);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids)
                {
                    snapshot.Stash.Add(new StashItem(id, 1));
                }
            });
            _remote.FailWhen = call => call.Contains(ids[1].Value);

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Partial));
            Assert.That(outcome.Completed, Is.EqualTo(2));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(3));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetLoadout().Select(item => item.LootId), Is.EqualTo(new[] { ids[0], ids[2] }));
            Assert.That(_store.GetStash().Single().LootId, Is.EqualTo(ids[1]));
        }

        [Test]
        public async Task TakeAll_RemoteFailureAndFailedReconciliation_StopsBeforeTheNextRemoteCall()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.FailWhen = call => call == "toLoadout:bone:2";
            _reconciliation.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2" }),
                "A failed reconciliation must stop further mutations against untrusted local state.");
            Assert.That(outcome.IsSuccess, Is.False);
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RecoveryFailed));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo("REVISION_CONFLICT"));
            Assert.That(outcome.RemoteError, Is.EqualTo("conflict"));
            Assert.That(outcome.RecoveryErrorCode, Is.EqualTo("RECONCILIATION_FAILED"));
            Assert.That(outcome.Completed, Is.Zero);
            Assert.That(outcome.Failed, Is.EqualTo(2));
        }

        [Test]
        public async Task TakeAll_LocalApplyDivergenceAndFailedReconciliation_StopsBeforeTheNextRemoteCall()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.OnCall = call =>
            {
                if (call == "toLoadout:bone:2")
                {
                    Assert.That(_stash.TryConsumeLoot(_profile, Bone, 2), Is.EqualTo(StashOperationResult.Success));
                }
            };
            _reconciliation.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2" }),
                "Remote success followed by local divergence cannot permit another mutation when recovery fails.");
            Assert.That(outcome.IsSuccess, Is.False);
            Assert.That(_store.GetStash().Single(), Is.EqualTo(new StashItem(Sword, 1)));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RecoveryFailed));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(outcome.RemoteErrorCode, Is.Null);
            Assert.That(outcome.RecoveryErrorCode, Is.EqualTo("RECONCILIATION_FAILED"));
            Assert.That(outcome.Completed, Is.Zero);
            Assert.That(outcome.Failed, Is.EqualTo(2));
        }

        [Test]
        public async Task TakeAll_RemoteErrorButReconciliationRestoresCommittedDestination_DoesNotClaimNotMoved()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.FailWhen = call => call == "toLoadout:rapier:1";
            _remote.Error = new BackendError { error = "TRANSPORT_ERROR", message = "response lost" };
            _reconciliation.OnCall = () =>
            {
                // Model the authoritative refresh after a committed mutation whose response was lost.
                Assert.That(_loadout.TryTransferToLoadout(_profile, Sword, 1), Is.EqualTo(StashOperationResult.Success));
            };

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:bone:2", "toLoadout:rapier:1" }));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
            Assert.That(outcome.Completed, Is.EqualTo(1));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo("TRANSPORT_ERROR"));
            Assert.That(outcome.ToString(), Does.Not.Contain("not moved"),
                "A lost response does not prove the mutation failed; refreshed ownership confirms the destination.");
        }

        [Test]
        public async Task TakeAll_OwnershipDisappearsAfterReconciliation_AccountsForEveryOriginalStack()
        {
            LootId[] ids = DistinctLootIds(3);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids)
                {
                    snapshot.Stash.Add(new StashItem(id, 1));
                }
            });
            _remote.FailWhen = call => call == $"toLoadout:{ids[1].Value}:1";
            _reconciliation.OnCall = () =>
            {
                Assert.That(_stash.TryConsumeLoot(_profile, ids[2], 1), Is.EqualTo(StashOperationResult.Success));
            };

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[]
            {
                $"toLoadout:{ids[0].Value}:1", $"toLoadout:{ids[1].Value}:1"
            }));
            Assert.That(_store.GetLoadout().Single().LootId, Is.EqualTo(ids[0]));
            Assert.That(_store.GetStash().Single().LootId, Is.EqualTo(ids[1]));
            Assert.That(outcome.IsSuccess, Is.False);
            Assert.That(outcome.Completed, Is.EqualTo(1));
            Assert.That(outcome.Completed + outcome.Failed, Is.EqualTo(ids.Length),
                "A stack removed by reconciliation must not disappear from the sequence summary.");
        }

        [Test]
        public async Task TakeAll_AppliesEachStackLocallyBeforeTheNextRemoteCall()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            var loadoutCountAtEachCall = new List<int>();
            _remote.OnCall = _ => loadoutCountAtEachCall.Add(_store.GetLoadout().Count);

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(loadoutCountAtEachCall, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public async Task LeaveAll_RemoteFailureOnOneStack_ContinuesAndKeepsEarlierMoves()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Bone, 2));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
            });
            _remote.FailWhen = call => call.Contains(Bone.Value);

            StashTransferOutcome outcome = await coordinator.LeaveAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Partial));
            Assert.That(outcome.Completed, Is.EqualTo(1));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toStash:bone:2", "toStash:rapier:1" }));
            Assert.That(_store.GetLoadout().Single().LootId, Is.EqualTo(Bone));
            Assert.That(_store.GetStash().Single().LootId, Is.EqualTo(Sword));
        }

        [Test]
        public async Task LeaveAll_MovesEveryStackRemotelyThenLocally()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Bone, 2));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
            });

            StashTransferOutcome outcome = await coordinator.LeaveAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toStash:bone:2", "toStash:rapier:1" }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
        }

        private static async Task YieldSeveralTimes()
        {
            for (int index = 0; index < 5; index++)
            {
                await Task.Yield();
            }
        }

        private LootId[] DistinctLootIds(int count)
        {
            var ids = new LootId[count];
            int found = 0;
            for (int index = 0; index < _catalog.DefinitionCount && found < count; index++)
            {
                if (_catalog.TryGetByIndex(index, out LootDefinition definition))
                {
                    ids[found++] = definition.LootId;
                }
            }

            Assert.That(found, Is.EqualTo(count), "The loot catalog needs enough distinct definitions.");
            return ids;
        }

        private StashTransferCoordinator Create(Action<LocalProfileSnapshot> configure, bool withRemote = true)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            configure?.Invoke(snapshot);
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            _store = new LocalProfileStore(repository, _profile, _catalog);

            _services = new GameObject("StashCoordinatorTestServices");
            var stashService = _services.AddComponent<InMemoryPlayerStashService>();
            stashService.Initialize(_store);
            var loadoutService = _services.AddComponent<InMemoryPlayerLoadoutService>();
            loadoutService.Initialize(_store);
            _stash = stashService;
            _loadout = loadoutService;

            var endpoint = new TownEquipmentMutationEndpoint(
                _loadout,
                _profile,
                _catalog,
                () => _canMutateEquipment);
            return new StashTransferCoordinator(
                _profile,
                _stash,
                _loadout,
                withRemote ? _remote : null,
                withRemote ? _reconciliation : null,
                endpoint);
        }

        private sealed class FakeRemote : IRemoteInventoryService
        {
            public readonly List<string> Calls = new();
            public readonly Queue<Task> Gates = new();
            public Action<string> OnCall;
            public Func<string, bool> FailWhen;
            public bool Succeeds = true;
            public BackendError Error = new() { error = "REVISION_CONFLICT", message = "conflict" };
            public PreparedEquipmentLoadout LastEquipment;

            public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId lootId, int amount) =>
                Run($"toLoadout:{lootId.Value}:{amount}");

            public Task<(bool success, BackendError error)> MoveToStashAsync(LootId lootId, int amount) =>
                Run($"toStash:{lootId.Value}:{amount}");

            public Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(
                PreparedEquipmentLoadout equipment)
            {
                LastEquipment = equipment;
                return Run("equipment");
            }

            private async Task<(bool success, BackendError error)> Run(string call)
            {
                Calls.Add(call);
                OnCall?.Invoke(call);
                if (Gates.Count > 0)
                {
                    await Gates.Dequeue();
                }

                return Succeeds && (FailWhen == null || !FailWhen(call))
                    ? (true, default)
                    : (false, Error);
            }
        }

        private sealed class FakeReconciliation : IProfileReconciliation
        {
            public int Calls;
            public Action OnCall;
            public bool Succeeds = true;

            public Task<bool> ReconcileAsync()
            {
                Calls++;
                OnCall?.Invoke();
                return Task.FromResult(Succeeds);
            }
        }
    }
}
#endif

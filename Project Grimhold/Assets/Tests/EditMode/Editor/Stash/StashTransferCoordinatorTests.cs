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
        private LootDefinitionCatalog _capacityCatalog;
        private readonly List<LootDefinition> _capacityDefinitions = new();
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
            if (_capacityCatalog != null) UnityEngine.Object.DestroyImmediate(_capacityCatalog);
            foreach (LootDefinition definition in _capacityDefinitions) UnityEngine.Object.DestroyImmediate(definition);
            _capacityDefinitions.Clear();
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
        public async Task Transfer_FullStash_IsRejectedBeforeAnyRemoteCall()
        {
            LootId[] ids = CreateCapacityCatalog();
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids.Take(60)) snapshot.Stash.Add(new StashItem(id, 1));
                snapshot.Loadout.Add(new StashItem(ids[60], 1));
            });
            StashTransferOutcome outcome = await coordinator.TransferAsync(ids[60], false, LootTransferQuantityMode.FullStack);
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_reconciliation.Calls, Is.Zero);
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task LeaveAll_StashFills_PreservesSuccessfulStacksAndSkipsRemoteForRejectedStack()
        {
            LootId[] ids = CreateCapacityCatalog();
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids.Take(59)) snapshot.Stash.Add(new StashItem(id, 1));
                snapshot.Loadout.Add(new StashItem(ids[59], 2));
                snapshot.Loadout.Add(new StashItem(ids[60], 3));
                snapshot.Loadout.Add(new StashItem(ids[0], 4));
            });
            StashTransferOutcome outcome = await coordinator.LeaveAllAsync();
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Partial));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { $"toStash:{ids[59].Value}:2", $"toStash:{ids[0].Value}:4" }));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(60));
            Assert.That(_store.GetStash().Single(x => x.LootId == ids[0]).Amount, Is.EqualTo(5));
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(ids[60], 3)));
            Assert.That(_reconciliation.Calls, Is.Zero);
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
        public async Task Equip_FromTheStash_MovesOneRemotelyThenProjectsEquipmentBeforeOneLocalCommit()
        {
            var displaced = new LootId("arming_sword");
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 2));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(displaced, default);
            });
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            _remote.OnCall = _ =>
            {
                Assert.That(commits, Is.Zero, "Neither request may publish optimistic local state.");
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2) }));
                Assert.That(_store.GetLoadout(), Is.Empty);
                Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(displaced));
            };

            StashTransferOutcome outcome = await coordinator.EquipAsync(
                Sword,
                EquipmentSlot.WeaponSetAMainHand,
                fromStash: true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
            Assert.That(_remote.LastEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(displaced, 1) }));
            Assert.That(commits, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Equip_FromStash_OwnershipChangesDuringEquipmentAwait_DivergesWithoutApplyingEquip(bool clearOtherSet)
        {
            var otherSword = new LootId("arming_sword");
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 2));
                snapshot.Loadout.Add(new StashItem(Bone, 2));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(default, otherSword);
            });
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            StashOperationResult competingMutation = StashOperationResult.InvalidInventory;
            _remote.OnCall = call =>
            {
                if (call != "equipment") return;
                _remote.Gates.Enqueue(gate.Task);
            };
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            Task<StashTransferOutcome> pending = coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);
            await YieldSeveralTimes();
            // The second request has captured its DTO and is awaiting the gate. Another
            // endpoint now commits a valid Store mutation before the request completes.
            competingMutation = clearOtherSet
                ? _store.TryClearPreparedEquipment(EquipmentSlot.WeaponSetBMainHand)
                : _store.TryTransferToStash(Bone, 1);
            gate.SetResult(true);
            StashTransferOutcome outcome = await pending;

            Assert.That(competingMutation, Is.EqualTo(StashOperationResult.Success));
            Assert.That(_remote.LastEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_remote.LastEquipment.WeaponSetBMainHand, Is.EqualTo(otherSword));
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Diverged),
                "A successful but different current-state plan must not be reported as the submitted plan's success.");
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
            Assert.That(commits, Is.EqualTo(1), "Only the competing Store mutation may commit; the fake refresh writes nothing.");
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
            Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Sword, 2)));
            Assert.That(_store.GetLoadout().Any(item => item.LootId == Sword), Is.False);
            if (clearOtherSet)
            {
                Assert.That(_store.GetPreparedEquipment().WeaponSetBMainHand.IsValid, Is.False);
                Assert.That(_store.GetLoadout(), Has.Some.EqualTo(new StashItem(otherSword, 1)));
            }
            else
            {
                Assert.That(_store.GetPreparedEquipment().WeaponSetBMainHand, Is.EqualTo(otherSword));
                Assert.That(_store.GetLoadout(), Has.Some.EqualTo(new StashItem(Bone, 1)));
                Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Bone, 1)));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Equip_FromStash_OwnershipDriftAfterFirstRequest_StopsBeforeEquipmentRequest(bool recoverySucceeds)
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 2));
                snapshot.Loadout.Add(new StashItem(Bone, 2));
            });
            _reconciliation.Succeeds = recoverySucceeds;
            StashOperationResult competingMutation = StashOperationResult.InvalidInventory;
            _remote.OnCall = _ => competingMutation = _store.TryTransferToStash(Bone, 1);

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(competingMutation, Is.EqualTo(StashOperationResult.Success));
            Assert.That(outcome.Status, Is.EqualTo(recoverySucceeds
                ? StashTransferStatus.Diverged : StashTransferStatus.RecoveryFailed));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1" }));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Sword, 2)));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 1) }));
        }

        [Test]
        public async Task Equip_FromStash_AttributesChangeAfterRemoteSuccess_LocalRefusalReconciles()
        {
            var greatsword = new LootId("long_sword");
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(greatsword, 1)));
            Assert.That(CharacterAttributeState.TryCreate(5, 5, 10, 5, 5, 5, 0, out CharacterAttributeState eligible), Is.True);
            Assert.That(CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState ineligible), Is.True);
            _store.ForceCharacterAttributeState(eligible);
            _remote.OnCall = call =>
            {
                if (call == "equipment") _store.ForceCharacterAttributeState(ineligible);
            };

            StashTransferOutcome outcome = await coordinator.EquipAsync(greatsword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:long_sword:1", "equipment" }));
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Diverged));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.AttributeRequirementsNotMet));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(greatsword, 1) }));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task Equip_FromStash_RemoteRevisionOnlyChanges_StillAppliesSubmittedOwnershipPlan()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Sword, 2)));
            int revision = 0;
            _remote.OnCall = _ => _store.SetRemoteRevision(++revision);

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
            Assert.That(_reconciliation.Calls, Is.Zero, "Endpoint revision updates alone are not ownership drift.");
            Assert.That(_store.RemoteRevision, Is.EqualTo(2));
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [TestCase("toLoadout:rapier:1")]
        [TestCase("equipment")]
        public async Task Equip_FromStash_RemoteFailureAtEitherStep_ReconcilesWithoutRetryOrLocalCommit(string failingCall)
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Sword, 2)));
            _remote.FailWhen = call => call == failingCall;
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            _reconciliation.OnCall = () =>
            {
                Assert.That(commits, Is.Zero);
                Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2) }));
                Assert.That(_store.GetLoadout(), Is.Empty);
            };

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(_remote.Calls, Is.EqualTo(failingCall == "equipment"
                ? new[] { "toLoadout:rapier:1", "equipment" }
                : new[] { "toLoadout:rapier:1" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(commits, Is.Zero);
        }

        [TestCase("toLoadout:rapier:1")]
        [TestCase("equipment")]
        public async Task Equip_FromStash_RemoteException_ReconcilesOnceAndStops(string throwingCall)
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Sword, 1)));
            _remote.OnCall = call =>
            {
                if (call == throwingCall) throw new InvalidOperationException("response unavailable");
            };
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo("REMOTE_RESULT_UNKNOWN"));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(throwingCall == "equipment"
                ? new[] { "toLoadout:rapier:1", "equipment" } : new[] { "toLoadout:rapier:1" }));
            Assert.That(commits, Is.Zero);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
        }

        [Test]
        public async Task Equip_FromStash_SecondStepFailure_ReconciliationCanRestoreCommittedFirstMove()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Sword, 2)));
            _remote.FailWhen = call => call == "equipment";
            _reconciliation.OnCall = () =>
            {
                Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2) }));
                Assert.That(_loadout.TryTransferToLoadout(_profile, Sword, 1), Is.EqualTo(StashOperationResult.Success));
            };

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
        }

        [Test]
        public async Task Equip_FromStash_WithoutRemote_IsUnavailableWithoutLocalCommit()
        {
            StashTransferCoordinator coordinator = Create(
                snapshot => snapshot.Stash.Add(new StashItem(Sword, 1)), withRemote: false);
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(commits, Is.Zero);
            Assert.That(_remote.Calls, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Equip_FromStash_PreflightRejectsBeforeRemote(bool gateClosed)
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(new LootId("arming_sword"), default);
                for (int index = 0; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
                    snapshot.Loadout.Add(new StashItem(new LootId($"preflight-{index}"), 1));
            });
            _canMutateEquipment = !gateClosed;
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(gateClosed ? StashTransferStatus.Blocked : StashTransferStatus.Rejected));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_reconciliation.Calls, Is.Zero);
            Assert.That(commits, Is.Zero);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(_store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(new LootId("arming_sword")));
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(30));
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

        private LootId[] CreateCapacityCatalog()
        {
            // Only the U6 capacity tests replace the fixture's productive catalog.
            for (int i = 0; i < 70; i++)
            {
                var definition = ScriptableObject.CreateInstance<LootDefinition>();
                typeof(LootDefinition).GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, $"stash_capacity_{i}");
                _capacityDefinitions.Add(definition);
            }
            _capacityCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            typeof(LootDefinitionCatalog).GetField("_definitions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_capacityCatalog, new List<LootDefinition>(_capacityDefinitions));
            _catalog = _capacityCatalog;
            Assert.That(_catalog.DefinitionCount, Is.EqualTo(70));
            return _capacityDefinitions.Select(d => d.LootId).ToArray();
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

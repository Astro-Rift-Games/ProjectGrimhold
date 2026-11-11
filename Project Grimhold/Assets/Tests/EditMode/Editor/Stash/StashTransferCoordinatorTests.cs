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
            _remote = new FakeRemote { Simulate = SimulateBulk };
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
        public void ApplicationContext_SharesQueueAcrossConsumersAndReinitialization_ReplacesOnServiceChange()
        {
            Create(null);
            var context = _services.AddComponent<ApplicationStashContext>();
            context.Initialize(_store, _stash, _loadout, null, null);
            var first = context.GetTransferCoordinator();
            Assert.That(first, Is.Not.Null);
            Assert.That(context.GetTransferCoordinator(), Is.SameAs(first));
            context.Initialize(_store, _stash, _loadout, null, null);
            Assert.That(context.GetTransferCoordinator(), Is.SameAs(first));
            var replacement = _services.AddComponent<InMemoryPlayerLoadoutService>();
            replacement.Initialize(_store);
            context.Initialize(_store, _stash, replacement, null, null);
            Assert.That(context.GetTransferCoordinator(), Is.Not.SameAs(first));
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
        public async Task LeaveAll_StashFills_SkipsTheRefusedStackAndKeepsEvaluatingLaterStacks()
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
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }));
            Assert.That(outcome.Completed, Is.EqualTo(2));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(60));
            Assert.That(_store.GetStash().Single(x => x.LootId == ids[0]).Amount, Is.EqualTo(5));
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(ids[60], 3, 1)));
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
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Bone, 5, 0)));
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
            Assert.That(_store.GetStash().Single(), Is.EqualTo(new StashItem(Bone, 4, 0)));
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
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(displaced, 1, 0) }));
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
            Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Sword, 2, 0)));
            Assert.That(_store.GetLoadout().Any(item => item.LootId == Sword), Is.False);
            if (clearOtherSet)
            {
                Assert.That(_store.GetPreparedEquipment().WeaponSetBMainHand.IsValid, Is.False);
                Assert.That(_store.GetLoadout(), Has.Some.EqualTo(new StashItem(otherSword, 1, 1)));
            }
            else
            {
                Assert.That(_store.GetPreparedEquipment().WeaponSetBMainHand, Is.EqualTo(otherSword));
                Assert.That(_store.GetLoadout(), Has.Some.EqualTo(new StashItem(Bone, 1, 0)));
                Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Bone, 1, 1)));
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
            Assert.That(_store.GetStash(), Has.Some.EqualTo(new StashItem(Sword, 2, 0)));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 1, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(greatsword, 1, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
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
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
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
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 2, 0) }));
                Assert.That(_loadout.TryTransferToLoadout(_profile, Sword, 1), Is.EqualTo(StashOperationResult.Success));
            };

            StashTransferOutcome outcome = await coordinator.EquipAsync(Sword, EquipmentSlot.WeaponSetAMainHand, true);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "toLoadout:rapier:1", "equipment" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
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
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Sword, 1, 0) }));
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
            Assert.That(_store.GetLoadout().Single(), Is.EqualTo(new StashItem(Sword, 1, 0)));
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
        public async Task TakeAll_IssuesOneBulkCall_AndAppliesTheBackendResultWithOneCommit()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            TransferAllResult backend = default;
            _remote.OnResult = result => backend = result;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(outcome.Completed, Is.EqualTo(2));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout().Select(item => (item.Slot, item.LootId.Value, item.Amount)),
                Is.EquivalentTo(backend.loadout.Select(item => (item.slot, item.lootId, item.amount))));
            Assert.That(_store.RemoteRevision, Is.EqualTo(backend.revision));
            Assert.That(_reconciliation.Calls, Is.Zero);
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
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(_store.GetStash().Single().LootId, Is.EqualTo(ids[LocalProfileSnapshot.MaxLoadoutSlots]));
        }

        [Test]
        public async Task TakeAll_NothingFits_IsRejectedWithoutChangingLocalState()
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
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(outcome.Completed, Is.Zero);
            Assert.That(outcome.Failed, Is.EqualTo(2));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(commits, Is.Zero);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task TakeAll_EmptySource_SucceedsWithoutARemoteCall()
        {
            StashTransferCoordinator coordinator = Create(_ => { });

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(outcome.Completed, Is.Zero);
            Assert.That(_remote.Calls, Is.Empty);
        }

        [Test]
        public async Task TakeAll_CountsComeFromThePerStackResults()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.Override = simulated =>
            {
                simulated.results = new[]
                {
                    new TransferAllStackResult { slot = 0, lootId = "bone", amount = 2, moved = true },
                    new TransferAllStackResult { slot = 1, lootId = "rapier", amount = 1, moved = false, errorCode = "INVENTORY_FULL" },
                };
                return simulated;
            };

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Partial));
            Assert.That(outcome.Completed, Is.EqualTo(1));
            Assert.That(outcome.Failed, Is.EqualTo(1));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InventoryFull));
        }

        [Test]
        public async Task TakeAll_RemoteFailure_LeavesLocalStateUnchangedAndReconcilesOnce()
        {
            LootId[] ids = DistinctLootIds(3);
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids)
                {
                    snapshot.Stash.Add(new StashItem(id, 1));
                }
            });
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            _remote.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo("REVISION_CONFLICT"));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(commits, Is.Zero);
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(3));
        }

        [Test]
        public async Task TakeAll_RemoteFailureAndFailedReconciliation_ReportsRecoveryFailed()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.Succeeds = false;
            _reconciliation.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
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
        public async Task TakeAll_BackendResultThatCannotBeApplied_ReconcilesOnceAsDiverged()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Stash.Add(new StashItem(Sword, 1));
            });
            _remote.Override = simulated =>
            {
                simulated.revision = _store.RemoteRevision;
                return simulated;
            };

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Diverged));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toLoadout" }));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task TakeAll_BackendResultThatCannotBeAppliedAndFailedReconciliation_ReportsRecoveryFailed()
        {
            StashTransferCoordinator coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 2)));
            _remote.Override = simulated =>
            {
                simulated.revision = _store.RemoteRevision;
                return simulated;
            };
            _reconciliation.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.TakeAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RecoveryFailed));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(outcome.RemoteErrorCode, Is.Null);
            Assert.That(outcome.RecoveryErrorCode, Is.EqualTo("RECONCILIATION_FAILED"));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task LeaveAll_RemoteFailure_LeavesLocalStateUnchangedAndReconcilesOnce()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Bone, 2));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
            });
            _remote.Succeeds = false;

            StashTransferOutcome outcome = await coordinator.LeaveAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.RemoteFailed));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetLoadout(), Has.Count.EqualTo(2));
            Assert.That(_store.GetStash(), Is.Empty);
        }

        [Test]
        public async Task LeaveAll_IssuesOneBulkCall_AndAppliesTheBackendResult()
        {
            StashTransferCoordinator coordinator = Create(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Bone, 2));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
            });

            StashTransferOutcome outcome = await coordinator.LeaveAllAsync();

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "bulk:toStash" }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(_store.GetStash(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Sort_RemoteFirst_ThenLocal_AndIdenticalOrderSkips()
        {
            LootId[] ids = new[] { Bone, Sword }.OrderByDescending(id => id.Value, StringComparer.Ordinal).ToArray();
            var coordinator = Create(snapshot =>
            {
                foreach (LootId id in ids) snapshot.Stash.Add(new StashItem(id, 2));
            });
            _remote.OnCall = _ => Assert.That(_store.GetStash().Select(item => item.LootId), Is.EqualTo(ids));
            Assert.That((await coordinator.SortAsync(InventoryList.Stash)).IsSuccess, Is.True);
            Assert.That(_store.GetStash().Select(item => item.LootId), Is.EqualTo(ids.Reverse()));
            Assert.That((await coordinator.SortAsync(InventoryList.Stash)).IsSuccess, Is.True);
            Assert.That(_remote.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Sort_RemoteFailure_ReconcilesOnceWithoutApplying()
        {
            var coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1));
                snapshot.Stash.Add(new StashItem(Bone, 2));
            });
            _remote.Succeeds = false;
            Assert.That((await coordinator.SortAsync(InventoryList.Stash)).IsSuccess, Is.False);
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(1));
            Assert.That(_store.GetStash()[0].LootId, Is.EqualTo(Sword));
        }

        [Test]
        public void Order_ExactPermutation_PreservesAmountsAndOtherList_NoOpDoesNotPublish()
        {
            Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 3));
                snapshot.Stash.Add(new StashItem(Bone, 2));
                snapshot.Loadout.Add(new StashItem(Bone, 5));
            });
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            Assert.That(_store.EvaluateOrder(InventoryList.Stash, new[] { Bone, Sword }), Is.EqualTo(StashOperationResult.Success));
            Assert.That(commits, Is.Zero);
            Assert.That(_store.TryOrder(InventoryList.Stash, new[] { Bone, Sword }), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_store.GetStash().Select(item => item.Amount), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(_store.GetLoadout().Single().Amount, Is.EqualTo(5));
            Assert.That(_store.TryOrder(InventoryList.Stash, new[] { Bone, Sword }), Is.EqualTo(StashOperationResult.Success));
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test]
        public void Order_RejectsMissingExtraAndDuplicateIds()
        {
            Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1));
                snapshot.Stash.Add(new StashItem(Bone, 2));
            });
            foreach (LootId[] order in new[] { new[] { Bone }, new[] { Bone, Sword, new LootId("extra") }, new[] { Bone, Bone } })
                Assert.That(_store.TryOrder(InventoryList.Stash, order), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_store.GetStash()[0].LootId, Is.EqualTo(Sword));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Sort_LocalRefusalAfterRemoteSuccess_ReconcilesOnce(bool recoverySucceeds)
        {
            var coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1));
                snapshot.Stash.Add(new StashItem(Bone, 2));
            });
            // Remove an ID so the accepted remote permutation is no longer applicable.
            _remote.OnCall = _ => Assert.That(_store.TryTransferToLoadout(Bone, 2), Is.EqualTo(StashOperationResult.Success));
            _reconciliation.Succeeds = recoverySucceeds;
            var result = await coordinator.SortAsync(InventoryList.Stash);
            Assert.That(result.IsSuccess, Is.False);
            if (!recoverySucceeds) Assert.That(result.Status, Is.EqualTo(StashTransferStatus.RecoveryFailed));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Sort_UnknownRemoteResult_ReconcilesOnce()
        {
            var coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1));
                snapshot.Stash.Add(new StashItem(Bone, 2));
            });
            _remote.OnCall = _ => throw new InvalidOperationException("unknown result");
            Assert.That((await coordinator.SortAsync(InventoryList.Stash)).IsSuccess, Is.False);
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public void OrderRequest_SerializesBackendContract()
        {
            var request = new InventoryOrderRequest { list = "stash", lootIds = new[] { "bone", "rapier" }, expectedRevision = 7 };
            string json = JsonUtility.ToJson(request);
            Assert.That(json, Does.Contain("\"list\":\"stash\""));
            Assert.That(json, Does.Contain("\"lootIds\":[\"bone\",\"rapier\"]"));
            Assert.That(json, Does.Contain("\"expectedRevision\":7"));
        }

        [TestCase(InventoryList.Stash)]
        [TestCase(InventoryList.Loadout)]
        public void Order_ReservationPending_RejectsEvenIdenticalOrder(InventoryList list)
        {
            Create(snapshot => snapshot.PendingReservation = new PendingLoadoutReservation("order-test", Array.Empty<StashItem>()));
            Assert.That(_store.EvaluateOrder(list, Array.Empty<LootId>()), Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(_store.TryOrder(list, Array.Empty<LootId>()), Is.EqualTo(StashOperationResult.ReservationPending));
        }

        [Test]
        public async Task Sort_AllowedWhenEquipmentReadyGateIsClosed()
        {
            var coordinator = Create(snapshot =>
            {
                snapshot.Loadout.Add(new StashItem(Sword, 3));
                snapshot.Loadout.Add(new StashItem(Bone, 2));
            });
            _canMutateEquipment = false;
            Assert.That((await coordinator.SortAsync(InventoryList.Loadout)).IsSuccess, Is.True);
            Assert.That(_store.GetLoadout().Select(item => item.LootId), Is.EqualTo(new[] { Bone, Sword }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task BulkTransfer_WithCatalog_StillIssuesOneCall(bool fromStash)
        {
            var coordinator = Create(snapshot =>
            {
                var source = fromStash ? snapshot.Stash : snapshot.Loadout;
                source.Add(new StashItem(Sword, 1));
                source.Add(new StashItem(Bone, 2));
            }, withCatalog: true);
            Assert.That((await (fromStash ? coordinator.TakeAllAsync() : coordinator.LeaveAllAsync())).IsSuccess, Is.True);
            Assert.That(_remote.Calls, Is.EqualTo(new[] { $"bulk:{(fromStash ? "toLoadout" : "toStash")}" }));
        }

        [TestCase(InventoryList.Stash, false)]
        [TestCase(InventoryList.Loadout, false)]
        [TestCase(InventoryList.Stash, true)]
        [TestCase(InventoryList.Loadout, true)]
        public async Task Split_Success_RemoteFirstThenLocal_OnlyAddressedSimpleOrMixedStack(InventoryList list, bool mixed)
        {
            var coordinator = Create(snapshot =>
            {
                var items = list == InventoryList.Stash ? snapshot.Stash : snapshot.Loadout;
                items.Add(new StashItem(Bone, 13, 9));
                items.Add(new StashItem(Sword, 2, 17));
                if (mixed) items.Add(new StashItem(Bone, 7, 1));
            });
            var stashBefore = _store.GetStash().ToArray();
            var loadoutBefore = _store.GetLoadout().ToArray();
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            _remote.OnCall = _ =>
            {
                Assert.That(_store.GetStash(), Is.EqualTo(stashBefore));
                Assert.That(_store.GetLoadout(), Is.EqualTo(loadoutBefore));
                Assert.That(commits, Is.Zero);
            };

            var outcome = await coordinator.SplitAsync(list, 9, 8, 5, Bone);

            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { $"split:{list}:9:8:5:bone" }));
            var itemsAfter = list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout();
            var itemsBefore = list == InventoryList.Stash ? stashBefore : loadoutBefore;
            Assert.That(itemsAfter.Single(item => item.Slot == 9), Is.EqualTo(new StashItem(Bone, 5, 9)));
            Assert.That(itemsAfter.Single(item => item.Slot == 5), Is.EqualTo(new StashItem(Bone, 8, 5)));
            Assert.That(itemsAfter.Where(item => item.Slot != 9 && item.Slot != 5),
                Is.EquivalentTo(itemsBefore.Where(item => item.Slot != 9)));
            Assert.That(itemsAfter.Sum(item => item.Amount), Is.EqualTo(itemsBefore.Sum(item => item.Amount)));
            Assert.That(list == InventoryList.Stash ? _store.GetLoadout() : _store.GetStash(),
                Is.EqualTo(list == InventoryList.Stash ? loadoutBefore : stashBefore));
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(_reconciliation.Calls, Is.Zero);
        }

        [TestCase(InventoryList.Stash)]
        [TestCase(InventoryList.Loadout)]
        public async Task Split_LocalRejection_NeverCallsRemoteOrCommits(InventoryList list)
        {
            var coordinator = Create(snapshot =>
                (list == InventoryList.Stash ? snapshot.Stash : snapshot.Loadout).Add(new StashItem(Bone, 13, 9)));
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            var stashBefore = _store.GetStash().ToArray();
            var loadoutBefore = _store.GetLoadout().ToArray();
            var outcome = await coordinator.SplitAsync(list, 9, 13, 5, Bone);
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_reconciliation.Calls, Is.Zero);
            Assert.That(_store.GetStash(), Is.EqualTo(stashBefore));
            Assert.That(_store.GetLoadout(), Is.EqualTo(loadoutBefore));
            Assert.That(commits, Is.Zero);
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public async Task Split_RemoteFailureOrUnknown_ReconcilesOnceWithoutRetryOrLocalMutation(bool unknown, bool recoverySucceeds)
        {
            var coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)));
            _reconciliation.Succeeds = recoverySucceeds;
            _remote.Succeeds = false;
            if (unknown) _remote.OnCall = _ => throw new InvalidOperationException("response lost");
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            var outcome = await coordinator.SplitAsync(InventoryList.Stash, 9, 8, 5, Bone);
            Assert.That(outcome.Status, Is.EqualTo(recoverySucceeds ? StashTransferStatus.RemoteFailed : StashTransferStatus.RecoveryFailed));
            Assert.That(outcome.RemoteErrorCode, Is.EqualTo(unknown ? "REMOTE_RESULT_UNKNOWN" : "REVISION_CONFLICT"));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "split:Stash:9:8:5:bone" }));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 13, 9) }));
            Assert.That(_store.GetLoadout(), Is.Empty);
            Assert.That(commits, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Split_LocalRefusalAfterRemoteSuccess_ReconcilesWithoutRetry(bool recoverySucceeds)
        {
            var coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)));
            _reconciliation.Succeeds = recoverySucceeds;
            _remote.OnCall = _ => Assert.That(_store.TryConsumeStashSlot(9, Bone, 13), Is.EqualTo(StashOperationResult.Success));
            var outcome = await coordinator.SplitAsync(InventoryList.Stash, 9, 8, 5, Bone);
            Assert.That(outcome.Status, Is.EqualTo(recoverySucceeds ? StashTransferStatus.Diverged : StashTransferStatus.RecoveryFailed));
            Assert.That(outcome.Rule, Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_reconciliation.Calls, Is.EqualTo(1));
            Assert.That(_remote.Calls, Has.Count.EqualTo(1));
            Assert.That(_store.GetStash(), Is.Empty);
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public async Task Split_LegacySchema_IsUnavailableWithoutRemoteCall()
        {
            var coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)), schemaVersion: 1);
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;
            var outcome = await coordinator.SplitAsync(InventoryList.Stash, 9, 8, 5, Bone);
            Assert.That(outcome.Status, Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That(_remote.Calls, Is.Empty);
            Assert.That(_reconciliation.Calls, Is.Zero);
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 13, 9) }));
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public async Task Split_WithoutRemote_IsUnavailableWithoutLocalMutation()
        {
            var coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)), withRemote: false);
            Assert.That((await coordinator.SplitAsync(InventoryList.Stash, 9, 8, 5, Bone)).Status,
                Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 13, 9) }));
            Assert.That(_remote.Calls, Is.Empty);
        }

        [Test]
        public async Task Split_AndTransfer_ShareSerializedQueue()
        {
            var coordinator = Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)));
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remote.Gates.Enqueue(gate.Task);
            Task<StashTransferOutcome> split = coordinator.SplitAsync(InventoryList.Stash, 9, 8, 5, Bone);
            Task<StashTransferOutcome> transfer = coordinator.TransferAsync(5, Bone, true, LootTransferQuantityMode.FullStack);
            await YieldSeveralTimes();
            // Release even if an assertion fails, so no pending task leaks out of this test.
            try
            {
                Assert.That(_remote.Calls, Is.EqualTo(new[] { "split:Stash:9:8:5:bone" }));
                Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 13, 9) }));
            }
            finally { gate.TrySetResult(true); }
            var outcomes = await Task.WhenAll(split, transfer);
            Assert.That(outcomes.All(outcome => outcome.IsSuccess), Is.True);
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "split:Stash:9:8:5:bone", "toLoadout:bone:8" }));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 5, 9) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 8, 0) }));
        }

        [Test]
        public void Split_LoadoutAdapter_RejectsOtherProfileWithoutCommitting()
        {
            Create(snapshot => snapshot.Stash.Add(new StashItem(Bone, 13, 9)));
            var slots = (ISlotPlayerLoadoutService)_loadout;
            var other = new ProfileId("59595959595959595959595959595959");
            Assert.That(slots.EvaluateSplit(other, InventoryList.Stash, 9, 8, 5, Bone), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(slots.TrySplit(other, InventoryList.Stash, 9, 8, 5, Bone), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 13, 9) }));
        }

        [TestCase(2)]
        [TestCase(3)]
        public async Task SupportedSchema_MoveSwapOrderAndTransfer_PreserveInstancesRemoteFirst(int schema)
        {
            var instance = new EquipmentInstance("123456789012345678901234");
            var coordinator = Create(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 1, 3, schema == 3 ? instance : null));
                snapshot.Stash.Add(new StashItem(Bone, 4, 7));
            }, schemaVersion: schema);
            var before = _store.GetStash().ToArray();
            _remote.OnCall = _ => Assert.That(_store.GetStash(), Is.EqualTo(before));
            Assert.That((await coordinator.MoveAsync(InventoryList.Stash, 3, InventoryList.Stash, 7, Sword)).IsSuccess, Is.True);
            Assert.That(_store.GetStash().Single(x => x.Slot == 7).Instance, Is.EqualTo(schema == 3 ? instance : null));
            Assert.That(_store.GetStash().Single(x => x.Slot == 3).Amount, Is.EqualTo(4));
            before = _store.GetStash().ToArray();
            Assert.That((await coordinator.OrderAsync(InventoryList.Stash, new[] { 7, 3 })).IsSuccess, Is.True);
            Assert.That(_store.GetStash().Select(x => x.Slot), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(_store.GetStash()[0].Instance, Is.EqualTo(schema == 3 ? instance : null));
            before = _store.GetStash().ToArray();
            Assert.That((await coordinator.TransferAsync(0, Sword, true, LootTransferQuantityMode.FullStack)).IsSuccess, Is.True);
            Assert.That(_store.GetLoadout().Single().Instance, Is.EqualTo(schema == 3 ? instance : null));
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 4, 1) }));
            Assert.That(_remote.Calls, Is.EqualTo(new[] { "slotMove", "order", "toLoadout:rapier:1" }));
            Assert.That(_reconciliation.Calls, Is.Zero);
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

        /// <summary>Models the backend: the same per-stack rules, in source slot order, on a scratch profile.</summary>
        private TransferAllResult SimulateBulk(bool fromStash)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            LocalProfileSnapshot scratch = repository.Snapshot.Clone();
            scratch.InventorySchemaVersion = _store.InventorySchemaVersion;
            scratch.Stash.AddRange(_store.GetStash());
            scratch.Loadout.AddRange(_store.GetLoadout());
            scratch.PreparedEquipment = _store.GetPreparedEquipment();
            Assert.That(repository.TrySave(scratch, out string error), Is.True, error);
            var backend = new LocalProfileStore(repository, _profile, _catalog);
            var results = new List<TransferAllStackResult>();
            foreach (StashItem item in (fromStash ? backend.GetStash() : backend.GetLoadout()).OrderBy(i => i.Slot).ToArray())
            {
                StashOperationResult rule = backend.TryTransfer(item.Slot, item.LootId, item.Amount, fromStash);
                results.Add(new TransferAllStackResult
                {
                    slot = item.Slot, lootId = item.LootId.Value, amount = item.Amount,
                    moved = rule == StashOperationResult.Success,
                    errorCode = rule == StashOperationResult.Success ? null
                        : rule == StashOperationResult.InventoryFull ? "INVENTORY_FULL" : "VALIDATION_FAILED"
                });
            }

            static InventoryItemData[] Map(IReadOnlyList<StashItem> items) => items.Select(item => new InventoryItemData
            {
                slot = item.Slot, lootId = item.LootId.Value, amount = item.Amount,
                instance = EquipmentInstanceData.FromInstance(item.Instance)
            }).ToArray();
            return new TransferAllResult
            {
                stash = Map(backend.GetStash()), loadout = Map(backend.GetLoadout()), results = results.ToArray(),
                revision = _store.RemoteRevision + (results.Any(r => r.moved) ? 1 : 0)
            };
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

        private StashTransferCoordinator Create(Action<LocalProfileSnapshot> configure, bool withRemote = true, bool withCatalog = false, int schemaVersion = 2)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            configure?.Invoke(snapshot);
            snapshot.InventorySchemaVersion = schemaVersion;
            PhysicalSlotTestSeed.AssignUnplaced(snapshot);
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
                endpoint,
                withCatalog ? _catalog : null);
        }

        private sealed class FakeRemote : IRemoteInventoryService, ISlotRemoteInventoryService, IRemoteBulkTransferService
        {
            public Func<bool, TransferAllResult> Simulate;
            public Func<TransferAllResult, TransferAllResult> Override;
            public Action<TransferAllResult> OnResult;

            public async Task<(bool success, TransferAllResult data, BackendError error)> TransferAllAsync(bool fromStash)
            {
                (bool success, BackendError error) = await Run($"bulk:{(fromStash ? "toLoadout" : "toStash")}");
                if (!success) return (false, default, error);
                TransferAllResult result = Simulate(fromStash);
                if (Override != null) result = Override(result);
                OnResult?.Invoke(result);
                return (true, result, default);
            }

            public Task<(bool success, BackendError error)> SplitSlotAsync(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId id) =>
                Run($"split:{list}:{sourceSlot}:{amount}:{destinationSlot}:{id.Value}");
            public Task<(bool success, BackendError error)> OrderSlotsAsync(InventoryList list, IReadOnlyList<int> slots) => Run("order");
            public Task<(bool success, BackendError error)> TransferAsync(int slot, LootId id, int amount, bool fromStash) =>
                Run($"{(fromStash ? "toLoadout" : "toStash")}:{id.Value}:{amount}");
            public Task<(bool success, BackendError error)> MoveSlotsAsync(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) => Run("slotMove");
            public Task<(bool success, BackendError error)> OrderAsync(InventoryList list, IReadOnlyList<LootId> lootIds) => Run("order");

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

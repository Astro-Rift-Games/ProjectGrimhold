#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;

namespace Tests.PlayMode.Loot
{
    /// <summary>
    /// Receiver integration contract for TASK-227. The original physical-slot cases are retained;
    /// addressed cases require the next additive request API. Reflection only inspects storage
    /// or invokes existing authority processors; no shared asset mutation or new runner is required.
    /// </summary>
    public sealed class PlayerLootReceiverSlotsPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private static readonly LootId Bone = new("bone");
        private static readonly LootId Coins = new("coins");
        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private NetworkObject _prefab;
        private PlayerLootReceiver _receiver;
        private LootDefinitionCatalog _catalog;
        private LootDefinition _boneDefinition;
        private readonly List<UnityEngine.Object> _ownedAssets = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var holder = new GameObject(nameof(PlayerLootReceiverSlotsPlayModeTests));
            _runner = holder.AddComponent<NetworkRunner>();
            holder.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
            _driver = holder.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-227-slots-{Guid.NewGuid():N}",
                SceneManager = holder.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = holder.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
                yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PlayerPrefabGuid));
            Assert.That(id.IsValid, Is.True);
            _prefab = _runner.Config.PrefabTable.Load(id, true);
            Assert.That(_prefab, Is.Not.Null);
            LootDefinitionCatalog source = _prefab.GetComponent<PlayerLootReceiver>().LootCatalog;
            // Clone real definitions to retain their validated sprite/configuration dependencies.
            // Only these in-memory clones have stack/transport limits changed for this fixture.
            _boneDefinition = CloneDefinition(source, Bone, 10);
            LootDefinition coinsDefinition = CloneDefinition(source, Coins, 1);
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _ownedAssets.Add(_catalog);
            SetField(_catalog, "_definitions", new List<LootDefinition> { _boneDefinition, coinsDefinition });
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
            Assert.That(_catalog.TryGetIndex(Bone, out int boneIndex), Is.True);
            Assert.That(boneIndex, Is.Zero, "Catalog index zero must remain a usable item.");
            _receiver = SpawnReceiver();
            yield return null;
            Assert.That(_receiver.HasStateAuthority, Is.True);
            Assert.That(_receiver.IsRaidLootOriginAware, Is.True);
            Assert.That(_receiver.SlotCapacity, Is.EqualTo(30));
            Assert.That(_receiver.OccupiedSlotCount, Is.Zero);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner.IsRunning)
                    yield return null;
            }
            if (_runner != null)
                UnityEngine.Object.DestroyImmediate(_runner.gameObject);
            foreach (UnityEngine.Object asset in _ownedAssets)
                UnityEngine.Object.DestroyImmediate(asset);
            _ownedAssets.Clear();
        }

        [UnityTest]
        public IEnumerator RepeatedReception_PartitionsAtStackMaxAndKeepsAggregateSnapshot()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 6, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 7, RaidLootOrigin.Dungeon);
            });
            Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(13));
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(2), "13 units at StackMax 10 need two physical slots.");
            NetworkArray<LootInventorySlot> slots = ReadPhysicalSlots(_receiver);
            Assert.That(slots.Length, Is.EqualTo(30));
            Assert.That(slots[0].Quantity, Is.EqualTo(10));
            Assert.That(slots[1].Quantity, Is.EqualTo(3));
            for (int index = 0; index < 2; index++)
            {
                Assert.That(slots[index].CatalogIndex, Is.Zero);
                Assert.That(slots[index].Origin, Is.EqualTo(RaidLootOrigin.Dungeon));
            }
            for (int index = 2; index < slots.Length; index++)
                Assert.That(slots[index].IsEmpty, Is.True);
            yield return Execute(() => Receive(_receiver, Bone, 7, RaidLootOrigin.Dungeon));
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(2));
            slots = ReadPhysicalSlots(_receiver);
            Assert.That(slots[1].Quantity, Is.EqualTo(10));
            Assert.That(_receiver.DistinctLootCount, Is.EqualTo(1));
            Assert.That(_receiver.GetLootContent(), Is.EqualTo(new[] { new LootEntry(Bone, 20) }));
        }

        [UnityTest]
        public IEnumerator FullInventory_RejectsOversizedTransferButAcceptsExactRemainingCapacity()
        {
            PlayerLootReceiver source = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 297, RaidLootOrigin.Dungeon);
                Receive(source, Bone, 4, RaidLootOrigin.Dungeon);
                int receiverRevision = _receiver.Revision;
                int sourceRevision = source.Revision;
                PlayerExpeditionLootSnapshot receiverBefore = Capture(_receiver);
                PlayerExpeditionLootSnapshot sourceBefore = Capture(source);
                var oversized = new LootTransferRequest(source.Id, _receiver.Id, Bone, 4, _runner.Tick);
                LootTransferResult result = LootTransferTransaction.Execute(source, _receiver, oversized);
                Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.InventoryFull),
                    "Only three units fit in the final compatible partial stack.");
                Assert.That(_receiver.Revision, Is.EqualTo(receiverRevision));
                Assert.That(source.Revision, Is.EqualTo(sourceRevision));
                Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(297));
                Assert.That(source.GetLootAmount(Bone), Is.EqualTo(4));
                Assert.That(receiverBefore.MatchesCurrent(_receiver, Equipment(_receiver), out _), Is.True);
                Assert.That(sourceBefore.MatchesCurrent(source, Equipment(source), out _), Is.True);
                var exact = new LootTransferRequest(source.Id, _receiver.Id, Bone, 3, _runner.Tick);
                Assert.That(LootTransferTransaction.Execute(source, _receiver, exact).Success, Is.True);
                Assert.That(ValidateReceive(_receiver, Bone, 1, RaidLootOrigin.Dungeon),
                    Is.EqualTo(LootTransferFailureReason.InventoryFull));
            });
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(30));
            Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(300));
            Assert.That(source.GetLootAmount(Bone), Is.EqualTo(1));
            Assert.That(Capture(_receiver).InventoryOrigins,
                Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 300) }));
            Assert.That(Capture(source).InventoryOrigins,
                Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 1) }));
        }

        [UnityTest]
        public IEnumerator MultiOriginTransfer_CompetingForLastHoleRejectsBothEndpointsAtomically()
        {
            PlayerLootReceiver source = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Coins, 29, RaidLootOrigin.Dungeon);
                Receive(source, Bone, 1, RaidLootOrigin.Dungeon);
                Receive(source, Bone, 1, PlayerOrigin(1));
                PlayerExpeditionLootSnapshot receiverBefore = Capture(_receiver);
                PlayerExpeditionLootSnapshot sourceBefore = Capture(source);
                LootInventorySlot[] receiverSlots = SnapshotSlots(_receiver);
                LootInventorySlot[] sourceSlots = SnapshotSlots(source);
                int receiverRevision = _receiver.Revision;
                int sourceRevision = source.Revision;
                var request = new LootTransferRequest(source.Id, _receiver.Id, Bone, 2, _runner.Tick);
                Assert.That(_receiver.ValidateReceive(request), Is.EqualTo(LootTransferFailureReason.None),
                    "The quantity-only upper bound cannot know the two distinct origins.");
                Assert.That(LootTransferTransaction.Execute(source, _receiver, request).FailureReason,
                    Is.EqualTo(LootTransferFailureReason.InventoryFull));
                Assert.That(receiverBefore.MatchesCurrent(_receiver, Equipment(_receiver), out _), Is.True);
                Assert.That(sourceBefore.MatchesCurrent(source, Equipment(source), out _), Is.True);
                Assert.That(_receiver.Revision, Is.EqualTo(receiverRevision));
                Assert.That(source.Revision, Is.EqualTo(sourceRevision));
                AssertSlots(_receiver, receiverSlots);
                AssertSlots(source, sourceSlots);
            });
        }

        [UnityTest]
        public IEnumerator SameItem_DungeonPlayerOneAndPlayerTwoNeverShareSlots()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 1, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 1, PlayerOrigin(1));
                Receive(_receiver, Bone, 1, PlayerOrigin(2));
            });
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(3), "Exact participant identity is part of stack compatibility.");
            yield return Execute(() =>
            {
                Receive(_receiver, Coins, 27, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 9, PlayerOrigin(1));
                Assert.That(ValidateReceive(_receiver, Bone, 10, PlayerOrigin(2)),
                    Is.EqualTo(LootTransferFailureReason.InventoryFull), "Other origins' partial stacks cannot provide capacity.");
            });
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(30));
            Assert.That(Capture(_receiver).InventoryOrigins, Is.EqualTo(new[]
            {
                new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 1),
                new RaidLootOriginEntry(Bone, PlayerOrigin(1), 10),
                new RaidLootOriginEntry(Bone, PlayerOrigin(2), 1),
                new RaidLootOriginEntry(Coins, RaidLootOrigin.Dungeon, 27)
            }));
        }

        [UnityTest]
        public IEnumerator MaxTransportable_IsSharedAcrossAllOriginsWithoutMutationOnRejection()
        {
            SetField(_boneDefinition, "_maxTransportable", 7);
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 3, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 4, PlayerOrigin(1));
                PlayerExpeditionLootSnapshot before = Capture(_receiver);
                int revision = _receiver.Revision;
                Assert.That(ValidateReceive(_receiver, Bone, 1, PlayerOrigin(2)),
                    Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                Assert.That(before.MatchesCurrent(_receiver, Equipment(_receiver), out string error), Is.True, error);
            });
            Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator OvercapacityRaidLoadout_IsRejectedAtomicallyAndCanRetryValidLoadout()
        {
            yield return Execute(() =>
            {
                int revision = _receiver.Revision;
                bool initialized = _receiver.TryInitializeRaidLoadout(
                    new[] { new LootEntry(Bone, 1), new LootEntry(Coins, 30) }, Participant(16), out string error);
                Assert.That(initialized, Is.False, "Two aggregate IDs still require 31 physical slots.");
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                Assert.That(_receiver.GetLootContent(), Is.Empty);
                Assert.That(Capture(_receiver).InventoryOrigins, Is.Empty);
                Assert.That(_receiver.TryInitializeRaidLoadout(
                    new[] { new LootEntry(Bone, 11) }, Participant(16), out error), Is.True, error);
            });
            Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(11));
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ValidRaidLoadout_SplitsStacksAndRetainsAdmittedParticipantOrigin()
        {
            yield return Execute(() => Assert.That(_receiver.TryInitializeRaidLoadout(
                new[] { new LootEntry(Bone, 11), new LootEntry(Coins, 2) }, Participant(16), out string error), Is.True, error));
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(4), "11 stackable and two nonstackable units require four slots.");
            PlayerExpeditionLootSnapshot snapshot = Capture(_receiver);
            Assert.That(snapshot.Inventory, Is.EqualTo(new[] { new LootEntry(Bone, 11), new LootEntry(Coins, 2) }));
            Assert.That(snapshot.InventoryOrigins, Is.EqualTo(new[]
            {
                new RaidLootOriginEntry(Bone, PlayerOrigin(16), 11),
                new RaidLootOriginEntry(Coins, PlayerOrigin(16), 2)
            }));
        }

        [UnityTest]
        public IEnumerator ExtractionAndExactClear_ConserveQuantitiesOriginsAndImmutableSnapshots()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 14, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 8, PlayerOrigin(1));
                Receive(_receiver, Bone, 3, PlayerOrigin(2));
                PlayerExpeditionLootSnapshot before = Capture(_receiver);
                var request = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Bone, 17, _runner.Tick);
                Assert.That(_receiver.ValidateExtraction(request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Buckets.Count, Is.EqualTo(2));
                Assert.That(transfer.Buckets[0].Origin, Is.EqualTo(RaidLootOrigin.Dungeon));
                Assert.That(transfer.Buckets[0].Amount, Is.EqualTo(14));
                Assert.That(transfer.Buckets[1].Origin, Is.EqualTo(PlayerOrigin(1)));
                Assert.That(transfer.Buckets[1].Amount, Is.EqualTo(3));
                _receiver.CommitRaidLootExtraction(request, transfer);
                Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(8));
                Assert.That(before.Inventory, Is.EqualTo(new[] { new LootEntry(Bone, 25) }));
                Assert.That(before.InventoryOrigins[0].Amount, Is.EqualTo(14));
                int revision = _receiver.Revision;
                Assert.That(before.TryClearExact(_receiver, Equipment(_receiver), out _), Is.False);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                PlayerExpeditionLootSnapshot remaining = Capture(_receiver);
                Assert.That(remaining.InventoryOrigins, Is.EqualTo(new[]
                {
                    new RaidLootOriginEntry(Bone, PlayerOrigin(1), 5),
                    new RaidLootOriginEntry(Bone, PlayerOrigin(2), 3)
                }));
                Assert.That(remaining.TryClearExact(_receiver, Equipment(_receiver), out string error), Is.True, error);
                Assert.That(_receiver.Revision, Is.EqualTo(revision + 1));
                Assert.That(_receiver.GetLootContent(), Is.Empty);
                Assert.That(Capture(_receiver).InventoryOrigins, Is.Empty);
                Assert.That(remaining.Inventory, Is.EqualTo(new[] { new LootEntry(Bone, 8) }));
            });
            Assert.That(_receiver.OccupiedSlotCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InvalidLateExtractionBucket_DoesNotCommitEarlierBucketOrQuantity()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 1, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 1, PlayerOrigin(1));
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                var request = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Bone, 2, _runner.Tick);
                var invalid = new RaidLootOriginTransfer(new[]
                {
                    new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 1),
                    new RaidLootOriginBucket(PlayerOrigin(2), 1)
                });
                Assert.That(_receiver.ValidateExtraction(request), Is.EqualTo(LootTransferFailureReason.None));
                LogAssert.Expect(LogType.Error,
                    "PlayerLootReceiver: CommitRaidLootExtraction contract was violated after successful validation.");
                Assert.Throws<InvalidOperationException>(() => _receiver.CommitRaidLootExtraction(request, invalid));
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                Assert.That(_receiver.GetLootAmount(Bone), Is.EqualTo(2));
                AssertSlots(_receiver, before);
            });
        }

        [UnityTest]
        public IEnumerator LowerGameplayCapacity_DoesNotResizeOrRestrictPhysicalAddresses()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Coins, 1, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 3, RaidLootOrigin.Dungeon);
                var extraction = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Coins, 1, _runner.Tick);
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(extraction, out RaidLootOriginTransfer transfer), Is.True);
                _receiver.CommitRaidLootExtraction(extraction, transfer);
                SetField(_receiver, "_slotCapacity", 1);
                Receive(_receiver, Bone, 7, RaidLootOrigin.Dungeon);
                Assert.That(ValidateReceive(_receiver, Bone, 1, PlayerOrigin(1)), Is.EqualTo(LootTransferFailureReason.InventoryFull));
                Assert.That(_receiver.TryGetInventorySlot(0, out LootInventorySlot empty), Is.True);
                Assert.That(empty.IsEmpty, Is.True);
                Assert.That(_receiver.TryGetInventorySlot(1, out LootInventorySlot occupied), Is.True);
                Assert.That(occupied.Quantity, Is.EqualTo(10));
                Assert.That(ReadPhysicalSlots(_receiver).Length, Is.EqualTo(30));
                Assert.That(_receiver.TryGetInventorySlot(-1, out _), Is.False);
                Assert.That(_receiver.TryGetInventorySlot(30, out _), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator PlayerFirstResolution_SpendsPlayersInIdOrderBeforeDungeon()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 4, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 2, PlayerOrigin(2));
                Receive(_receiver, Bone, 1, PlayerOrigin(1));
                var request = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Bone, 4, _runner.Tick);
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, RaidLootOriginSelectionOrder.PlayerFirst,
                    out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Buckets.Count, Is.EqualTo(3));
                Assert.That(transfer.Buckets[0].Origin, Is.EqualTo(RaidLootOrigin.Dungeon));
                Assert.That(transfer.Buckets[0].Amount, Is.EqualTo(1));
                Assert.That(transfer.Buckets[1].Origin, Is.EqualTo(PlayerOrigin(1)));
                Assert.That(transfer.Buckets[1].Amount, Is.EqualTo(1));
                Assert.That(transfer.Buckets[2].Origin, Is.EqualTo(PlayerOrigin(2)));
                Assert.That(transfer.Buckets[2].Amount, Is.EqualTo(2));
                _receiver.CommitRaidLootExtraction(request, transfer);
                Assert.That(Capture(_receiver).InventoryOrigins,
                    Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 3) }));
            });
        }

        [UnityTest]
        public IEnumerator Reception_FillsCompatiblePartialBeforeHoleWithoutCompacting()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Coins, 1, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 3, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 1, PlayerOrigin(1));
            });
            // Behavioral RED on the old dictionary occurs before any structural inspection.
            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(3));
            yield return Execute(() =>
            {
                // Coins have only one unit, so making this hole imposes no same-origin extraction order.
                var request = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Coins, 1, _runner.Tick);
                Assert.That(_receiver.ValidateExtraction(request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
                _receiver.CommitRaidLootExtraction(request, transfer);
                NetworkArray<LootInventorySlot> slots = ReadPhysicalSlots(_receiver);
                Assert.That(slots.Length, Is.EqualTo(30));
                Assert.That(slots[0].IsEmpty, Is.True);
                Assert.That(slots[1].CatalogIndex, Is.Zero);
                Assert.That(slots[1].Quantity, Is.EqualTo(3));
                LootInventorySlot unrelated = slots[2];
                Assert.That(unrelated.Origin, Is.EqualTo(PlayerOrigin(1)));
                Assert.That(unrelated.Quantity, Is.EqualTo(1));
                Receive(_receiver, Bone, 5, RaidLootOrigin.Dungeon);
                Assert.That(slots[0].IsEmpty, Is.True, "A compatible partial stack is filled before an earlier hole.");
                Assert.That(slots[1].Quantity, Is.EqualTo(8));
                Assert.That(slots[2], Is.EqualTo(unrelated), "An incompatible occupied slot must not move or merge.");
            });
        }

        [UnityTest]
        public IEnumerator BehaviourCopyStateFrom_PreservesSlotsOriginsAndRevisionAcrossSpawnedReplay()
        {
            PlayerLootReceiver restored = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Coins, 1, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 2, PlayerOrigin(16));
                Receive(_receiver, Coins, 2, PlayerOrigin(1));
                var extraction = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Coins, 1, _runner.Tick);
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(extraction, out RaidLootOriginTransfer transfer), Is.True);
                _receiver.CommitRaidLootExtraction(extraction, transfer);
                LootInventorySlot[] expectedSlots = SnapshotSlots(_receiver);
                Assert.That(expectedSlots[0].IsEmpty, Is.True, "Keep a leading hole that repacking would destroy.");
                Assert.That(expectedSlots[1].Quantity, Is.EqualTo(10));
                Assert.That(expectedSlots[2].Quantity, Is.EqualTo(3));
                PlayerExpeditionLootSnapshot snapshot = Capture(_receiver);
                Receive(restored, Bone, 1, PlayerOrigin(2));
                // Real Fusion behaviour-storage copy. Single runner cannot prove migration resume,
                // reference fixups or Host + two Client recovery. Equipment is empty in this case.
                restored.CopyStateFrom(_receiver);
                restored.GetComponent<PlayerRaidLootOriginState>().CopyStateFrom(
                    _receiver.GetComponent<PlayerRaidLootOriginState>());
                AssertSlots(restored, expectedSlots);
                // Release only this receiver registration before replaying the real callback;
                // otherwise Spawned's registry collision would obscure the inventory assertion.
                Assert.That(_runner.GetComponent<EntityRegistry>().TryUnregisterLootReceiver(restored.Id, restored), Is.True);
                restored.Spawned();
                AssertSlots(restored, expectedSlots);
                AssertSlots(_receiver, expectedSlots);
                Assert.That(restored.Revision, Is.EqualTo(_receiver.Revision));
                Assert.That(snapshot.MatchesCurrent(restored, Equipment(restored), out string error), Is.True, error);
            });
            Assert.That(restored.OccupiedSlotCount, Is.EqualTo(5), "Copied inventory must retain all physical stacks.");
            Assert.That(restored.DistinctLootCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AddressedFullStack_SecondSameOriginStackLeavesFirstUntouched()
        {
            PlayerLootReceiver destination = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                Receive(destination, Bone, 1, RaidLootOrigin.Dungeon);
                LootInventorySlot[] expected = SnapshotSlots(_receiver);
                int sourceRevision = _receiver.Revision;
                int destinationRevision = destination.Revision;
                Assert.That(_receiver.ResolveInventorySlotExtraction(destination.Id, Bone, LootTransferQuantityMode.FullStack,
                    1, sourceRevision, _runner.Tick, out LootTransferRequest request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(request.SourceInventorySlotIndex, Is.EqualTo(1));
                Assert.That(request.ExpectedInventoryRevision, Is.EqualTo(sourceRevision));
                Assert.That(request.RequestedAmount, Is.EqualTo(3), "FullStack is the selected physical stack, not all 13 units.");
                Assert.That(LootTransferTransaction.Execute(_receiver, destination, request).Success, Is.True);
                expected[1] = default;
                AssertSlots(_receiver, expected);
                Assert.That(_receiver.Revision, Is.EqualTo(sourceRevision + 1));
                Assert.That(destination.Revision, Is.EqualTo(destinationRevision + 1));
                Assert.That(Capture(_receiver).InventoryOrigins,
                    Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 10) }));
                Assert.That(Capture(destination).InventoryOrigins,
                    Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, RaidLootOrigin.Dungeon, 4) }));
                Assert.That(destination.TryGetInventorySlot(0, out LootInventorySlot filled), Is.True);
                Assert.That(filled.Quantity, Is.EqualTo(4), "Insertion still fills the authoritative compatible partial stack.");
            });
        }

        [UnityTest]
        public IEnumerator AddressedSingleUnit_UsesSelectedOriginInsteadOfCanonicalFirstOrigin()
        {
            PlayerLootReceiver destination = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 2, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 2, PlayerOrigin(1));
                Receive(_receiver, Bone, 2, PlayerOrigin(2));
                LootInventorySlot[] expected = SnapshotSlots(_receiver);
                Assert.That(_receiver.ResolveInventorySlotExtraction(destination.Id, Bone, LootTransferQuantityMode.SingleUnit,
                    2, _receiver.Revision, _runner.Tick, out LootTransferRequest request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(request.RequestedAmount, Is.EqualTo(1));
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Buckets.Count, Is.EqualTo(1));
                Assert.That(transfer.Buckets[0].Origin, Is.EqualTo(PlayerOrigin(2)));
                Assert.That(transfer.Buckets[0].Amount, Is.EqualTo(1));
                Assert.That(LootTransferTransaction.Execute(_receiver, destination, request).Success, Is.True);
                Assert.That(LootInventorySlot.TryCreate(0, 1, PlayerOrigin(2), out expected[2]), Is.True);
                AssertSlots(_receiver, expected);
                Assert.That(Capture(destination).InventoryOrigins,
                    Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, PlayerOrigin(2), 1) }));
            });
        }

        [UnityTest]
        public IEnumerator AddressedExtraction_RejectsStaleCatalogEmptyAndInvalidAddressesWithoutFallback()
        {
            PlayerLootReceiver destination = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                int staleRevision = _receiver.Revision;
                Receive(_receiver, Coins, 1, PlayerOrigin(1));
                int revision = _receiver.Revision;
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                var requests = new[]
                {
                    new LootTransferRequest(_receiver.Id, destination.Id, Bone, 3, _runner.Tick, 1, staleRevision),
                    new LootTransferRequest(_receiver.Id, destination.Id, Coins, 1, _runner.Tick, 1, revision),
                    new LootTransferRequest(_receiver.Id, destination.Id, Bone, 1, _runner.Tick, 3, revision),
                    new LootTransferRequest(_receiver.Id, destination.Id, Bone, 1, _runner.Tick, -2, revision),
                    new LootTransferRequest(_receiver.Id, destination.Id, Bone, 1, _runner.Tick, 30, revision),
                    new LootTransferRequest(_receiver.Id, destination.Id, Bone, 4, _runner.Tick, 1, revision)
                };
                var failures = new[]
                {
                    LootTransferFailureReason.ContainerUnavailable, LootTransferFailureReason.InvalidLoot,
                    LootTransferFailureReason.InsufficientAmount, LootTransferFailureReason.InvalidLoot,
                    LootTransferFailureReason.InvalidLoot, LootTransferFailureReason.InsufficientAmount
                };
                for (int index = 0; index < requests.Length; index++)
                {
                    Assert.That(_receiver.ValidateExtraction(requests[index]), Is.EqualTo(failures[index]));
                    Assert.That(_receiver.TryResolveRaidLootOriginTransfer(requests[index], out _), Is.False);
                    Assert.That(LootTransferTransaction.Execute(_receiver, destination, requests[index]).FailureReason, Is.EqualTo(failures[index]));
                    AssertSlots(_receiver, before);
                    Assert.That(_receiver.Revision, Is.EqualTo(revision));
                    Assert.That(destination.GetLootContent(), Is.Empty);
                }
                Assert.That(_receiver.ResolveInventorySlotExtraction(destination.Id, Bone, LootTransferQuantityMode.FullStack,
                    1, staleRevision, _runner.Tick, out LootTransferRequest rejected), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
                Assert.That(rejected.IsValid, Is.False, "Stale input must not create a retargeted canonical request.");
            });
        }

        [UnityTest]
        public IEnumerator AddressedTransfer_DestinationFailurePreservesSlotsOriginsAndRevisions()
        {
            PlayerLootReceiver destination = SpawnReceiver();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, PlayerOrigin(2));
                Receive(destination, Coins, 30, RaidLootOrigin.Dungeon);
                PlayerExpeditionLootSnapshot sourceBefore = Capture(_receiver);
                PlayerExpeditionLootSnapshot destinationBefore = Capture(destination);
                LootInventorySlot[] sourceSlots = SnapshotSlots(_receiver);
                LootInventorySlot[] destinationSlots = SnapshotSlots(destination);
                int sourceRevision = _receiver.Revision;
                int destinationRevision = destination.Revision;
                Assert.That(_receiver.ResolveInventorySlotExtraction(destination.Id, Bone, LootTransferQuantityMode.FullStack,
                    1, sourceRevision, _runner.Tick, out LootTransferRequest request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(LootTransferTransaction.Execute(_receiver, destination, request).FailureReason,
                    Is.EqualTo(LootTransferFailureReason.InventoryFull));
                Assert.That(sourceBefore.MatchesCurrent(_receiver, Equipment(_receiver), out _), Is.True);
                Assert.That(destinationBefore.MatchesCurrent(destination, Equipment(destination), out _), Is.True);
                AssertSlots(_receiver, sourceSlots);
                AssertSlots(destination, destinationSlots);
                Assert.That(_receiver.Revision, Is.EqualTo(sourceRevision));
                Assert.That(destination.Revision, Is.EqualTo(destinationRevision));
            });
        }

        [UnityTest]
        public IEnumerator AddressedDepositAuthority_FullStackIsSelectedSlotNotItemAggregate()
        {
            NetworkLootContainer container = SpawnAddressedDepositContainer();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, PlayerOrigin(2));
                LootInventorySlot[] expected = SnapshotSlots(_receiver);
                var identity = new LootTransferRequestIdentity(1, _receiver.Id, container.Id, 0,
                    LootTransferQuantityMode.FullStack, 1, _receiver.Revision);
                LootTransferConfirmation confirmation = InvokeAuthority<LootTransferConfirmation>(
                    _receiver.GetComponent<PlayerLootTransferNetworkController>(), identity);
                Assert.That(confirmation.Result.Success, Is.True);
                Assert.That(confirmation.Result.TransferredAmount, Is.EqualTo(3));
                expected[1] = default;
                AssertSlots(_receiver, expected);
                Assert.That(container.GetLootAmount(Bone), Is.EqualTo(3));
                Assert.That(container.TryGetRaidLootOriginEntries(out IReadOnlyList<RaidLootOriginEntry> origins), Is.True);
                Assert.That(origins, Is.EqualTo(new[] { new RaidLootOriginEntry(Bone, PlayerOrigin(2), 3) }));
            });
        }

        [UnityTest]
        public IEnumerator AddressedDepositAuthority_RejectsStaleAndMismatchedPayloadsWithoutRetargeting()
        {
            NetworkLootContainer container = SpawnAddressedDepositContainer();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                int staleRevision = _receiver.Revision;
                Receive(_receiver, Coins, 1, RaidLootOrigin.Dungeon);
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                var stale = new LootTransferRequestIdentity(1, _receiver.Id, container.Id, 0,
                    LootTransferQuantityMode.FullStack, 1, staleRevision);
                var mismatched = new LootTransferRequestIdentity(2, _receiver.Id, container.Id, 1,
                    LootTransferQuantityMode.FullStack, 1, _receiver.Revision);
                var controller = _receiver.GetComponent<PlayerLootTransferNetworkController>();
                Assert.That(InvokeAuthority<LootTransferConfirmation>(controller, stale).Result.FailureReason,
                    Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
                Assert.That(InvokeAuthority<LootTransferConfirmation>(controller, mismatched).Result.FailureReason,
                    Is.EqualTo(LootTransferFailureReason.InvalidLoot));
                AssertSlots(_receiver, before);
                Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
                Assert.That(content, Is.Empty);
            });
        }

        [UnityTest]
        public IEnumerator AddressedPublicSenders_RequireInputAuthorityAndLeaveInventoryUntouched()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                var transfer = _receiver.GetComponent<PlayerLootTransferNetworkController>();
                var drop = _receiver.GetComponent<PlayerLootDropNetworkController>();
                Assert.That(_receiver.Object.HasInputAuthority, Is.False);
                Assert.That(transfer.TryRequestTransfer(_receiver.Id, new EntityId(int.MaxValue), Bone,
                    LootTransferQuantityMode.FullStack, 1, revision), Is.False);
                Assert.That(drop.TryRequestDrop(Bone, LootTransferQuantityMode.FullStack, 1, revision), Is.False);
                Assert.That(transfer.HasRequestInFlight, Is.False);
                Assert.That(drop.HasRequestInFlight, Is.False);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                AssertSlots(_receiver, before);
            });
        }

        [UnityTest]
        public IEnumerator AddressedDropAuthority_RejectsStaleIntentBeforeWorldPlacement()
        {
            yield return Execute(() =>
            {
                // Use the productive catalog shared by the existing drop controller/pickup.
                // The stale rejection must precede placement/spawning, so this needs no geometry setup.
                LootDefinitionCatalog catalog = _prefab.GetComponent<PlayerLootReceiver>().LootCatalog;
                SetField(_receiver, "_lootCatalog", catalog);
                Assert.That(catalog.TryGet(Bone.Value, out LootDefinition definition), Is.True);
                Receive(_receiver, Bone, checked(definition.StackMax + 1), PlayerOrigin(1));
                int staleRevision = _receiver.Revision;
                Assert.That(catalog.TryGetIndex(Bone, out int catalogIndex), Is.True);
                var identity = new LootDropRequestIdentity(1, catalogIndex, LootTransferQuantityMode.FullStack, 1, staleRevision);
                Receive(_receiver, Coins, 1, RaidLootOrigin.Dungeon);
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                LootDropConfirmation confirmation = InvokeAuthority<LootDropConfirmation>(
                    _receiver.GetComponent<PlayerLootDropNetworkController>(), identity);
                Assert.That(confirmation.Result.FailureReason, Is.EqualTo(LootDropFailureReason.PlayerUnavailable));
                AssertSlots(_receiver, before);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
            });
        }

        [UnityTest]
        public IEnumerator AddressedDropAuthority_FullStackPublishesOnlySelectedStackAndOrigin()
        {
            yield return Execute(() =>
            {
                // Retain productive pickup references and placement settings rather than
                // inventing a pickup prefab, collision mask, or serialized configuration.
                LootDefinitionCatalog catalog = _prefab.GetComponent<PlayerLootReceiver>().LootCatalog;
                SetField(_receiver, "_lootCatalog", catalog);
                Assert.That(catalog.TryGet(Bone.Value, out LootDefinition definition), Is.True);
                Assert.That(catalog.TryGetIndex(Bone, out int catalogIndex), Is.True);
                Receive(_receiver, Bone, definition.StackMax, RaidLootOrigin.Dungeon);
                Receive(_receiver, Bone, 1, PlayerOrigin(2));
                LootInventorySlot[] expected = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                var identity = new LootDropRequestIdentity(1, catalogIndex, LootTransferQuantityMode.FullStack, 1, revision);
                LootDropConfirmation confirmation = InvokeAuthority<LootDropConfirmation>(
                    _receiver.GetComponent<PlayerLootDropNetworkController>(), identity);
                Assert.That(confirmation.Result.Success, Is.True,
                    $"Productive drop placement/publication failed: {confirmation.Result.FailureReason}.");
                Assert.That(confirmation.Result.DroppedAmount, Is.EqualTo(1));
                expected[1] = default;
                AssertSlots(_receiver, expected);
                Assert.That(_receiver.Revision, Is.EqualTo(revision + 1));
                int published = 0;
                foreach (NetworkLootPickup pickup in UnityEngine.Object.FindObjectsByType<NetworkLootPickup>(FindObjectsSortMode.None))
                {
                    if (pickup.Object == null || !pickup.Object.IsValid || pickup.Runner != _runner) continue;
                    published++;
                    Assert.That(pickup.IsAvailable, Is.True);
                    Assert.That(pickup.LootDefinition, Is.SameAs(definition));
                    Assert.That(pickup.Amount, Is.EqualTo(1));
                    Assert.That(pickup.TryResolveRaidOriginTransfer(out RaidLootOriginTransfer transfer), Is.True);
                    Assert.That(transfer.Buckets.Count, Is.EqualTo(1));
                    Assert.That(transfer.Buckets[0].Origin, Is.EqualTo(PlayerOrigin(2)));
                    Assert.That(transfer.Buckets[0].Amount, Is.EqualTo(1));
                }
                Assert.That(published, Is.EqualTo(1));
            });
        }

        [UnityTest]
        public IEnumerator AddressedEquipmentPreflight_OnlySelectedSlotMayFreeSpace()
        {
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                var trim = new LootTransferRequest(_receiver.Id, _receiver.Id, Bone, 9, _runner.Tick);
                _receiver.CommitRaidLootExtraction(trim, RaidLootOriginTransfer.Dungeon(9));
                // Same-origin slots now contain 1 and 3. Spending from slot 1 leaves both
                // occupied; spending the canonical first unit would incorrectly free space.
                SetField(_receiver, "_slotCapacity", 2);
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                var incoming = new LootEntry?[] { new LootEntry(Coins, 1) };
                var origins = new[] { RaidLootOrigin.Dungeon };
                var selectedSecond = new LootTransferRequest(_receiver.Id, _receiver.Id, Bone, 1, _runner.Tick, 1, revision);
                var selectedFirst = new LootTransferRequest(_receiver.Id, _receiver.Id, Bone, 1, _runner.Tick, 0, revision);
                Assert.That(_receiver.CanApplyInventoryExchange(selectedSecond, RaidLootOriginTransfer.Dungeon(1),
                    incoming, origins), Is.False);
                Assert.That(_receiver.CanApplyInventoryExchange(selectedFirst, RaidLootOriginTransfer.Dungeon(1),
                    incoming, origins), Is.True);
                Assert.That(RaidLootOriginTransfer.TryCreate(PlayerOrigin(2), 1, out RaidLootOriginTransfer wrongOrigin), Is.True);
                Assert.That(_receiver.CanApplyInventoryExchange(selectedFirst, wrongOrigin, incoming, origins), Is.False);
                AssertSlots(_receiver, before);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
            });
        }

        [UnityTest]
        public IEnumerator AddressedTransferAuthority_RejectsContainerSourceMetadataWithoutMutation()
        {
            NetworkLootContainer container = SpawnAddressedDepositContainer();
            yield return Execute(() =>
            {
                Receive(_receiver, Bone, 13, RaidLootOrigin.Dungeon);
                LootInventorySlot[] before = SnapshotSlots(_receiver);
                int revision = _receiver.Revision;
                int containerRevision = container.LootChangeSequence;
                var withdrawal = new LootTransferRequestIdentity(1, container.Id, _receiver.Id, 0,
                    LootTransferQuantityMode.FullStack, 1, revision);
                LootTransferConfirmation confirmation = InvokeAuthority<LootTransferConfirmation>(
                    _receiver.GetComponent<PlayerLootTransferNetworkController>(), withdrawal);
                Assert.That(confirmation.Result.FailureReason, Is.EqualTo(LootTransferFailureReason.MissingAuthority));
                AssertSlots(_receiver, before);
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                Assert.That(container.LootChangeSequence, Is.EqualTo(containerRevision));
                Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
                Assert.That(content, Is.Empty);
            });
        }

        private NetworkLootContainer SpawnAddressedDepositContainer()
        {
            const string containerGuid = "2c19a78647c64b84a765ff0280706b7d";
            NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(containerGuid));
            Assert.That(prefabId.IsValid, Is.True);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            bool initialized = false;
            NetworkObject instance = _runner.Spawn(prefab, Vector3.right, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (runner, spawned) =>
                {
                    var container = spawned.GetComponent<NetworkLootContainer>();
                    SetField(container, "_lootCatalog", _catalog);
                    initialized = container.TrySetInitialContentOverride(runner, spawned, Array.Empty<LootEntry>());
                });
            Assert.That(initialized, Is.True);
            SetField(_receiver.GetComponent<PlayerLootTransferNetworkController>(), "_lootCatalog", _catalog);
            Physics2D.SyncTransforms();
            return instance.GetComponent<NetworkLootContainer>();
        }

        private static T InvokeAuthority<T>(NetworkBehaviour controller, object identity)
        {
            // Exercise the real existing authority processor inside Execute's Fusion tick.
            // This intentionally does not claim multi-peer RPC delivery or client UI coverage.
            MethodInfo process = controller.GetType().GetMethod("ProcessAuthoritativeRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            return (T)process.Invoke(controller, new[] { identity });
        }

        private PlayerLootReceiver SpawnReceiver()
        {
            NetworkObject avatar = _runner.Spawn(_prefab, Vector3.zero, Quaternion.identity,
                inputAuthority: null, onBeforeSpawned: (_, instance) =>
                    SetField(instance.GetComponent<PlayerLootReceiver>(), "_lootCatalog", _catalog));
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            return avatar.GetComponent<PlayerLootReceiver>();
        }

        private IEnumerator Execute(Action action)
        {
            _driver.ClearException();
            _driver.PendingAction = _ => action();
            int frames = 120;
            while (_driver.PendingAction != null && frames-- > 0)
                yield return null;
            Assert.That(_driver.PendingAction, Is.Null, "No authoritative simulation tick executed the test action.");
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private void Receive(PlayerLootReceiver receiver, LootId lootId, int amount, RaidLootOrigin origin)
        {
            Assert.That(ValidateReceive(receiver, lootId, amount, origin), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(RaidLootOriginTransfer.TryCreate(origin, amount, out RaidLootOriginTransfer transfer), Is.True);
            var request = new LootTransferRequest(new EntityId(int.MaxValue), receiver.Id, lootId, amount, _runner.Tick);
            receiver.CommitRaidLootReceive(request, transfer);
        }

        private LootTransferFailureReason ValidateReceive(PlayerLootReceiver receiver, LootId lootId, int amount, RaidLootOrigin origin)
        {
            var request = new LootTransferRequest(new EntityId(int.MaxValue), receiver.Id, lootId, amount, _runner.Tick);
            LootTransferFailureReason failure = receiver.ValidateReceive(request);
            if (failure != LootTransferFailureReason.None)
                return failure;
            Assert.That(RaidLootOriginTransfer.TryCreate(origin, amount, out RaidLootOriginTransfer transfer), Is.True);
            return receiver.ValidateRaidLootOriginReceive(request, transfer);
        }

        private static PlayerWeaponEquipmentNetworkController Equipment(PlayerLootReceiver receiver) =>
            receiver.GetComponent<PlayerWeaponEquipmentNetworkController>();

        private static PlayerExpeditionLootSnapshot Capture(PlayerLootReceiver receiver)
        {
            Assert.That(PlayerExpeditionLootSnapshot.TryCapture(receiver, Equipment(receiver), out PlayerExpeditionLootSnapshot snapshot,
                out string error), Is.True, error);
            return snapshot;
        }

        private LootDefinition CloneDefinition(LootDefinitionCatalog source, LootId id, int stackMax)
        {
            Assert.That(source.TryGet(id.Value, out LootDefinition original), Is.True, id.Value);
            LootDefinition clone = UnityEngine.Object.Instantiate(original);
            _ownedAssets.Add(clone);
            SetField(clone, "_stackMax", stackMax);
            SetField(clone, "_maxTransportable", 0);
            return clone;
        }

        private static RaidParticipantId Participant(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId participant), Is.True);
            return participant;
        }

        private static RaidLootOrigin PlayerOrigin(int value)
        {
            Assert.That(RaidLootOrigin.TryCreatePlayer(Participant(value), out RaidLootOrigin origin), Is.True);
            return origin;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static LootInventorySlot[] SnapshotSlots(PlayerLootReceiver receiver)
        {
            var slots = new LootInventorySlot[30];
            for (int index = 0; index < slots.Length; index++)
                Assert.That(receiver.TryGetInventorySlot(index, out slots[index]), Is.True);
            return slots;
        }

        private static void AssertSlots(PlayerLootReceiver receiver, IReadOnlyList<LootInventorySlot> expected)
        {
            Assert.That(expected.Count, Is.EqualTo(30));
            for (int index = 0; index < expected.Count; index++)
            {
                Assert.That(receiver.TryGetInventorySlot(index, out LootInventorySlot actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected[index]), $"Physical slot {index} changed.");
            }
        }

        private static NetworkArray<LootInventorySlot> ReadPhysicalSlots(PlayerLootReceiver receiver)
        {
            foreach (PropertyInfo property in typeof(PlayerLootReceiver).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.PropertyType == typeof(NetworkArray<LootInventorySlot>))
                    return (NetworkArray<LootInventorySlot>)property.GetValue(receiver);
            }
            Assert.Fail("PlayerLootReceiver must replicate its physical slots as NetworkArray<LootInventorySlot>.");
            return default;
        }
    }
}
#endif

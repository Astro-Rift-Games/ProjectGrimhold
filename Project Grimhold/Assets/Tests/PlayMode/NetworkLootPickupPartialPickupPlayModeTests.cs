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

namespace Tests.PlayMode.Loot
{
    /// <summary>
    /// Authoritative world pickup reception for TASK-228 (Doc 07 sections 11, 14 and 28).
    /// A single runner proves State Authority outcomes only, not multi-peer replication.
    /// </summary>
    public sealed class NetworkLootPickupPartialPickupPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string LootPickupGuid = "5d26f13a358d7894b9419465e4ba1869";
        private static readonly LootId Bone = new("bone");
        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private NetworkObject _playerPrefab;
        private NetworkObject _pickupPrefab;
        private LootDefinitionCatalog _catalog;
        private readonly List<UnityEngine.Object> _ownedAssets = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var holder = new GameObject(nameof(NetworkLootPickupPartialPickupPlayModeTests));
            _runner = holder.AddComponent<NetworkRunner>();
            holder.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
            _driver = holder.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-228-pickup-{Guid.NewGuid():N}",
                SceneManager = holder.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = holder.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
                yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());

            _playerPrefab = Load(PlayerPrefabGuid);
            _pickupPrefab = Load(LootPickupGuid);
            LootDefinitionCatalog source = _playerPrefab.GetComponent<PlayerLootReceiver>().LootCatalog;
            // Clone the real definition so only this fixture's in-memory limits change.
            Assert.That(source.TryGet(Bone.Value, out LootDefinition original), Is.True);
            LootDefinition bone = UnityEngine.Object.Instantiate(original);
            _ownedAssets.Add(bone);
            SetField(bone, "_stackMax", 10);
            SetField(bone, "_maxTransportable", 0);
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _ownedAssets.Add(_catalog);
            SetField(_catalog, "_definitions", new List<LootDefinition> { bone });
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
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
        public IEnumerator FullCapacity_TransfersWholeStackAndDespawnsPickup()
        {
            PlayerLootReceiver receiver = SpawnReceiver();
            NetworkLootPickup pickup = SpawnPickup(RaidLootOriginTransfer.Dungeon(10));
            yield return null;

            InteractionResult result = default;
            yield return Execute(() => result = Interact(pickup, receiver));
            yield return null;

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsConsumed, Is.True);
            Assert.That(receiver.GetLootAmount(Bone), Is.EqualTo(10));
            Assert.That(pickup == null || pickup.Object == null || !pickup.Object.IsValid, Is.True,
                "A completely transferred pickup must despawn.");
        }

        [UnityTest]
        public IEnumerator PartialCapacity_TransfersAdmissibleUnitsAndKeepsRemainderInWorld()
        {
            PlayerLootReceiver receiver = SpawnReceiver();
            yield return LimitToOneSlotHolding(receiver, 7);
            NetworkLootPickup pickup = SpawnPickup(RaidLootOriginTransfer.Dungeon(10));
            yield return null;

            InteractionResult result = default;
            yield return Execute(() => result = Interact(pickup, receiver));
            yield return null;

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsConsumed, Is.False, "A partially collected pickup remains in the world.");
            Assert.That(receiver.GetLootAmount(Bone), Is.EqualTo(10));
            Assert.That(pickup.Object != null && pickup.Object.IsValid, Is.True);
            Assert.That(pickup.IsAvailable, Is.True);
            Assert.That(pickup.Amount, Is.EqualTo(7));
            Assert.That(pickup.FirstAcquisitionEligibleAmount, Is.EqualTo(7),
                "Only the collected units consume first-acquisition eligibility.");
            AssertOrigins(pickup, new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 7));
        }

        [UnityTest]
        public IEnumerator ZeroCapacity_RejectsWithoutChangingPickup()
        {
            PlayerLootReceiver receiver = SpawnReceiver();
            yield return LimitToOneSlotHolding(receiver, 10);
            NetworkLootPickup pickup = SpawnPickup(RaidLootOriginTransfer.Dungeon(10));
            yield return null;

            InteractionResult result = default;
            yield return Execute(() => result = Interact(pickup, receiver));
            yield return null;

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.LootRejected));
            Assert.That(result.LootFailureReason, Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(receiver.GetLootAmount(Bone), Is.EqualTo(10));
            Assert.That(pickup.IsAvailable, Is.True);
            Assert.That(pickup.Amount, Is.EqualTo(10));
            Assert.That(pickup.FirstAcquisitionEligibleAmount, Is.EqualTo(10));
            AssertOrigins(pickup, new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 10));
        }

        [UnityTest]
        public IEnumerator MixedOriginPartial_KeepsRemainderOriginsExact()
        {
            PlayerLootReceiver receiver = SpawnReceiver();
            yield return LimitToOneSlotHolding(receiver, 7);
            var origins = new RaidLootOriginTransfer(new[]
            {
                new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 2),
                new RaidLootOriginBucket(PlayerOrigin(3), 4)
            });
            NetworkLootPickup pickup = SpawnPickup(origins, eligibleAmount: 0);
            yield return null;

            InteractionResult result = default;
            yield return Execute(() => result = Interact(pickup, receiver));
            yield return null;

            // The only slot accepts the Dungeon units; Player units cannot merge into it.
            Assert.That(result.Success, Is.True);
            Assert.That(receiver.GetLootAmount(Bone), Is.EqualTo(9));
            Assert.That(pickup.Amount, Is.EqualTo(4));
            AssertOrigins(pickup, new RaidLootOriginBucket(PlayerOrigin(3), 4));
        }

        [UnityTest]
        public IEnumerator SequentialCompetitors_SecondTakesRemainderWithoutDuplication()
        {
            PlayerLootReceiver first = SpawnReceiver();
            PlayerLootReceiver second = SpawnReceiver();
            yield return LimitToOneSlotHolding(first, 7);
            NetworkLootPickup pickup = SpawnPickup(RaidLootOriginTransfer.Dungeon(10));
            yield return null;

            InteractionResult firstResult = default;
            InteractionResult secondResult = default;
            yield return Execute(() =>
            {
                firstResult = Interact(pickup, first);
                secondResult = Interact(pickup, second);
            });
            yield return null;

            Assert.That(firstResult.Success, Is.True);
            Assert.That(secondResult.Success, Is.True);
            Assert.That(secondResult.IsConsumed, Is.True);
            Assert.That(first.GetLootAmount(Bone), Is.EqualTo(10));
            Assert.That(second.GetLootAmount(Bone), Is.EqualTo(7));
            Assert.That(pickup == null || pickup.Object == null || !pickup.Object.IsValid, Is.True);
        }

        private InteractionResult Interact(NetworkLootPickup pickup, PlayerLootReceiver receiver) =>
            pickup.Interact(new InteractionRequest(receiver.Id, pickup.Id, _runner.Tick));

        private IEnumerator LimitToOneSlotHolding(PlayerLootReceiver receiver, int amount)
        {
            SetField(receiver, "_slotCapacity", 1);
            yield return Execute(() =>
            {
                var request = new LootTransferRequest(new EntityId(int.MaxValue), receiver.Id, Bone, amount, _runner.Tick);
                RaidLootOriginTransfer origin = RaidLootOriginTransfer.Dungeon(amount);
                Assert.That(receiver.ValidateRaidLootOriginReceive(request, origin), Is.EqualTo(LootTransferFailureReason.None));
                receiver.CommitRaidLootReceive(request, origin);
            });
        }

        private PlayerLootReceiver SpawnReceiver()
        {
            NetworkObject avatar = _runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity,
                inputAuthority: null, onBeforeSpawned: (_, instance) =>
                    SetField(instance.GetComponent<PlayerLootReceiver>(), "_lootCatalog", _catalog));
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            return avatar.GetComponent<PlayerLootReceiver>();
        }

        private NetworkLootPickup SpawnPickup(RaidLootOriginTransfer origins, int? eligibleAmount = null)
        {
            Assert.That(origins.TryGetTotal(out int amount), Is.True);
            bool accepted = false;
            NetworkObject instance = _runner.Spawn(_pickupPrefab, new Vector3(3f, 0f, 0f), Quaternion.identity,
                onBeforeSpawned: (runner, spawned) =>
                {
                    var pickup = spawned.GetComponent<NetworkLootPickup>();
                    SetField(pickup, "_lootCatalog", _catalog);
                    accepted = pickup.TrySetSpawnContentOverride(runner, spawned, new LootEntry(Bone, amount), true,
                        eligibleAmount ?? amount, origins);
                });
            Assert.That(accepted, Is.True);
            var result = instance.GetComponent<NetworkLootPickup>();
            Assert.That(result.IsAvailable, Is.True);
            return result;
        }

        private static void AssertOrigins(NetworkLootPickup pickup, params RaidLootOriginBucket[] expected)
        {
            Assert.That(pickup.TryResolveRaidOriginTransfer(out RaidLootOriginTransfer origins), Is.True);
            Assert.That(origins.Buckets, Is.EqualTo(expected));
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

        private NetworkObject Load(string guid)
        {
            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(id.IsValid, Is.True, guid);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(id, true);
            Assert.That(prefab, Is.Not.Null, guid);
            return prefab;
        }

        private static RaidLootOrigin PlayerOrigin(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId participant), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin origin), Is.True);
            return origin;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
#endif

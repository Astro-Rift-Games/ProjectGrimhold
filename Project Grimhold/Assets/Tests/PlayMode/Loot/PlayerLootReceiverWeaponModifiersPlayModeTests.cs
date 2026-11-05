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
    /// TASK-283 T7b: PlayerLootReceiver replicates one packed modifier word per physical slot,
    /// kept in lockstep with the slot, and carries it through RaidLootOriginTransfer.
    /// </summary>
    public sealed class PlayerLootReceiverWeaponModifiersPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private static readonly LootId Sword = new("arming_sword");
        private static readonly LootId Ruby = new("RES_RUBY_GEM");
        private static readonly LootId Bone = new("bone");
        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;
        private NetworkObject _prefab;
        private PlayerLootReceiver _receiver;
        private LootDefinitionCatalog _catalog;
        private WeaponInstanceModifiers _socketed;
        private WeaponInstanceModifiers _scaled;
        private WeaponInstanceModifiersNetworkData _socketedData;
        private WeaponInstanceModifiersNetworkData _scaledData;
        private readonly List<UnityEngine.Object> _ownedAssets = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var holder = new GameObject(nameof(PlayerLootReceiverWeaponModifiersPlayModeTests));
            _runner = holder.AddComponent<NetworkRunner>();
            holder.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
            _driver = holder.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-283-modifiers-{Guid.NewGuid():N}",
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
            LootDefinitionCatalog source = _prefab.GetComponent<PlayerLootReceiver>().LootCatalog;
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _ownedAssets.Add(_catalog);
            SetField(_catalog, "_definitions", new List<LootDefinition>
            {
                Clone(source, Sword), Clone(source, Ruby), Clone(source, Bone)
            });
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);

            Assert.That(WeaponGemSocket.TryCreateOccupied(Ruby, out WeaponGemSocket rubySocket, out error), Is.True, error);
            Assert.That(WeaponInstanceModifiers.TryCreate(default, default, CharacterAttribute.Strength, 2,
                rubySocket, default, out _socketed, out error), Is.True, error);
            Assert.That(WeaponScalingModifier.TryCreate(CharacterAttribute.Strength, WeaponScalingGrade.E,
                out WeaponScalingModifier primary, out error), Is.True, error);
            Assert.That(WeaponInstanceModifiers.TryCreate(primary, default, CharacterAttribute.Strength,
                out _scaled, out error), Is.True, error);
            Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(_socketed, CharacterAttribute.Strength, _catalog,
                out _socketedData, out error), Is.True, error);
            Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(_scaled, CharacterAttribute.Strength, _catalog,
                out _scaledData, out error), Is.True, error);
            Assert.That(_socketedData.IsEmpty, Is.False);
            Assert.That(_scaledData.IsEmpty, Is.False);

            _receiver = SpawnReceiver();
            yield return null;
            Assert.That(_receiver.HasStateAuthority, Is.True);
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
        public IEnumerator RaidLoadout_WritesEachWeaponUnitModifiersAndNoneForStacks()
        {
            yield return Execute(() => Assert.That(_receiver.TryInitializeRaidLoadout(LoadoutOf(_socketed, _scaled, 3),
                Participant(1), out string error), Is.True, error));

            AssertModifiers(_receiver, 0, _socketedData);
            AssertModifiers(_receiver, 1, _scaledData);
            Assert.That(_receiver.TryGetInventorySlot(2, out LootInventorySlot bone), Is.True);
            Assert.That(bone.Quantity, Is.EqualTo(3));
            AssertModifiers(_receiver, 2, default);
            for (int index = 3; index < 30; index++)
                AssertModifiers(_receiver, index, default);
        }

        [UnityTest]
        public IEnumerator AddressedWeaponResolution_CarriesTheSelectedSlotModifiers()
        {
            yield return Execute(() =>
            {
                Assert.That(_receiver.TryInitializeRaidLoadout(LoadoutOf(_socketed, _scaled, 0),
                    Participant(1), out string error), Is.True, error);
                Assert.That(_receiver.ResolveInventorySlotExtraction(new EntityId(int.MaxValue), Sword,
                    LootTransferQuantityMode.FullStack, 1, _receiver.Revision, _runner.Tick,
                    out LootTransferRequest request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Modifiers, Is.EqualTo(_scaledData));
            });
        }

        [UnityTest]
        public IEnumerator AddressedWeaponTransfer_MovesModifiersToTheDestinationAndClearsTheSource()
        {
            PlayerLootReceiver destination = SpawnReceiver();
            yield return Execute(() =>
            {
                Assert.That(_receiver.TryInitializeRaidLoadout(LoadoutOf(_socketed, _scaled, 0),
                    Participant(1), out string error), Is.True, error);
                Assert.That(_receiver.ResolveInventorySlotExtraction(destination.Id, Sword,
                    LootTransferQuantityMode.FullStack, 0, _receiver.Revision, _runner.Tick,
                    out LootTransferRequest request), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(LootTransferTransaction.Execute(_receiver, destination, request).Success, Is.True);
            });

            Assert.That(_receiver.TryGetInventorySlot(0, out LootInventorySlot emptied), Is.True);
            Assert.That(emptied.IsEmpty, Is.True);
            AssertModifiers(_receiver, 0, default);
            AssertModifiers(_receiver, 1, _scaledData);
            AssertModifiers(destination, 0, _socketedData);
        }

        [UnityTest]
        public IEnumerator UnaddressedWeaponExtraction_RemovesTheResolvedUnitOnly()
        {
            yield return Execute(() =>
            {
                Assert.That(_receiver.TryInitializeRaidLoadout(LoadoutOf(_socketed, _scaled, 0),
                    Participant(1), out string error), Is.True, error);
                var request = new LootTransferRequest(_receiver.Id, new EntityId(int.MaxValue), Sword, 1, _runner.Tick);
                Assert.That(_receiver.TryResolveRaidLootOriginTransfer(request, out RaidLootOriginTransfer transfer), Is.True);
                Assert.That(transfer.Modifiers, Is.EqualTo(_socketedData));
                _receiver.CommitRaidLootExtraction(request, transfer);
            });

            Assert.That(_receiver.TryGetInventorySlot(0, out LootInventorySlot first), Is.True);
            Assert.That(first.IsEmpty, Is.True);
            AssertModifiers(_receiver, 0, default);
            AssertModifiers(_receiver, 1, _scaledData);
        }

        [UnityTest]
        public IEnumerator Reception_RejectsModifiersOnStackableLootWithoutMutation()
        {
            yield return Execute(() =>
            {
                Assert.That(RaidLootOriginTransfer.TryCreate(RaidLootOrigin.Dungeon, 2, out RaidLootOriginTransfer plain), Is.True);
                RaidLootOriginTransfer tagged = plain.WithModifiers(_socketedData);
                var request = new LootTransferRequest(new EntityId(int.MaxValue), _receiver.Id, Bone, 2, _runner.Tick);
                int revision = _receiver.Revision;
                Assert.That(_receiver.ValidateRaidLootOriginReceive(request, tagged),
                    Is.EqualTo(LootTransferFailureReason.InvalidLoot));
                Assert.That(_receiver.Revision, Is.EqualTo(revision));
                Assert.That(_receiver.OccupiedSlotCount, Is.Zero);
            });
        }

        [UnityTest]
        public IEnumerator BehaviourCopyStateFrom_PreservesModifiersAcrossSpawnedReplay()
        {
            PlayerLootReceiver restored = SpawnReceiver();
            yield return Execute(() =>
            {
                Assert.That(_receiver.TryInitializeRaidLoadout(LoadoutOf(_socketed, _scaled, 2),
                    Participant(1), out string error), Is.True, error);
                restored.CopyStateFrom(_receiver);
                restored.GetComponent<PlayerRaidLootOriginState>().CopyStateFrom(
                    _receiver.GetComponent<PlayerRaidLootOriginState>());
                Assert.That(_runner.GetComponent<EntityRegistry>().TryUnregisterLootReceiver(restored.Id, restored), Is.True);
                restored.Spawned();
            });

            AssertModifiers(restored, 0, _socketedData);
            AssertModifiers(restored, 1, _scaledData);
            AssertModifiers(restored, 2, default);
        }

        private IReadOnlyList<LootEntry> LoadoutOf(WeaponInstanceModifiers first, WeaponInstanceModifiers second, int bones)
        {
            var entries = new List<LootEntry>
            {
                new(Sword, 1, first, isEquipmentUnit: true),
                new(Sword, 1, second, isEquipmentUnit: true)
            };
            if (bones > 0)
                entries.Add(new LootEntry(Bone, bones));
            return entries;
        }

        private static void AssertModifiers(PlayerLootReceiver receiver, int slot, WeaponInstanceModifiersNetworkData expected)
        {
            Assert.That(receiver.TryGetInventorySlotModifiers(slot, out WeaponInstanceModifiersNetworkData actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected), $"Modifiers of slot {slot}.");
        }

        private PlayerLootReceiver SpawnReceiver()
        {
            NetworkObject avatar = _runner.Spawn(_prefab, Vector3.zero, Quaternion.identity,
                inputAuthority: null, onBeforeSpawned: (_, instance) =>
                    SetField(instance.GetComponent<PlayerLootReceiver>(), "_lootCatalog", _catalog));
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

        private LootDefinition Clone(LootDefinitionCatalog source, LootId id)
        {
            Assert.That(source.TryGet(id.Value, out LootDefinition original), Is.True, id.Value);
            LootDefinition clone = UnityEngine.Object.Instantiate(original);
            _ownedAssets.Add(clone);
            SetField(clone, "_maxTransportable", 0);
            return clone;
        }

        private static RaidParticipantId Participant(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId participant), Is.True);
            return participant;
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

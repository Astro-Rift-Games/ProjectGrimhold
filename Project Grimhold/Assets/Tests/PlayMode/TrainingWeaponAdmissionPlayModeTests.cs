#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Equipment
{
    public sealed class TrainingWeaponAdmissionPlayModeTests
    {
        private NetworkRunner _runner;
        private bool _previousIgnoreLogs;

        [UnityTest] public IEnumerator Admission_EquipsTrainingSword() => Admit("training_short_sword");
        [UnityTest] public IEnumerator Admission_EquipsTrainingDagger() => Admit("training_dagger");
        [UnityTest] public IEnumerator Admission_EquipsTrainingWand() => Admit("training_wand");

        private IEnumerator Admit(string id)
        {
            // Same isolated Single-runner composition as PlayerEquipmentPlayModeTests.
            // The productive avatar has unrelated scene-service dependencies absent in this fixture.
            _previousIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            var owner = new GameObject("Training admission runner");
            _runner = owner.AddComponent<NetworkRunner>();
            owner.AddComponent<EntityRegistry>();
            var driver = owner.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single, SessionName = "training-" + Guid.NewGuid().ToString("N"),
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(RaidParticipantId.TryCreate(1, out var participantId), Is.True);
            var participant = _runner.Spawn(Load("c39d451563bae6e43934008a0dadc6d6"), Vector3.zero, Quaternion.identity,
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "training-admission", participantId, ProgressionBalanceDefaults.InitialCharacterAttributeState,
                    ExperienceCurve.InitialLevel, 0, "training-generation"));
            var avatar = _runner.Spawn(Load("fea3a7b256f965a4eb9b965832939741"), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) => instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant));
            var equipment = avatar.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var receiver = avatar.GetComponent<PlayerLootReceiver>();
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            EquipmentTestContent.SetField(equipment, "_lootCatalog", catalog);
            EquipmentTestContent.SetField(receiver, "_lootCatalog", catalog);
            Assert.That(catalog.TryGet(id, out var definition), Is.True);
            var plain = EquipmentInstance.PlainCache(definition);
            var reserved = new[] { new LootEntry(new LootId(id), 1, plain.Modifiers) };
            var indices = new int[EquipmentSlotRules.AllSlots.Length];
            indices[Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand)] = 1;
            bool initialized = false;
            string error = null;
            driver.PendingAction = _ =>
            {
                Assert.That(receiver.TryInitializeRaidLoadout(reserved, participantId, out var inventoryError), Is.True, inventoryError);
                initialized = equipment.TryInitializePreparedEquipment(reserved, indices, WeaponSetSlot.SetA, out error);
            };
            float deadline = Time.realtimeSinceStartup + 20f;
            while (driver.PendingAction != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(driver.PendingAction, Is.Null, "Admission must execute on a Fusion tick.");
            Assert.That(driver.LastException, Is.Null, driver.LastException?.ToString());
            Assert.That(initialized, Is.True, error);
            Assert.That(equipment.TryGetEquippedDefinition(out var equipped), Is.True);
            Assert.That(equipped.Id, Is.EqualTo(id));
            Assert.That(equipment.TryPrepareAttack(EquipmentSlot.WeaponSetAMainHand, out var attack, out var weapon), Is.True);
            Assert.That(weapon, Is.SameAs(definition.WeaponDefinition));
            Assert.That(attack, id == "training_wand" ? Is.TypeOf<RangedAttack>() : Is.TypeOf<MeleeAttack>());
            Assert.That(equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
            Assert.That(receiver.GetLootAmount(new LootId("arming_sword")), Is.Zero);
            Assert.That(receiver.GetLootContent(), Is.Empty, "The only reserved unit moves into Equipment.");
            int occupied = 0;
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                if (!equipment.TryGetSlotLoot(slot, out var entry)) continue;
                occupied++;
                Assert.That(entry.LootId.Value, Is.EqualTo(id));
            }
            Assert.That(occupied, Is.EqualTo(1), "No extra recovery weapon may be equipped.");
        }

        private NetworkObject Load(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            var prefab = _runner.Config.PrefabTable.Load(id, true);
            Assert.That(prefab, Is.Not.Null, guid);
            return prefab;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
            }
            if (_runner != null) Object.DestroyImmediate(_runner.gameObject);
            LogAssert.ignoreFailingMessages = _previousIgnoreLogs;
        }
    }
}
#endif

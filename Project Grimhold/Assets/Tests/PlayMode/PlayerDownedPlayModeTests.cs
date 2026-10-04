#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Loot
{
    public sealed class PlayerDownedPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return ShutdownRunner(_runner);
        }

        [UnityTest]
        public IEnumerator FatalHit_EntersDownedWithFullReserveAndNoCorpse()
        {
            yield return StartRunnerAndLoadPlayer();
            Spawn(out PlayerCharacter player, out PlayerDownedStateNetworkController downed,
                out NetworkLootContainer container);
            SetField(downed, "_downedDrainPerSecond", 0f);

            yield return Hit(100000f);

            Assert.That(_driver.FirstResult.IsApplied, Is.True);
            Assert.That(_driver.FirstResult.IsFatal, Is.False);
            Assert.That(player.Health, Is.EqualTo(0f));
            Assert.That((bool)downed.IsDowned, Is.True);
            Assert.That(player.IsDowned, Is.True);
            Assert.That(player.IsAlive, Is.True);
            Assert.That(downed.DownedHealth, Is.EqualTo(75f).Within(0.001f));
            Assert.That(downed.DownedCycle, Is.EqualTo(1));
            Assert.That((bool)container.IsAvailable, Is.False);
        }

        [UnityTest]
        public IEnumerator HitWhileDowned_ReducesReserveByMitigatedTimesMultiplier()
        {
            yield return StartRunnerAndLoadPlayer();
            Spawn(out PlayerCharacter player, out PlayerDownedStateNetworkController downed,
                out NetworkLootContainer container);
            SetField(downed, "_downedDrainPerSecond", 0f);
            SetField(downed, "_downedDamageMultiplier", 2f);
            yield return Hit(100000f);

            yield return Hit(10f);

            Assert.That(_driver.FirstResult.IsApplied, Is.True);
            Assert.That(_driver.FirstResult.AppliedDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(downed.DownedHealth, Is.EqualTo(55f).Within(0.001f));
            Assert.That((bool)downed.IsDowned, Is.True);
        }

        [UnityTest]
        public IEnumerator Depletion_ResolvesDefeatExactlyOnceAndSecondCycleStartsFull()
        {
            yield return StartRunnerAndLoadPlayer();
            Spawn(out PlayerCharacter player, out PlayerDownedStateNetworkController downed,
                out NetworkLootContainer container);
            SetField(downed, "_downedDrainPerSecond", 0f);
            yield return Hit(100000f);

            yield return Hit(100000f);

            Assert.That(_driver.FirstResult.IsFatal, Is.True);
            Assert.That((bool)downed.IsDowned, Is.False);
            Assert.That(downed.DownedHealth, Is.EqualTo(0f));
            Assert.That(player.IsAlive, Is.False);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude),
                Has.Length.EqualTo(1));

            Assert.That(downed.TryEnterDowned(), Is.True);
            Assert.That(downed.DownedCycle, Is.EqualTo(2));
            Assert.That(downed.DownedHealth, Is.EqualTo(75f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Drain_DepletesReserveAndResolvesDefeat()
        {
            yield return StartRunnerAndLoadPlayer();
            Spawn(out PlayerCharacter player, out PlayerDownedStateNetworkController downed,
                out NetworkLootContainer container);
            SetField(downed, "_downedDrainPerSecond", 100000f);

            yield return Hit(100000f);
            int frames = 120;
            while (player.IsAlive && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(player.IsAlive, Is.False);
            Assert.That((bool)downed.IsDowned, Is.False);
            Assert.That((bool)container.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator HealingWhileDowned_IsRejected()
        {
            yield return StartRunnerAndLoadPlayer();
            Spawn(out PlayerCharacter player, out PlayerDownedStateNetworkController downed,
                out NetworkLootContainer container);
            SetField(downed, "_downedDrainPerSecond", 0f);
            yield return Hit(100000f);

            _driver.RequestedHealingAmount = 50f;
            _driver.RequestedDamageAmount = 0.0001f;
            _driver.IsRequested = true;
            yield return WaitForRequestConsumed();

            Assert.That(_driver.HealingResult.Success, Is.False);
            Assert.That(player.Health, Is.EqualTo(0f));
            Assert.That(player.CanReceiveStatusEffects, Is.False);
        }

        private void Spawn(
            out PlayerCharacter player,
            out PlayerDownedStateNetworkController downed,
            out NetworkLootContainer container)
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.");
            NetworkObject playerObject = _runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity);
            player = playerObject.GetComponent<PlayerCharacter>();
            downed = playerObject.GetComponent<PlayerDownedStateNetworkController>();
            container = playerObject.GetComponent<NetworkLootContainer>();
            Assert.That(downed, Is.Not.Null, "Player prefab must contain PlayerDownedStateNetworkController.");
            _driver.AllowDowned = true;
            _driver.Target = player;
            _driver.Receiver = playerObject.GetComponent<PlayerLootReceiver>();
            _driver.SetEntries(Array.Empty<LootEntry>());
        }

        private IEnumerator Hit(float amount)
        {
            _driver.RequestedDamageAmount = amount;
            _driver.IsRequested = true;
            yield return WaitForRequestConsumed();
        }

        private IEnumerator WaitForRequestConsumed()
        {
            int frames = 300;
            while (_driver.IsRequested && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(_driver.IsRequested, Is.False, "Driver request was not consumed by a simulation tick.");
            yield return WaitFrames(2);
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private IEnumerator StartRunnerAndLoadPlayer()
        {
            var runnerObject = new GameObject("PlayerDownedTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"downed-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());
            NetworkPrefabId playerId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PlayerPrefabGuid));
            _playerPrefab = _runner.Config.PrefabTable.Load(playerId, true);
        }

        private static IEnumerator ShutdownRunner(NetworkRunner runner)
        {
            if (runner != null && runner.IsRunning)
            {
                runner.Shutdown();
                while (runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (runner != null)
            {
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }
        }
    }
}
#endif

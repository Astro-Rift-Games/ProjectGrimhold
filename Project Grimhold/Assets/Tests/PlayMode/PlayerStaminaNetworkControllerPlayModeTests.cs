#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Player
{
    public sealed class PlayerStaminaNetworkControllerPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";

        private NetworkRunner _runner;
        private PlayerStaminaSimulationDriver _driver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner != null && _runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator AdmittedAvatar_InitializesOnceFromResistanceAndRegeneratesAfterDelay()
        {
            yield return StartRunner();
            PlayerStaminaNetworkController stamina = SpawnAdmittedAvatar(resistance: 5);
            yield return WaitUntil(() => stamina.CurrentStamina > 0f, "Stamina did not initialize.");

            Assert.That(stamina.TryGetMaximumStamina(out float maximum), Is.True);
            Assert.That(maximum, Is.EqualTo(100f));
            Assert.That(stamina.CurrentStamina, Is.EqualTo(maximum));

            yield return Spend(stamina, 10f, expected: true);
            float afterSpend = stamina.CurrentStamina;
            yield return WaitSeconds(0.5f);
            Assert.That(stamina.CurrentStamina, Is.EqualTo(afterSpend).Within(0.001f));

            yield return WaitUntil(
                () => stamina.CurrentStamina > afterSpend,
                "Stamina did not regenerate after its configured delay.");
            float expectedPerTick = 15f * _runner.DeltaTime;
            Assert.That(stamina.CurrentStamina, Is.LessThanOrEqualTo(afterSpend + expectedPerTick * 2f));
        }

        [UnityTest]
        public IEnumerator ContinuousSpend_UsesRunnerDeltaTimeAndPreservesUnaffordableRemainder()
        {
            yield return StartRunner();
            PlayerStaminaNetworkController stamina = SpawnAdmittedAvatar(resistance: 5);
            yield return WaitUntil(() => stamina.CurrentStamina > 0f, "Stamina did not initialize.");

            float tickCost = 10f * _runner.DeltaTime;
            float initial = stamina.CurrentStamina;
            yield return SpendContinuous(stamina, tickCost, expected: true);
            Assert.That(stamina.CurrentStamina, Is.EqualTo(initial - tickCost).Within(0.0001f));

            float remainder = tickCost * 0.5f;
            yield return Spend(stamina, stamina.CurrentStamina - remainder, expected: true);
            yield return SpendContinuous(stamina, tickCost, expected: false);

            Assert.That(stamina.CurrentStamina, Is.EqualTo(remainder).Within(0.0001f));
            Assert.That((bool)stamina.IsExhausted, Is.True);
        }

        [UnityTest]
        public IEnumerator ContinuousSpend_CompletePaymentAllowsFinalTickAndThenBlocks()
        {
            yield return StartRunner();
            PlayerStaminaNetworkController stamina = SpawnAdmittedAvatar(resistance: 5);
            yield return WaitUntil(() => stamina.CurrentStamina > 0f, "Stamina did not initialize.");

            float tickCost = 10f * _runner.DeltaTime;
            yield return Spend(stamina, stamina.CurrentStamina - tickCost, expected: true);
            yield return SpendContinuous(stamina, tickCost, expected: true);

            Assert.That(stamina.CurrentStamina, Is.Zero.Within(0.0001f));
            Assert.That((bool)stamina.IsExhausted, Is.True);
            yield return SpendContinuous(stamina, tickCost, expected: false);
            Assert.That(stamina.CurrentStamina, Is.Zero.Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator CopyStateFrom_PreservesAllNetworkedStateAndFreezesWithoutParticipantFixup()
        {
            yield return StartRunner();
            PlayerStaminaNetworkController source = SpawnAdmittedAvatar(resistance: 30);
            yield return WaitUntil(() => source.CurrentStamina > 0f, "Source Stamina did not initialize.");
            SetField(source, "_exhaustionRecoveryThreshold", 0.75f);
            yield return Spend(source, 100f, expected: true);
            yield return SpendContinuous(source, 150f, expected: false);

            LogAssert.ignoreFailingMessages = true;
            PlayerStaminaNetworkController restored = SpawnAvatarWithoutParticipant();
            yield return CopyState(restored, source);

            float copiedCurrent = restored.CurrentStamina;
            bool copiedExhaustion = restored.IsExhausted;
            bool copiedInitialization = ReadNetworkedProperty<NetworkBool>(restored, "IsInitialized");
            TickTimer copiedDelay = ReadNetworkedProperty<TickTimer>(restored, "RegenerationDelay");
            float? sourceRemaining = ReadNetworkedProperty<TickTimer>(source, "RegenerationDelay")
                .RemainingTime(_runner);
            float? restoredRemaining = copiedDelay.RemainingTime(_runner);

            Assert.That(copiedCurrent, Is.EqualTo(125f).Within(0.0001f));
            Assert.That(copiedExhaustion, Is.True);
            Assert.That(copiedInitialization, Is.True);
            Assert.That(restoredRemaining.HasValue, Is.True);
            Assert.That(sourceRemaining.HasValue, Is.True);
            Assert.That(restoredRemaining.Value, Is.EqualTo(sourceRemaining.Value).Within(_runner.DeltaTime));

            yield return WaitSeconds(_runner.DeltaTime * 3f);
            Assert.That(restored.TryGetMaximumStamina(out _), Is.False);
            Assert.That(restored.CurrentStamina, Is.EqualTo(copiedCurrent));
            Assert.That((bool)restored.IsExhausted, Is.EqualTo(copiedExhaustion));
            Assert.That(
                (bool)ReadNetworkedProperty<NetworkBool>(restored, "IsInitialized"),
                Is.True);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("PlayerStaminaTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerStaminaSimulationDriver>();

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"stamina-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });

            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private PlayerStaminaNetworkController SpawnAdmittedAvatar(int resistance)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    5, resistance, 5, 5, 5, 5, 0, out CharacterAttributeState attributes),
                Is.True);
            RaidParticipantId.TryCreate(1, out RaidParticipantId participantId);
            NetworkObject participant = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        $"stamina-{resistance}",
                        participantId,
                        attributes,
                        ExperienceCurve.InitialLevel,
                        0,
                        "stamina-test"));

            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject avatar = _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                Vector3.zero,
                Quaternion.identity,
                _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant));
            return avatar.GetComponent<PlayerStaminaNetworkController>();
        }

        private PlayerStaminaNetworkController SpawnAvatarWithoutParticipant()
        {
            NetworkObject avatar = _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                Vector3.zero,
                Quaternion.identity,
                _runner.LocalPlayer);
            return avatar.GetComponent<PlayerStaminaNetworkController>();
        }

        private static void ExpectBasePrefabExtractionProgressValidationError()
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
        }

        private NetworkObject LoadPrefab(string prefabGuid)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(prefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, prefabGuid);
            return prefab;
        }

        private IEnumerator Spend(
            PlayerStaminaNetworkController stamina,
            float amount,
            bool expected)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestSpend(stamina, amount);
            yield return WaitUntil(() => _driver.CompletionSequence != previous, "Spend did not execute.");
            Assert.That(_driver.LastResult, Is.EqualTo(expected));
        }

        private IEnumerator SpendContinuous(
            PlayerStaminaNetworkController stamina,
            float amount,
            bool expected)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestSpendContinuous(stamina, amount);
            yield return WaitUntil(
                () => _driver.CompletionSequence != previous,
                "Continuous spend did not execute.");
            Assert.That(_driver.LastResult, Is.EqualTo(expected));
        }

        private IEnumerator CopyState(
            PlayerStaminaNetworkController target,
            PlayerStaminaNetworkController source)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestCopyState(target, source);
            yield return WaitUntil(() => _driver.CompletionSequence != previous, "Copy did not execute.");
            Assert.That(_driver.LastResult, Is.True);
        }

        private static T ReadNetworkedProperty<T>(
            PlayerStaminaNetworkController stamina,
            string propertyName)
        {
            PropertyInfo property = typeof(PlayerStaminaNetworkController).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(stamina);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string message)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, message);
        }
    }
}
#endif

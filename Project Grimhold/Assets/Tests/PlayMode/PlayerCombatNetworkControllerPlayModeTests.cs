#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Combat
{
    public sealed class PlayerCombatNetworkControllerPlayModeTests
    {
        private const string BasePrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MeleePrefabGuid = "982f360e5acbdd344a8a75bbc0af94ec";
        private const string RangedPrefabGuid = "5ac01b0bb782cc04c8ccfc9c41612d57";
        private const string MissingExtractionProgressDependenciesMessage =
            "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.";

        private static readonly PropertyInfo PreviousButtonsProperty = GetProperty("PreviousButtons");
        private static readonly PropertyInfo AttackCooldownProperty = GetProperty("AttackCooldown");
        private static readonly PropertyInfo HasActiveAttackProperty = GetProperty("HasActiveAttack");
        private static readonly PropertyInfo CooldownDurationProperty =
            GetProperty("AttackCooldownDurationSeconds");
        private static readonly PropertyInfo AttackSequenceProperty = GetProperty("AttackSequence");
        private static readonly FieldInfo ActiveAttackField = GetField("_activeAttack");
        private static readonly FieldInfo ActiveAttackSourceField = GetField("_activeAttackSource");
        private static readonly MethodInfo CacheDependenciesMethod =
            GetMethod("CacheDependencies");

        private NetworkRunner _runner;
        private PlayerCombatInputDriver _inputDriver;
        private PlayerCombatStrategySimulationDriver _strategyDriver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted)
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
        public IEnumerator NeutralAssignmentAndCooldownFlow_UsesAuthoritativeStrategyPresence()
        {
            yield return StartRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject playerObject = Spawn(BasePrefabGuid, _runner.LocalPlayer, Vector3.zero);
            PlayerCombatNetworkController controller =
                playerObject.GetComponent<PlayerCombatNetworkController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.HasStateAuthority, Is.True);
            Assert.That(controller.HasInputAuthority, Is.True);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(controller.TryGetPrimaryAttackStatus(out _), Is.False);

            int feedbackCount = 0;
            int performedCount = 0;
            controller.CombatFeedbackResolved += _ => feedbackCount++;
            controller.AttackPerformed += _ => performedCount++;
            int neutralSequence = ReadAttackSequence(controller);

            _inputDriver.AttackHeld = true;
            yield return WaitUntil(
                () => ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Neutral combat did not retain the current button history.");
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(neutralSequence));
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(feedbackCount, Is.Zero);
            Assert.That(performedCount, Is.Zero);
            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Neutral combat did not consume the button release.");

            PlayerCombatTestAttack firstAttack =
                playerObject.gameObject.AddComponent<PlayerCombatTestAttack>();
            firstAttack.Initialize(AttackType.Melee, 0.4f);
            PlayerCombatTestAttack zeroCooldownAttack =
                playerObject.gameObject.AddComponent<PlayerCombatTestAttack>();
            zeroCooldownAttack.Initialize(AttackType.Ranged, 0f);

            yield return SetStrategy(controller, firstAttack, true);
            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus ready), Is.True);
            Assert.That(ready.IsAvailable, Is.True);

            yield return SetAttackEnabled(controller, false, true);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus disabled), Is.True);
            Assert.That(disabled.IsAvailable, Is.False);
            yield return SetAttackEnabled(controller, true, true);

            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerCombatNetworkController: Cannot set active attack to null.");
            yield return SetStrategy(controller, null, false);
            Assert.That(ReadActiveAttack(controller), Is.SameAs(firstAttack));
            Assert.That(ReadActiveAttackSource(controller), Is.SameAs(firstAttack));
            Assert.That(ReadHasActiveAttack(controller), Is.True);

            TickTimer timerBeforeInvalidAssignment = ReadCooldown(controller);
            float durationBeforeInvalidAssignment = ReadCooldownDuration(controller);
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                new Regex("PlayerCombatNetworkController: The component .* does not implement IAttack\\."));
            yield return SetStrategy(controller, controller, false);
            Assert.That(ReadActiveAttack(controller), Is.SameAs(firstAttack));
            Assert.That(ReadActiveAttackSource(controller), Is.SameAs(firstAttack));
            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(ReadCooldown(controller), Is.EqualTo(timerBeforeInvalidAssignment));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(durationBeforeInvalidAssignment));

            yield return PressAttackUntil(
                controller,
                () => firstAttack.ExecutionCount == 1,
                "The assigned attack did not execute from Fusion input.");
            int firstSequence = ReadAttackSequence(controller);
            TickTimer firstTimer = ReadCooldown(controller);
            Assert.That(firstTimer, Is.Not.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));

            yield return ClearStrategy(controller, true);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadActiveAttack(controller), Is.Null);
            Assert.That(ReadActiveAttackSource(controller), Is.Null);
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(controller.TryGetPrimaryAttackStatus(out _), Is.False);

            CacheDependenciesMethod.Invoke(controller, null);
            Assert.That(ReadHasActiveAttack(controller), Is.False);
            Assert.That(ReadActiveAttack(controller), Is.Null);
            Assert.That(ReadActiveAttackSource(controller), Is.Null);

            yield return SetStrategy(controller, zeroCooldownAttack, true);
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));
            Assert.That(ReadCooldownDuration(controller), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus blocked), Is.True);
            Assert.That(blocked.IsAvailable, Is.False);
            Assert.That(blocked.CooldownDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(blocked.CooldownRemainingSeconds, Is.GreaterThan(0f));

            yield return PressAttackForFrames(controller, 5);
            Assert.That(zeroCooldownAttack.ExecutionCount, Is.Zero);
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(firstSequence));
            Assert.That(ReadCooldown(controller), Is.EqualTo(firstTimer));

            yield return WaitUntil(
                () => controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    status.IsAvailable,
                "The preserved cooldown did not expire.");
            yield return PressAttackUntil(
                controller,
                () => zeroCooldownAttack.ExecutionCount == 1,
                "The replacement attack did not execute after the prior cooldown expired.");
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(firstSequence + 1));
            Assert.That(ReadCooldown(controller), Is.EqualTo(TickTimer.None));
            Assert.That(ReadCooldownDuration(controller), Is.Zero);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus zeroStatus), Is.True);
            Assert.That(zeroStatus.IsAvailable, Is.True);
            Assert.That(zeroStatus.CooldownDurationSeconds, Is.Zero);
            Assert.That(zeroStatus.CooldownRemainingSeconds, Is.Zero);

            yield return SetStrategy(controller, firstAttack, true);
            yield return PressAttackUntil(
                controller,
                () => firstAttack.ExecutionCount == 2,
                "The first strategy did not execute a second time.");
            int sequenceBeforeMissingImplementation = ReadAttackSequence(controller);
            ActiveAttackField.SetValue(controller, null);

            Assert.That(ReadHasActiveAttack(controller), Is.True);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus proxyStatus), Is.True);
            Assert.That(proxyStatus.IsAvailable, Is.False);
            Assert.That(proxyStatus.CooldownDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
            yield return SetAttackEnabled(controller, false, true);
            yield return WaitUntil(
                () => controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    status.CooldownRemainingSeconds <= 0f,
                "The replicated cooldown did not expire while combat was disabled.");
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus disabledProxyStatus), Is.True);
            Assert.That(disabledProxyStatus.IsAvailable, Is.False);

            yield return SetAttackEnabled(controller, true, true);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus enabledProxyStatus), Is.True);
            Assert.That(enabledProxyStatus.IsAvailable, Is.True);

            PlayerCharacter character = playerObject.GetComponent<PlayerCharacter>();
            yield return DefeatCharacter(character, true);
            Assert.That(character.IsAlive, Is.False);
            Assert.That(controller.TryGetPrimaryAttackStatus(out PrimaryAttackStatus defeatedProxyStatus), Is.True);
            Assert.That(defeatedProxyStatus.IsAvailable, Is.False);

            yield return PressAttackForFrames(controller, 5);
            Assert.That(firstAttack.ExecutionCount, Is.EqualTo(2));
            Assert.That(ReadAttackSequence(controller), Is.EqualTo(sequenceBeforeMissingImplementation));
        }

        [UnityTest]
        public IEnumerator FreshVariants_InitializeAuthoritativeAttackPresence()
        {
            yield return StartRunner();
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject melee = Spawn(MeleePrefabGuid, null, Vector3.zero);
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject ranged = Spawn(RangedPrefabGuid, null, Vector3.right * 3f);
            yield return null;

            PlayerCombatNetworkController meleeController =
                melee.GetComponent<PlayerCombatNetworkController>();
            PlayerCombatNetworkController rangedController =
                ranged.GetComponent<PlayerCombatNetworkController>();

            Assert.That(ReadHasActiveAttack(meleeController), Is.True);
            Assert.That(ReadActiveAttack(meleeController), Is.TypeOf<MeleeAttack>());
            Assert.That(meleeController.TryGetPrimaryAttackStatus(out PrimaryAttackStatus meleeStatus), Is.True);
            Assert.That(meleeStatus.IsAvailable, Is.True);

            Assert.That(ReadHasActiveAttack(rangedController), Is.True);
            Assert.That(ReadActiveAttack(rangedController), Is.TypeOf<RangedAttack>());
            Assert.That(rangedController.TryGetPrimaryAttackStatus(out PrimaryAttackStatus rangedStatus), Is.True);
            Assert.That(rangedStatus.IsAvailable, Is.True);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("PlayerCombatNetworkControllerTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _inputDriver = runnerObject.AddComponent<PlayerCombatInputDriver>();
            _strategyDriver = runnerObject.AddComponent<PlayerCombatStrategySimulationDriver>();
            _runner.AddCallbacks(_inputDriver);
            _runner.ProvideInput = true;

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"cb-01-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkObject Spawn(string prefabGuid, PlayerRef? inputAuthority, Vector3 position)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(prefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, prefabGuid);
            return _runner.Spawn(prefab, position, Quaternion.identity, inputAuthority);
        }

        private IEnumerator PressAttackUntil(
            PlayerCombatNetworkController controller,
            Func<bool> predicate,
            string failureMessage)
        {
            _inputDriver.AttackHeld = true;
            yield return WaitUntil(predicate, failureMessage);
            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Combat did not consume the button release.");
        }

        private IEnumerator SetStrategy(
            PlayerCombatNetworkController controller,
            MonoBehaviour attackSource,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetStrategy(controller, attackSource);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The strategy assignment was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator ClearStrategy(
            PlayerCombatNetworkController controller,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestClearStrategy(controller);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The strategy removal was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator SetAttackEnabled(
            PlayerCombatNetworkController controller,
            bool enabled,
            bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetEnabled(controller, enabled);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The combat-enabled change was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator DefeatCharacter(PlayerCharacter character, bool expectedResult)
        {
            int previousSequence = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestDefeatCharacter(character);
            yield return WaitUntil(
                () => _strategyDriver.CompletionSequence != previousSequence,
                "The character defeat was not processed during Fusion simulation.");
            Assert.That(_strategyDriver.LastResult, Is.EqualTo(expectedResult));
        }

        private IEnumerator PressAttackForFrames(
            PlayerCombatNetworkController controller,
            int frameCount)
        {
            _inputDriver.AttackHeld = true;
            for (int index = 0; index < frameCount; index++)
            {
                yield return null;
            }

            _inputDriver.AttackHeld = false;
            yield return WaitUntil(
                () => !ReadPreviousButtons(controller).IsSet(PlayerInputButton.PrimaryAttack),
                "Combat did not consume the button release.");
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private static NetworkButtons ReadPreviousButtons(PlayerCombatNetworkController controller) =>
            (NetworkButtons)PreviousButtonsProperty.GetValue(controller);

        private static TickTimer ReadCooldown(PlayerCombatNetworkController controller) =>
            (TickTimer)AttackCooldownProperty.GetValue(controller);

        private static bool ReadHasActiveAttack(PlayerCombatNetworkController controller) =>
            (bool)(NetworkBool)HasActiveAttackProperty.GetValue(controller);

        private static float ReadCooldownDuration(PlayerCombatNetworkController controller) =>
            (float)CooldownDurationProperty.GetValue(controller);

        private static int ReadAttackSequence(PlayerCombatNetworkController controller) =>
            (int)AttackSequenceProperty.GetValue(controller);

        private static IAttack ReadActiveAttack(PlayerCombatNetworkController controller) =>
            ActiveAttackField.GetValue(controller) as IAttack;

        private static MonoBehaviour ReadActiveAttackSource(PlayerCombatNetworkController controller) =>
            ActiveAttackSourceField.GetValue(controller) as MonoBehaviour;

        private static PropertyInfo GetProperty(string propertyName)
        {
            PropertyInfo property = typeof(PlayerCombatNetworkController).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return property;
        }

        private static FieldInfo GetField(string fieldName)
        {
            FieldInfo field = typeof(PlayerCombatNetworkController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }

        private static MethodInfo GetMethod(string methodName)
        {
            MethodInfo method = typeof(PlayerCombatNetworkController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }

        private sealed class PlayerCombatInputDriver : NetworkRunnerCallbacksAdapter
        {
            public bool AttackHeld { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                PlayerNetworkInput playerInput = default;
                playerInput.Buttons.Set(PlayerInputButton.PrimaryAttack, AttackHeld);
                input.Set(playerInput);
            }
        }
    }
}
#endif

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Downed
{
    /// <summary>
    /// End-to-end assisted recovery driven by real Fusion input: held Interact to completion, the
    /// voluntary-movement interruptions, retry from zero, a Downed disconnect that must not interrupt
    /// and the guarantee that an input held while Downed is not replayed after the recovery.
    /// Several rows characterize behavior the session owner already provides.
    /// </summary>
    public sealed class PlayerDownedReviveFlowPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MissingExtractionProgressDependenciesMessage =
            "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.";

        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;
        private PlayerCombatStrategySimulationDriver _strategyDriver;
        private FlowInputDriver _input;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                UnityEngine.Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator HeldInteract_ToCompletion_ReturnsActiveWithOneHealthAndZeroStamina()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            SetField(target.Recovery, "_reviveDurationSeconds", 0.5f);
            SetNetworkedProperty(target.Stamina, nameof(PlayerStaminaNetworkController.CurrentStamina), 50f);
            yield return DownTarget(target);
            Assert.That(target.Stamina.CurrentStamina, Is.GreaterThan(0f), "Stamina starts above zero.");

            _input.InteractHeld = true;
            yield return WaitUntil(() => !target.Character.IsDowned);

            Assert.That(target.Character.IsDowned, Is.False);
            Assert.That(target.Character.IsAlive, Is.True);
            Assert.That(target.Character.Health, Is.EqualTo(1f));
            Assert.That(target.Stamina.CurrentStamina, Is.EqualTo(0f));
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(0f));
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(reviver.Recovery.RevivingTargetId.IsValid, Is.False, "The reviver is free again.");
        }

        [UnityTest]
        public IEnumerator ReviverMovement_Interrupts_AndRetryRestartsFromZero()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            SetField(target.Recovery, "_reviveDurationSeconds", 4f);
            yield return DownTarget(target);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            yield return WaitFrames(60);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted), "Still held and still.");

            _input.Move = Vector2.right;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.None);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "Voluntary movement interrupts.");
            Assert.That(target.Character.IsDowned, Is.True);

            _input.Move = Vector2.zero;
            _input.InteractHeld = false;
            yield return WaitFrames(10);
            reviver.Object.GetComponent<NetworkTransform>().Teleport(new Vector3(1f, 0f, 0f));
            yield return WaitFrames(3);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            float remaining = target.Recovery.Completion.RemainingTime(_runner) ?? 0f;
            Assert.That(remaining, Is.GreaterThan(3.5f), "The retry starts from the full duration.");
        }

        [UnityTest]
        public IEnumerator DownedMovement_Interrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero, localInput: true);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            yield return DownTarget(target);
            SetHeld(reviver, true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(10);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted), "Still while not moving.");

            _input.Move = new Vector2(0f, 1f);
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.None);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Character.IsDowned, Is.True);
        }

        [UnityTest]
        public IEnumerator DownedDisconnect_DoesNotInterrupt_AndRecoveryStillCompletes()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero, localInput: true);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Recovery, "_reviveDurationSeconds", 1f);
            yield return DownTarget(target);
            SetHeld(reviver, true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            // The Downed player's connection is gone: its avatar loses input authority but stays in the Raid.
            target.Object.RemoveInputAuthority();
            yield return WaitFrames(10);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted), "Downed disconnect never interrupts.");

            yield return WaitUntil(() => !target.Character.IsDowned);

            Assert.That(target.Character.IsDowned, Is.False);
            Assert.That(target.Character.Health, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Recovery_DoesNotReplayAnAttackHeldWhileDowned()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero, localInput: true);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Recovery, "_reviveDurationSeconds", 0.5f);
            // Without a participant the equipment controller clears the test attack strategy every tick.
            target.Object.GetComponent<PlayerWeaponEquipmentNetworkController>().enabled = false;
            PlayerCombatTestAttack attack = target.Object.gameObject.AddComponent<PlayerCombatTestAttack>();
            attack.Initialize(AttackType.Melee, 0f);
            var combat = target.Object.GetComponent<PlayerCombatNetworkController>();
            int previous = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetStrategy(combat, attack);
            yield return WaitUntil(() => _strategyDriver.CompletionSequence != previous);
            Assert.That(_strategyDriver.LastResult, Is.True);
            yield return DownTarget(target);
            _input.AttackHeld = true;
            yield return WaitFrames(10);
            Assert.That(attack.ExecutionCount, Is.Zero, "Attacks are refused while Downed.");

            SetHeld(reviver, true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitUntil(() => !target.Character.IsDowned);
            Assert.That(target.Character.IsDowned, Is.False);
            yield return WaitFrames(20);

            Assert.That(attack.ExecutionCount, Is.Zero, "The held press is not replayed after the recovery.");

            _input.AttackHeld = false;
            yield return WaitFrames(3);
            _input.AttackHeld = true;
            yield return WaitUntil(() => attack.ExecutionCount > 0);
            Assert.That(attack.ExecutionCount, Is.GreaterThan(0), "A fresh press attacks normally.");
        }

        private struct Avatar
        {
            internal NetworkObject Object;
            internal PlayerCharacter Character;
            internal PlayerDownedStateNetworkController Downed;
            internal PlayerDownedRecoveryNetworkController Recovery;
            internal PlayerInteractionNetworkController Interaction;
            internal PlayerStaminaNetworkController Stamina;
            internal PlayerLootReceiver Receiver;
        }

        private Avatar SpawnAvatar(Vector3 position, bool localInput = false)
        {
            LogAssert.Expect(UnityEngine.LogType.Error, MissingExtractionProgressDependenciesMessage);
            NetworkObject playerObject = localInput
                ? _runner.Spawn(_playerPrefab, position, Quaternion.identity, _runner.LocalPlayer)
                : _runner.Spawn(_playerPrefab, position, Quaternion.identity);
            var avatar = new Avatar
            {
                Object = playerObject,
                Character = playerObject.GetComponent<PlayerCharacter>(),
                Downed = playerObject.GetComponent<PlayerDownedStateNetworkController>(),
                Recovery = playerObject.GetComponent<PlayerDownedRecoveryNetworkController>(),
                Interaction = playerObject.GetComponent<PlayerInteractionNetworkController>(),
                Stamina = playerObject.GetComponent<PlayerStaminaNetworkController>(),
                Receiver = playerObject.GetComponent<PlayerLootReceiver>()
            };
            avatar.Recovery.TestTeamOverride = true;
            SetField(avatar.Downed, "_downedDrainPerSecond", 0f);
            SetField(avatar.Recovery, "_reviveDurationSeconds", 30f);
            _driver.AllowDowned = true;
            return avatar;
        }

        private IEnumerator DownTarget(Avatar target)
        {
            yield return Hit(target, 100000f);
            Assert.That(target.Character.IsDowned, Is.True);
            yield return WaitFrames(3);
        }

        private static void SetHeld(Avatar reviver, bool held)
        {
            SetNetworkedProperty(
                reviver.Interaction,
                nameof(PlayerInteractionNetworkController.IsInteractHeld),
                (NetworkBool)held);
        }

        private IEnumerator Hit(Avatar avatar, float amount)
        {
            _driver.Target = avatar.Character;
            _driver.Receiver = avatar.Receiver;
            _driver.SetEntries(Array.Empty<LootEntry>());
            _driver.RequestedDamageAmount = amount;
            _driver.IsRequested = true;
            int frames = 300;
            while (_driver.IsRequested && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(_driver.IsRequested, Is.False, "Driver request was not consumed by a simulation tick.");
            yield return WaitFrames(2);
        }

        /// <summary>Waits for simulation ticks, not rendered frames, which can outrun the tick rate.</summary>
        private IEnumerator WaitFrames(int ticks)
        {
            int target = _runner.Tick.Raw + ticks;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_runner.Tick.Raw < target && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_runner.Tick.Raw, Is.GreaterThanOrEqualTo(target), "Simulation did not advance.");
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
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

        private static void SetNetworkedProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }

        private IEnumerator StartRunnerAndLoadPlayer()
        {
            var runnerObject = new GameObject("PlayerDownedReviveFlowTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            _strategyDriver = runnerObject.AddComponent<PlayerCombatStrategySimulationDriver>();
            _input = runnerObject.AddComponent<FlowInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"downed-revive-flow-{Guid.NewGuid():N}",
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

        private sealed class FlowInputDriver : NetworkRunnerCallbacksAdapter
        {
            public bool InteractHeld { get; set; }
            public bool AttackHeld { get; set; }
            public Vector2 Move { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                PlayerNetworkInput playerInput = default;
                playerInput.MoveDirection = Move;
                playerInput.AimWorldPosition = new Vector2(10f, 0f);
                playerInput.Buttons.Set(PlayerInputButton.Interact, InteractHeld);
                playerInput.Buttons.Set(PlayerInputButton.PrimaryAttack, AttackHeld);
                input.Set(playerInput);
            }
        }
    }
}
#endif

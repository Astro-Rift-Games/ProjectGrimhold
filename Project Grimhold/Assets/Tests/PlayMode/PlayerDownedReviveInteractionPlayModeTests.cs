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
    /// Verifies the held Interact entry point of assisted recovery and the incompatible-action
    /// interruptions through the real interaction controller and the authoritative action choke
    /// points. The reviver is the runner's local player, so its Interact and movement come from
    /// real Fusion input; other avatars have no input authority.
    /// </summary>
    public sealed class PlayerDownedReviveInteractionPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string MissingExtractionProgressDependenciesMessage =
            "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.";

        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;
        private PlayerCombatStrategySimulationDriver _strategyDriver;
        private ReviveInputDriver _input;

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
        public IEnumerator HeldInteract_ByTeammateInRange_OpensSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            yield return DownTarget(target);

            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            Assert.That(target.Recovery.ReviverId, Is.EqualTo(reviver.Object.Id));
            Assert.That(reviver.Recovery.RevivingTargetId, Is.EqualTo(target.Object.Id));
            Assert.That(target.Character.IsDowned, Is.True);
        }

        [UnityTest]
        public IEnumerator HeldInteract_ByNonTeammate_DoesNotOpenSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            yield return DownTarget(target);
            target.Recovery.TestTeamOverride = false;

            _input.InteractHeld = true;
            yield return WaitFrames(20);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "Enemy cannot start.");

            // Sanity: the same input path works once the reviver is a teammate.
            target.Recovery.TestTeamOverride = true;
            _input.InteractHeld = false;
            yield return WaitFrames(3);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);
            Assert.That(target.Recovery.ReviverId, Is.EqualTo(reviver.Object.Id));
        }

        [UnityTest]
        public IEnumerator HeldInteract_OutOfReviveRange_DoesNotOpenSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1.9f, 0f, 0f), localInput: true);
            yield return DownTarget(target);

            _input.InteractHeld = true;
            yield return WaitFrames(20);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "1.9 u is beyond 1.5 u.");

            reviver.Object.GetComponent<NetworkTransform>().Teleport(new Vector3(1f, 0f, 0f));
            _input.InteractHeld = false;
            yield return WaitFrames(3);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
        }

        [UnityTest]
        public IEnumerator HeldInteract_ByDownedInteractor_IsRejectedByTheInteractionGate()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            SetField(reviver.Downed, "_downedDrainPerSecond", 0f);
            yield return DownTarget(target);
            yield return Hit(reviver, 100000f);
            Assert.That(reviver.Character.IsDowned, Is.True);
            InteractionPresentationEvent received = default;
            int results = 0;
            reviver.Interaction.InteractionResolved += result =>
            {
                results++;
                received = result;
            };

            _input.InteractHeld = true;
            yield return WaitUntil(() => results > 0);

            Assert.That(received.Success, Is.False);
            Assert.That(received.FailureReason, Is.EqualTo(InteractionFailureReason.InteractorUnavailable));
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator Interactable_RejectsSecondReviverSelfAndNonDownedTargets()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            Avatar second = SpawnAvatar(new Vector3(-1f, 0f, 0f));
            Avatar healthy = SpawnAvatar(new Vector3(0f, 1f, 0f));
            Assert.That(target.Interactable, Is.Not.Null, "NetworkPlayer.prefab needs the revive interactable.");
            Assert.That(
                CanInteract(target, reviver.Character.Id, target.Character.Id), Is.False,
                "A target that is not Downed is never interactable.");
            Assert.That(CanInteract(healthy, reviver.Character.Id, healthy.Character.Id), Is.False);
            yield return DownTarget(target);

            Assert.That(CanInteract(target, second.Character.Id, target.Character.Id), Is.True);
            Assert.That(
                CanInteract(target, target.Character.Id, target.Character.Id), Is.False,
                "A Downed avatar never targets itself.");

            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));

            Assert.That(
                CanInteract(target, second.Character.Id, target.Character.Id), Is.False,
                "1 reviver : 1 Downed.");
        }

        [UnityTest]
        public IEnumerator PrimaryAttack_ByReviver_InterruptsSessionAndAttackProceeds()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f), localInput: true);
            yield return DownTarget(target);
            _input.InteractHeld = true;
            yield return WaitUntil(() => target.Recovery.Kind == RecoveryKind.Assisted);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            PlayerCombatTestAttack attack = reviver.Object.gameObject.AddComponent<PlayerCombatTestAttack>();
            attack.Initialize(AttackType.Melee, 0f);
            // With no participant, the equipment controller re-applies "no weapon" every tick and would
            // clear the test attack strategy; this test is about the combat choke point only.
            reviver.Object.GetComponent<PlayerWeaponEquipmentNetworkController>().enabled = false;
            yield return SetStrategy(reviver.Object.GetComponent<PlayerCombatNetworkController>(), attack);
            _input.AttackHeld = true;
            yield return WaitUntil(() => attack.ExecutionCount > 0);

            Assert.That(attack.ExecutionCount, Is.GreaterThan(0), "The attack proceeds under normal rules.");
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Character.IsDowned, Is.True);
        }

        [UnityTest]
        public IEnumerator Consumable_ByReviver_InterruptsSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            yield return OpenSession(target, reviver);
            PlayerConsumableNetworkController consumable =
                reviver.Object.GetComponent<PlayerConsumableNetworkController>();

            Invoke(consumable, "ProcessAuthoritativeConsume", 0);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator EquipmentChange_ByReviver_InterruptsSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            yield return OpenSession(target, reviver);
            PlayerWeaponEquipmentNetworkController equipment =
                reviver.Object.GetComponent<PlayerWeaponEquipmentNetworkController>();

            Invoke(equipment, "TryUnequipAuthority", EquipmentSlot.Helmet);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator LootTransferAndDrop_ByReviver_InterruptSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            yield return OpenSession(target, reviver);
            var transfer = reviver.Object.GetComponent<PlayerLootTransferNetworkController>();
            var identity = new LootTransferRequestIdentity(
                1, new EntityId(9001), reviver.Character.Id, 0, LootTransferQuantityMode.FullStack);

            Invoke(transfer, "ProcessAuthoritativeRequest", identity);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "Transfer.");

            yield return OpenSession(target, reviver);
            var drop = reviver.Object.GetComponent<PlayerLootDropNetworkController>();
            var dropIdentity = new LootDropRequestIdentity(1, 0, LootTransferQuantityMode.FullStack);

            Invoke(drop, "ProcessAuthoritativeRequest", dropIdentity);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "Drop.");
        }

        [UnityTest]
        public IEnumerator ActionByAnotherAvatar_DoesNotInterruptSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            Avatar bystander = SpawnAvatar(new Vector3(-1f, 0f, 0f));
            yield return OpenSession(target, reviver);

            Invoke(bystander.Object.GetComponent<PlayerConsumableNetworkController>(), "ProcessAuthoritativeConsume", 0);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
        }

        private struct Avatar
        {
            internal NetworkObject Object;
            internal PlayerCharacter Character;
            internal PlayerDownedStateNetworkController Downed;
            internal PlayerDownedRecoveryNetworkController Recovery;
            internal PlayerInteractionNetworkController Interaction;
            internal DownedReviveInteractable Interactable;
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
                Interactable = playerObject.GetComponent<DownedReviveInteractable>(),
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

        private IEnumerator OpenSession(Avatar target, Avatar reviver)
        {
            if (!target.Character.IsDowned)
            {
                yield return DownTarget(target);
            }

            SetNetworkedProperty(
                reviver.Interaction,
                nameof(PlayerInteractionNetworkController.IsInteractHeld),
                (NetworkBool)true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
        }

        private bool CanInteract(Avatar owner, EntityId interactor, EntityId targetId)
        {
            var request = new InteractionRequest(interactor, targetId, _runner.Tick);
            return owner.Interactable.CanInteract(request);
        }

        private IEnumerator SetStrategy(PlayerCombatNetworkController controller, MonoBehaviour attack)
        {
            int previous = _strategyDriver.CompletionSequence;
            _strategyDriver.RequestSetStrategy(controller, attack);
            yield return WaitUntil(() => _strategyDriver.CompletionSequence != previous);
            Assert.That(_strategyDriver.LastResult, Is.True);
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

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            return info.Invoke(target, arguments);
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
            var runnerObject = new GameObject("PlayerDownedReviveInteractionTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            _strategyDriver = runnerObject.AddComponent<PlayerCombatStrategySimulationDriver>();
            _input = runnerObject.AddComponent<ReviveInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"downed-revive-interaction-{Guid.NewGuid():N}",
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

        private sealed class ReviveInputDriver : NetworkRunnerCallbacksAdapter
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

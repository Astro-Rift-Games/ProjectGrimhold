#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    public sealed class PlayerAbilityRuntimeNetworkControllerPlayModeTests
    {
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private AbilityInputDriver _inputDriver;

        [UnityTest]
        public IEnumerator AbilityIntent_IndependentPressEdgesAreTickScopedAndDoNotRepeatWhileHeld()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-input",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"))), true);
            yield return WaitUntil(() => runtime.IsInitialized);
            _driver.ObservedRuntime = runtime;
            _inputDriver.Buttons.Set((PlayerInputButton)6, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
            Assert.That(_driver.Slot2Requests, Is.Zero);
            Assert.That(runtime.WasActivationRequested(UniversalAbilitySlot.Slot1), Is.False,
                "A render/coroutine read must not expose a previous simulation request.");
            _inputDriver.Buttons.Set((PlayerInputButton)7, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
            Assert.That(_driver.Slot2Requests, Is.EqualTo(1));
            _inputDriver.Buttons = default;
            yield return WaitTicks();
            _inputDriver.Buttons.Set((PlayerInputButton)6, true);
            _inputDriver.Buttons.Set((PlayerInputButton)7, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(2));
            Assert.That(_driver.Slot2Requests, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AbilityIntent_MissingInputDoesNotReplayHeldButtons()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-missing-input",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"))), true);
            yield return WaitUntil(() => runtime.IsInitialized);
            _driver.ObservedRuntime = runtime;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
            _inputDriver.ProvidePayload = false;
            yield return WaitTicks();
            Assert.That(_driver.MissingInputTicks, Is.GreaterThan(0), "The fixture must actually omit Fusion input.");
            Assert.That(ReadPreviousAbilityButtons(runtime).IsSet(PlayerInputButton.AbilitySlot1), Is.True);
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
            _inputDriver.ProvidePayload = true;
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbilityIntent_PendingBindingConsumesPressAndEmptySlotNeverRequests()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-pending-input",
                new PreparedAbilityLoadout(new AbilityId("charge"), default));
            var runtime = SpawnAvatar(participant, false);
            _driver.ObservedRuntime = runtime;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot2, true);
            yield return WaitTicks();
            Assert.That(ReadPreviousAbilityButtons(runtime).IsSet(PlayerInputButton.AbilitySlot1), Is.True);
            Assert.That(participant.TrySetCurrentAvatar(runtime.Object), Is.True);
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.Zero);
            Assert.That(_driver.Slot2Requests, Is.Zero);
            _inputDriver.Buttons = default;
            yield return WaitTicks();
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot2, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
            Assert.That(_driver.Slot2Requests, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AbilityIntent_ReenableBaselinesHeldInputWithoutReplay()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-input-reenable",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"))), true);
            yield return WaitUntil(() => runtime.IsInitialized);
            _driver.ObservedRuntime = runtime;
            runtime.enabled = false;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            runtime.enabled = true;
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.Zero);
            _inputDriver.Buttons = default;
            yield return WaitTicks();
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbilityIntent_CopiedHistoryIsPreservedAndRestoreBaselinesFirstInput()
        {
            yield return StartRunner();
            var loadout = new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"));
            var source = SpawnAvatar(SpawnParticipant("ability-input-copy-source", loadout), true);
            yield return WaitUntil(() => source.IsInitialized);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            var target = SpawnAvatar(null, false);
            SetRestoreGuard(target);
            yield return CopyState(target, source);
            Assert.That(ReadPreviousAbilityButtons(target).IsSet(PlayerInputButton.AbilitySlot1), Is.True);
            var participant = SpawnParticipant("ability-input-copy-target", loadout);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            _driver.ObservedRuntime = target;
            yield return WaitUntil(() => target.IsInitialized);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.Zero);
            _inputDriver.Buttons = default;
            yield return WaitTicks();
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(_driver.Slot1Requests, Is.EqualTo(1));
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
        }

        [UnityTest]
        public IEnumerator FreshPair_IsFrozenAndSlotsResolveIndependently()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-fresh",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap")));
            var runtime = SpawnAvatar(participant, true);
            yield return WaitUntil(() => runtime.IsInitialized);
            AssertSlot(runtime, UniversalAbilitySlot.Slot1, "charge");
            AssertSlot(runtime, UniversalAbilitySlot.Slot2, "trap");
            Assert.That(runtime.IsSlotAvailable((UniversalAbilitySlot)99), Is.False);
        }

        [UnityTest]
        public IEnumerator EmptyPair_InitializesWithoutInventingAnAbility()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-empty", default), true);
            yield return WaitUntil(() => runtime.IsInitialized);
            foreach (var slot in new[] { UniversalAbilitySlot.Slot1, UniversalAbilitySlot.Slot2 })
            {
                Assert.That(runtime.TryGetSlot(slot, out var state), Is.True);
                Assert.That(state.IsPrepared, Is.False);
                Assert.That(runtime.IsSlotAvailable(slot), Is.False);
                Assert.That(state.Definition, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator PendingCurrentAvatar_WaitsThenInitializes()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-pending", default);
            var runtime = SpawnAvatar(participant, false);
            yield return WaitTicks();
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(ReadMarker(runtime), Is.False);
            Assert.That(participant.TrySetCurrentAvatar(runtime.Object), Is.True);
            yield return WaitUntil(() => runtime.IsInitialized);
        }

        [UnityTest]
        public IEnumerator TerminalParticipant_CleansUpWithoutReinitializing()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-terminal", default);
            var runtime = SpawnAvatar(participant, true);
            yield return WaitUntil(() => runtime.IsInitialized);
            Assert.That(participant.TryMarkDefeated(runtime.Object), Is.True);
            yield return WaitTicks();
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(ReadMarker(runtime), Is.False);
            Assert.That(runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator GenerationChange_CleansUpInsteadOfRebinding()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-generation", default);
            var runtime = SpawnAvatar(participant, true);
            yield return WaitUntil(() => runtime.IsInitialized);
            typeof(NetworkRaidParticipant).GetProperty("RaidGenerationId")
                .SetValue(participant, (NetworkString<_32>)"another-generation");
            yield return WaitTicks();
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(ReadMarker(runtime), Is.False);
        }

        [UnityTest]
        public IEnumerator DisableAndReenable_RebindsWithoutResettingConfirmedState()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-reenable", default), true);
            yield return WaitUntil(() => runtime.IsInitialized);
            runtime.enabled = false;
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(ReadMarker(runtime), Is.True);
            runtime.enabled = true;
            yield return WaitUntil(() => runtime.IsInitialized);
        }

        [UnityTest]
        public IEnumerator CopiedInitialization_WaitsForFixupThenRebinds()
        {
            yield return StartRunner();
            var source = SpawnAvatar(SpawnParticipant("ability-source", default), true);
            yield return WaitUntil(() => source.IsInitialized);
            var target = SpawnAvatar(null, false);
            SetRestoreGuard(target);
            yield return CopyState(target, source);
            yield return WaitTicks();
            Assert.That(target.IsInitialized, Is.False);
            Assert.That(ReadMarker(target), Is.True);
            var participant = SpawnParticipant("ability-restored", default);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitUntil(() => target.IsInitialized);
            Assert.That(ReadMarker(target), Is.True);
        }

        [UnityTest]
        public IEnumerator CopiedUninitializedState_IsNotOverwrittenByFreshInitialization()
        {
            yield return StartRunner();
            var source = SpawnAvatar(null, false);
            var target = SpawnAvatar(null, false);
            SetRestoreGuard(target);
            yield return CopyState(target, source);
            var participant = SpawnParticipant("ability-uninitialized-restore", default);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitTicks();
            Assert.That(ReadMarker(target), Is.False);
            Assert.That(target.IsInitialized, Is.False);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("AbilityRuntimeTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _inputDriver = runnerObject.AddComponent<AbilityInputDriver>();
            _runner.AddCallbacks(_inputDriver);
            _runner.ProvideInput = true;
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-runtime-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkRaidParticipant SpawnParticipant(string profile, PreparedAbilityLoadout prepared)
        {
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var participantId);
            var participant = _runner.Spawn(LoadPrefab("c39d451563bae6e43934008a0dadc6d6"),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    profile, participantId, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: prepared));
            return participant.GetComponent<NetworkRaidParticipant>();
        }

        private PlayerAbilityRuntimeNetworkController SpawnAvatar(NetworkRaidParticipant participant, bool publishAvatar)
        {
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
            var avatar = _runner.Spawn(LoadPrefab("fea3a7b256f965a4eb9b965832939741"),
                Vector3.zero, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) => instance.GetComponent<RaidAvatarParticipantLink>()
                    .Initialize(participant != null ? participant.Object : null));
            if (publishAvatar) Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            var runtime = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            Assert.That(runtime, Is.Not.Null);
            return runtime;
        }

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private IEnumerator CopyState(PlayerAbilityRuntimeNetworkController target,
            PlayerAbilityRuntimeNetworkController source)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestCopyState(target, source);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private IEnumerator WaitTicks()
        {
            int until = _runner.Tick.Raw + 3;
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Ability runtime did not reach the expected state.");
        }

        private static void AssertSlot(PlayerAbilityRuntimeNetworkController runtime, UniversalAbilitySlot slot, string id)
        {
            Assert.That(runtime.TryGetSlot(slot, out var state), Is.True);
            Assert.That(state.AbilityId, Is.EqualTo(new AbilityId(id)));
            Assert.That(state.Definition.Id, Is.EqualTo(id));
            Assert.That(runtime.IsSlotAvailable(slot), Is.True);
        }

        private static bool ReadMarker(PlayerAbilityRuntimeNetworkController runtime) =>
            (NetworkBool)typeof(PlayerAbilityRuntimeNetworkController)
                .GetProperty("InitializationConfirmed", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runtime);

        private static NetworkButtons ReadPreviousAbilityButtons(PlayerAbilityRuntimeNetworkController runtime) =>
            (NetworkButtons)typeof(PlayerAbilityRuntimeNetworkController)
                .GetProperty("PreviousAbilityButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);

        private static void SetRestoreGuard(PlayerAbilityRuntimeNetworkController runtime) =>
            typeof(PlayerAbilityRuntimeNetworkController)
                .GetField("_restoreSpawn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, true);

        private sealed class AbilityInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;
            public bool ProvidePayload = true;

            public override void OnInput(NetworkRunner runner, NetworkInput input)
            {
                if (!ProvidePayload) return;
                input.Set(new PlayerNetworkInput { Buttons = Buttons });
            }
        }
    }
}
#endif

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
    /// Verifies the assisted recovery session owner with two or three real player avatars.
    /// Team membership and the reviver's held Interact are staged through test seams.
    /// </summary>
    public sealed class PlayerDownedRecoveryPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";

        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private PlayerCorpseGenerationSimulationDriver _driver;

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
        public IEnumerator AllyDamage_DownedTarget_PreservesReserveAndRevive()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            float reserve = target.Downed.DownedHealth;
            _driver.RequestedAllyDirectDamage = true;
            yield return Hit(target, 100000f);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserve));
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            Assert.That(_driver.FirstResult.AppliedDamage, Is.Zero);

            // A zero-effective hit on the reviver also leaves health-loss polling unchanged.
            yield return Hit(reviver, 100000f);
            Assert.That(reviver.Character.Health, Is.EqualTo(1f));
            // That first hit did lose health, so open a fresh session at the floor.
            if (target.Recovery.Kind == RecoveryKind.None)
                Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return Hit(reviver, 100000f);
            Assert.That(_driver.FirstResult.AppliedDamage, Is.Zero);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
        }

        [UnityTest]
        public IEnumerator Defaults_AreFourSecondsAndOnePointFiveUnits()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);

            Assert.That(GetField<float>(target.Recovery, "_reviveDurationSeconds"), Is.EqualTo(4f));
            Assert.That(GetField<float>(target.Recovery, "_reviveRange"), Is.EqualTo(1.5f));
        }

        [UnityTest]
        public IEnumerator TryBeginAssisted_ValidTeammateInRange_OpensSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            yield return DownAndHoldInteract(target, reviver);

            bool started = target.Recovery.TryBeginAssisted(reviver.Character);

            Assert.That(started, Is.True);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));
            Assert.That(target.Recovery.SessionCycle, Is.EqualTo(target.Downed.DownedCycle));
            Assert.That(target.Recovery.IsDrainPaused, Is.True);
        }

        [UnityTest]
        public IEnumerator TryBeginAssisted_RejectsEnemyOutOfRangeAndNonDowned()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar near = SpawnAvatar(new Vector3(1f, 0f, 0f));
            Avatar far = SpawnAvatar(new Vector3(5f, 0f, 0f));
            Avatar healthy = SpawnAvatar(new Vector3(0.5f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            target.Recovery.TestTeamOverride = true;

            Assert.That(target.Recovery.TryBeginAssisted(near.Character), Is.False, "Target is not Downed yet.");
            yield return Hit(target, 100000f);

            Assert.That(target.Recovery.TryBeginAssisted(far.Character), Is.False, "Out of range.");
            target.Recovery.TestTeamOverride = false;
            Assert.That(target.Recovery.TryBeginAssisted(near.Character), Is.False, "Enemy.");
            target.Recovery.TestTeamOverride = true;
            yield return Hit(healthy, 100000f);
            Assert.That(target.Recovery.TryBeginAssisted(healthy.Character), Is.False, "Reviver is Downed.");
            Assert.That(target.Recovery.TryBeginAssisted(target.Character), Is.False, "Self.");
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator TryBeginAssisted_SecondReviverAndBusyReviverAreRejected()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar first = SpawnAvatar(Vector3.zero);
            Avatar second = SpawnAvatar(new Vector3(0.5f, 0f, 0f));
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            Avatar other = SpawnAvatar(new Vector3(1.2f, 0f, 0f));
            SetField(first.Downed, "_downedDrainPerSecond", 0f);
            SetField(second.Downed, "_downedDrainPerSecond", 0f);
            first.Recovery.TestTeamOverride = true;
            second.Recovery.TestTeamOverride = true;
            yield return Hit(first, 100000f);
            yield return Hit(second, 100000f);
            Assert.That(first.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            Assert.That(first.Recovery.TryBeginAssisted(other.Character), Is.False, "1 reviver : 1 Downed.");
            Assert.That(second.Recovery.TryBeginAssisted(reviver.Character), Is.False, "Reviver already busy.");
            Assert.That(second.Recovery.TryBeginAssisted(other.Character), Is.True);
        }

        [UnityTest]
        public IEnumerator Session_PausesDrain_AndDamageStillReducesReserveAndInterrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 10f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return Hit(target, 100000f);
            target.Recovery.TestTeamOverride = true;
            SetHeld(reviver, true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            float reserveAtStart = target.Downed.DownedHealth;

            yield return WaitFrames(30);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserveAtStart).Within(0.001f), "Drain paused.");
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));

            SetField(target.Downed, "_downedDamageMultiplier", 1f);
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            yield return Hit(target, 10f);

            Assert.That(target.Downed.DownedHealth, Is.EqualTo(reserveAtStart - 10f).Within(0.001f));
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None), "Damage interrupts.");
            Assert.That(target.Recovery.LastOutcomeReason, Is.EqualTo(DownedRecoveryInterruptReason.DownedDamaged));
            float afterInterrupt = target.Downed.DownedHealth;
            SetField(target.Downed, "_downedDrainPerSecond", 10f);
            yield return WaitFrames(30);
            Assert.That(target.Downed.DownedHealth, Is.LessThan(afterInterrupt), "Drain resumes.");
        }

        [UnityTest]
        public IEnumerator Completion_AfterDuration_RestoresOneHealthAndClearsSessionOnce()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 0.3f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            yield return WaitUntil(() => !target.Character.IsDowned, 120);

            Assert.That(target.Character.IsDowned, Is.False);
            Assert.That(target.Character.IsAlive, Is.True);
            Assert.That(target.Character.Health, Is.EqualTo(1f));
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Recovery.IsDrainPaused, Is.False);
            Assert.That(target.Downed.DownedHealth, Is.EqualTo(0f));
            AssertOutcome(target, RecoveryOutcomeKind.Completed, DownedRecoveryInterruptReason.None, reviver, 1);
        }

        [UnityTest]
        public IEnumerator ReleasingInteract_InterruptsAndDiscardsProgress()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(5);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.Assisted));

            SetHeld(reviver, false);
            yield return WaitFrames(5);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Character.IsDowned, Is.True);
            AssertOutcome(target, RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.InteractReleased, reviver, 1);

            SetHeld(reviver, true);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True, "Retry starts from zero.");
            Assert.That((bool)target.Recovery.Completion.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator LeavingRange_Interrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            reviver.Object.GetComponent<NetworkTransform>().Teleport(new Vector3(4f, 0f, 0f));
            yield return WaitFrames(5);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            AssertOutcome(target, RecoveryOutcomeKind.Interrupted, DownedRecoveryInterruptReason.OutOfRange, reviver, 1);
        }

        [UnityTest]
        public IEnumerator ReviverDespawn_Interrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            _runner.Despawn(reviver.Object);
            yield return WaitFrames(5);

            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Character.IsDowned, Is.True);
            Assert.That(target.Recovery.LastOutcomeKind, Is.EqualTo(RecoveryOutcomeKind.Interrupted));
            Assert.That(target.Recovery.LastOutcomeReason, Is.EqualTo(DownedRecoveryInterruptReason.ReviverDisconnected));
        }

        [UnityTest]
        public IEnumerator ReviverBecomingDowned_Interrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(reviver.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            yield return Hit(reviver, 100000f);
            yield return WaitFrames(3);

            Assert.That(reviver.Character.IsDowned, Is.True);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator DamageToReviver_Interrupts()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(3);

            yield return Hit(reviver, 1f);
            yield return WaitFrames(3);

            Assert.That(reviver.Character.IsDowned, Is.False);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
        }

        [UnityTest]
        public IEnumerator DownedDefeatDuringSession_ClearsSession()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);

            yield return Hit(target, 100000f);
            yield return WaitFrames(3);

            Assert.That(target.Character.IsAlive, Is.False);
            Assert.That(target.Recovery.Kind, Is.EqualTo(RecoveryKind.None));
            Assert.That(target.Recovery.IsDrainPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator Progress_IsReportedWhileSessionRuns_AndAbsentOtherwise()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            yield return DownAndHoldInteract(target, reviver);
            Assert.That(target.Recovery.TryGetProgress(out _), Is.False, "No session yet.");

            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(5);

            Assert.That(target.Recovery.TryGetProgress(out float progress), Is.True);
            Assert.That(progress, Is.InRange(0f, 1f));
            SetHeld(reviver, false);
            yield return WaitFrames(5);
            Assert.That(target.Recovery.TryGetProgress(out _), Is.False, "Interrupt discards progress.");
        }

        [UnityTest]
        public IEnumerator DownedHud_ShowsReserveProgressAndInterruptionNotice_ThenSilentRebind()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 30f);
            RaidDownedHudPresenter presenter = target.Object.GetComponentInChildren<RaidDownedHudPresenter>(true);
            RaidDownedHudView view = target.Object.GetComponentInChildren<RaidDownedHudView>(true);
            Assert.That(presenter, Is.Not.Null);
            presenter.Bind(target.Character, null);
            presenter.Refresh();
            Assert.That(IsActive(view, "_downedRoot"), Is.False, "Active players show no Downed HUD.");

            yield return DownAndHoldInteract(target, reviver);
            presenter.Refresh();
            Assert.That(IsActive(view, "_downedRoot"), Is.True);
            Assert.That(IsActive(view, "_reviveRoot"), Is.False);

            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(3);
            presenter.Refresh();
            Assert.That(IsActive(view, "_reviveRoot"), Is.True, "Progress shown during the session.");

            SetHeld(reviver, false);
            yield return WaitFrames(3);
            presenter.Refresh();
            Assert.That(IsActive(view, "_reviveRoot"), Is.False, "Progress returns to zero.");
            Assert.That(IsActive(view, "_messageText"), Is.True, "Interruption notice shown.");

            presenter.Bind(target.Character, null);
            Assert.That(IsActive(view, "_messageText"), Is.False, "Rebind must not replay the old outcome.");
        }

        [UnityTest]
        public IEnumerator DownedHud_ReviverSide_ShowsAllyRangeProgressAndNotices()
        {
            yield return StartRunnerAndLoadPlayer();
            Avatar target = SpawnAvatar(Vector3.zero);
            Avatar reviver = SpawnAvatar(new Vector3(1f, 0f, 0f));
            SetField(target.Downed, "_downedDrainPerSecond", 0f);
            SetField(target.Recovery, "_reviveDurationSeconds", 0.4f);
            RaidDownedHudPresenter presenter = reviver.Object.GetComponentInChildren<RaidDownedHudPresenter>(true);
            RaidDownedHudView view = reviver.Object.GetComponentInChildren<RaidDownedHudView>(true);
            presenter.TestAllyOverride = target.Character;
            presenter.Bind(reviver.Character, null);
            presenter.Refresh();
            Assert.That(IsActive(view, "_allyDownedRoot"), Is.False, "No ally is Downed yet.");

            yield return DownAndHoldInteract(target, reviver);
            presenter.Refresh();
            Assert.That(IsActive(view, "_allyDownedRoot"), Is.True, "Ally Downed and in range.");
            Assert.That(IsActive(view, "_downedRoot"), Is.False, "The reviver is not Downed.");

            Assert.That(target.Recovery.TryBeginAssisted(reviver.Character), Is.True);
            yield return WaitFrames(3);
            presenter.Refresh();
            Assert.That(IsActive(view, "_reviveRoot"), Is.True, "Reviver sees the progress.");

            yield return WaitUntil(() => !target.Character.IsDowned, 120);
            presenter.Refresh();
            Assert.That(IsActive(view, "_reviveRoot"), Is.False);
            Assert.That(IsActive(view, "_allyDownedRoot"), Is.False);
            Assert.That(IsActive(view, "_messageText"), Is.True, "Completion notice reaches the reviver.");
        }

        private static bool IsActive(RaidDownedHudView view, string field)
        {
            object value = GetField<object>(view, field);
            GameObject go = value is Component component ? component.gameObject : (GameObject)value;
            return go.activeSelf;
        }

        private static void AssertOutcome(
            Avatar target,
            RecoveryOutcomeKind kind,
            DownedRecoveryInterruptReason reason,
            Avatar reviver,
            int sequence)
        {
            Assert.That(target.Recovery.LastOutcomeKind, Is.EqualTo(kind));
            Assert.That(target.Recovery.LastOutcomeReason, Is.EqualTo(reason));
            Assert.That(target.Recovery.LastOutcomeReviverId, Is.EqualTo(reviver.Object.Id));
            Assert.That(target.Recovery.LastOutcomeSequence, Is.EqualTo(sequence));
        }

        private struct Avatar
        {
            internal NetworkObject Object;
            internal PlayerCharacter Character;
            internal PlayerDownedStateNetworkController Downed;
            internal PlayerDownedRecoveryNetworkController Recovery;
            internal PlayerInteractionNetworkController Interaction;
            internal PlayerLootReceiver Receiver;
        }

        private Avatar SpawnAvatar(Vector3 position)
        {
            NetworkObject playerObject = _runner.Spawn(_playerPrefab, position, Quaternion.identity);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, playerObject);
            var avatar = new Avatar
            {
                Object = playerObject,
                Character = playerObject.GetComponent<PlayerCharacter>(),
                Downed = playerObject.GetComponent<PlayerDownedStateNetworkController>(),
                Recovery = playerObject.GetComponent<PlayerDownedRecoveryNetworkController>(),
                Interaction = playerObject.GetComponent<PlayerInteractionNetworkController>(),
                Receiver = playerObject.GetComponent<PlayerLootReceiver>()
            };
            Assert.That(avatar.Recovery, Is.Not.Null, "Player prefab must contain the recovery controller.");
            avatar.Recovery.TestTeamOverride = true;
            _driver.AllowDowned = true;
            return avatar;
        }

        private IEnumerator DownAndHoldInteract(Avatar target, Avatar reviver)
        {
            yield return Hit(target, 100000f);
            SetHeld(reviver, true);
            Assert.That(target.Character.IsDowned, Is.True);
        }

        private static void SetHeld(Avatar reviver, bool held)
        {
            SetNetworkedProperty(reviver.Interaction, nameof(PlayerInteractionNetworkController.IsInteractHeld), (NetworkBool)held);
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

        private static IEnumerator WaitUntil(Func<bool> condition, int maxFrames)
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

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
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
            var runnerObject = new GameObject("PlayerDownedRecoveryTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"downed-recovery-{Guid.NewGuid():N}",
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
    }
}
#endif

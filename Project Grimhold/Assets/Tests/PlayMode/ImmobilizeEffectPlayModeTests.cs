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
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>
    /// Proof of the Trap's immobilize + periodic damage effect on a real runner, real creatures and the real
    /// damage pipeline. Creatures are inert (FSM, combat and movement disabled) unless the movement is under test;
    /// attackers are other inert creatures carrying a recording damage sink.
    /// </summary>
    public sealed class ImmobilizeEffectPlayModeTests
    {
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const float Interval = 0.25f;
        private const float Duration = 1f;

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private readonly List<GameObject> _sceneObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var sceneObject in _sceneObjects)
                if (sceneObject != null) Object.Destroy(sceneObject);
            _sceneObjects.Clear();
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator StunIndicator_ObservesReplicatedStatus_AndHidesAfterExpiry()
        {
            yield return Begin();
            var enemy = SpawnEnemy(Vector3.zero);
            var stun = enemy.GetComponent<EnemyStunNetworkEffect>();
            IStunStatus status = stun;
            var presenter = enemy.GetComponent<StunStatusIndicatorPresenter>();
            Assert.That(presenter, Is.Not.Null);
            var indicator = (GameObject)typeof(StunStatusIndicatorPresenter)
                .GetField("_indicator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            yield return null;
            Assert.That(indicator.activeSelf, Is.False);
            yield return InSimulation(() => Assert.That(stun.TryApply(0.5f), Is.True));
            yield return null;
            yield return null;
            Assert.That(status.IsStunned, Is.True);
            Assert.That(status.RemainingStunSeconds, Is.GreaterThan(0f));
            Assert.That(indicator.activeSelf, Is.True);
            var visibility = enemy.GetComponent<ProjectGrimhold.Gameplay.Visibility.EntityVisibilityPresenter>();
            visibility.ApplyVisibility(0f);
            Assert.That(indicator.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True);
            yield return WaitUntil(() => !status.IsStunned);
            yield return null;
            yield return null;
            Assert.That(status.RemainingStunSeconds, Is.Zero);
            Assert.That(indicator.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Stun_BlocksMovement_LeavesImmobilizeIndependent_AndExpires()
        {
            yield return Begin();
            var target = SpawnPatrollingEnemy(Vector3.zero);
            var movement = target.GetComponent<EnemyMovementAIController>();
            var stun = target.GetComponent("EnemyStunNetworkEffect");
            Assert.That(stun, Is.Not.Null);
            Vector3 start = target.transform.position;
            yield return WaitUntil(() => Vector3.Distance(start, target.transform.position) > 0.3f);
            yield return InSimulation(() => Assert.That((bool)stun.GetType().GetMethod("TryApply").Invoke(stun, new object[] { 1f }), Is.True));
            // Let NetworkTransform presentation settle after the final pre-Stun motor step.
            yield return WaitTicks(2);
            Vector3 held = target.transform.position;
            yield return WaitTicks(20);
            Assert.That(Vector3.Distance(held, target.transform.position), Is.LessThan(0.001f));
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That((bool)movement.IsAttacking, Is.False);
            yield return InSimulation(() => movement.TryApplyKnockback(Vector2.right, 30f, true));
            yield return WaitTicks(10);
            Assert.That(Vector3.Distance(held, target.transform.position), Is.GreaterThan(0.5f));
            yield return WaitUntil(() => !(bool)stun.GetType().GetProperty("IsActive").GetValue(stun));
            Vector3 released = target.transform.position;
            yield return WaitTicks(30);
            Assert.That(Vector3.Distance(released, target.transform.position), Is.GreaterThan(0.3f));
        }

        [UnityTest]
        public IEnumerator Specter_StunDuringChannel_CancelsDamageAndWaitsInPursuit()
        {
            yield return Begin();
            var player = _runner.Spawn(LoadPrefab("fea3a7b256f965a4eb9b965832939741"), Vector3.zero, Quaternion.identity)
                .GetComponent<PlayerCharacter>();
            var enemy = _runner.Spawn(LoadPrefab("35da693761f9d7244bac33c1559a3918"), new Vector3(2f, 0f, 0f), Quaternion.identity);
            var specter = enemy.GetComponent<ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController>();
            var stun = enemy.GetComponent<EnemyStunNetworkEffect>();
            float health = player.Health;
            Assert.That(health, Is.GreaterThan(0f));
            yield return InSimulation(() =>
            {
                specter.CurrentState = ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Desvanecido;
                Assert.That(stun.TryApply(2f), Is.False, "An untargetable Specter rejects Stun.");
                Assert.That(stun.RemainingSeconds, Is.Zero);
                specter.AttackTargetPosition = player.transform.position;
                specter.TransitionTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Canalizacion);
                Assert.That(stun.TryApply(2f), Is.True);
                Assert.That(specter.CurrentState, Is.EqualTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Persecucion));
            });
            Vector3 held = enemy.transform.position;
            yield return WaitTicks(Mathf.CeilToInt(1.5f / _runner.DeltaTime));
            Assert.That(stun.IsActive, Is.True);
            Assert.That(specter.CurrentState, Is.EqualTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Persecucion));
            Assert.That(enemy.transform.position, Is.EqualTo(held));
            Assert.That(player.Health, Is.EqualTo(health), "The cancelled channel cannot detonate.");
            Assert.That(enemy.GetComponentInChildren<LineRenderer>().enabled, Is.False, "The cancelled channel has no marker.");
        }

        [UnityTest]
        public IEnumerator Stun_RejectsOutsideSimulationAndInvalidDurations_ReapplicationReplacesDeadline_DeathEndsIt()
        {
            yield return Begin();
            var enemy = SpawnEnemy(Vector3.zero);
            var stun = enemy.GetComponent<EnemyStunNetworkEffect>();
            Assert.That(stun.TryApply(5f), Is.False);
            yield return InSimulation(() =>
            {
                Assert.That(stun.TryApply(5f), Is.True);
                float remaining = stun.RemainingSeconds;
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                {
                    Assert.That(stun.TryApply(invalid), Is.False);
                    Assert.That(stun.RemainingSeconds, Is.EqualTo(remaining));
                }
                Assert.That(stun.TryApply(0.5f), Is.True);
                Assert.That(stun.RemainingSeconds, Is.EqualTo(0.5f).Within(_runner.DeltaTime));
            });
            yield return WaitUntil(() => !stun.IsActive);
            yield return InSimulation(() =>
            {
                Assert.That(stun.TryApply(5f), Is.True);
                SetNetworked(enemy, "Health", 0f, typeof(CharacterBase));
                Assert.That(stun.IsActive, Is.False);
                Assert.That(stun.TryApply(5f), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator Specter_ReactiveDisplacementWhileStunned_WaitsUntilExpiry()
        {
            yield return Begin();
            var enemy = _runner.Spawn(LoadPrefab("35da693761f9d7244bac33c1559a3918"), Vector3.zero, Quaternion.identity);
            var specter = enemy.GetComponent<ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController>();
            var stun = enemy.GetComponent<EnemyStunNetworkEffect>();
            yield return InSimulation(() =>
            {
                specter.TransitionTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Persecucion);
                Assert.That(stun.TryApply(0.5f), Is.True);
                specter.OnDamageReceived(new EntityId(123), 1f, DamageType.Magical, false);
                Assert.That(specter.CurrentState, Is.EqualTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Persecucion),
                    "Reactive displacement cannot advance the FSM while stunned.");
            });
            yield return WaitTicks(Mathf.CeilToInt(0.25f / _runner.DeltaTime));
            Assert.That(specter.CurrentState, Is.EqualTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Persecucion));
            yield return WaitUntil(() => !stun.IsActive);
            yield return WaitTicks(2);
            Assert.That(specter.CurrentState, Is.EqualTo(ProjectGrimhold.Gameplay.Enemies.Specter.SpecterStateType.Desvanecimiento));
        }

        // ---- Immobilize ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Apply_BlocksVoluntaryMovement_ButKnockbackStillDisplaces_AndEndingRestoresMovement()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f));
            var target = SpawnPatrollingEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();

            // Harness proof: the creature walks on its own before the effect.
            Vector3 start = target.transform.position;
            yield return WaitUntil(() => (target.transform.position - start).magnitude > 0.3f);

            bool applied = false;
            yield return Apply(effect, source.Id, 30f, 0f, Interval, DamageType.Physical, true, r => applied = r);
            yield return WaitTicks(2);
            Assert.That(applied, Is.True);
            Assert.That((bool)movement.IsImmobilized, Is.True);
            Vector3 held = target.transform.position;
            yield return WaitTicks(30);
            Assert.That((target.transform.position - held).magnitude, Is.LessThan(0.001f),
                "An immobilized creature must not walk on its own.");

            yield return InSimulation(() => movement.TryApplyKnockback(Vector2.right, 30f, true));
            yield return WaitTicks(10);
            Assert.That((target.transform.position - held).magnitude, Is.GreaterThan(0.5f),
                "Forced displacement stays possible while immobilized.");

            yield return InSimulation(() => effect.Cancel());
            yield return WaitTicks(30);
            Vector3 released = target.transform.position;
            yield return WaitTicks(30);
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That((target.transform.position - released).magnitude, Is.GreaterThan(0.3f),
                "Voluntary movement resumes once the effect ends.");
        }

        [UnityTest]
        public IEnumerator Immobilize_DoesNotDependOnControlEnabled()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f));
            var target = SpawnPatrollingEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();

            yield return Apply(effect, source.Id, 30f, 0f, Interval, DamageType.Physical, true);
            // A state transition rewrites control on every Enter; the effect must survive it.
            yield return InSimulation(() => movement.TrySetControlEnabled(true));
            yield return WaitTicks(2);
            Vector3 held = target.transform.position;
            yield return WaitTicks(20);

            Assert.That((target.transform.position - held).magnitude, Is.LessThan(0.001f));
            Assert.That((bool)movement.IsControlEnabled, Is.True);
            Assert.That((bool)movement.IsImmobilized, Is.True);
        }

        // ---- Periodic damage ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Ticks_AtTheConfiguredInterval_ForExactlyTheDuration_ThenEverythingStops()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();
            float health = target.Health;
            int appliedAt = 0;

            yield return Apply(effect, source.Id, Duration, 2f, Interval, DamageType.Magical, true, appliedAtTick: t => appliedAt = t);
            yield return WaitUntil(() => !effect.IsActive);
            yield return WaitTicks(_intervalTicks * 2);

            Assert.That(recorder.Events.Count, Is.EqualTo(4), "One tick per interval, including the one on the last instant.");
            Assert.That(target.Health, Is.EqualTo(health - 8f).Within(0.001f));
            foreach (var resolved in recorder.Events)
            {
                Assert.That(resolved.Request.AttackerId, Is.EqualTo(source.Id));
                Assert.That(resolved.Request.TargetId, Is.EqualTo(target.Id));
                Assert.That(resolved.Request.Amount, Is.EqualTo(2f));
                Assert.That(resolved.Request.DamageType, Is.EqualTo(DamageType.Magical));
                Assert.That(resolved.Result.IsApplied, Is.True);
            }
            for (int i = 1; i < recorder.Events.Count; i++)
                Assert.That(recorder.Events[i].Request.SimulationTick - recorder.Events[i - 1].Request.SimulationTick,
                    Is.EqualTo(_intervalTicks), "Ticks are evenly spaced, without drift.");
            Assert.That(recorder.Events[0].Request.SimulationTick - appliedAt, Is.EqualTo(_intervalTicks).Within(1),
                "The first tick comes one interval after the application.");
            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False, "Damage and immobilization end together.");
        }

        [UnityTest]
        public IEnumerator ZeroDamage_StillImmobilizes_WithoutAnyTick()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();
            float health = target.Health;

            bool applied = false;
            yield return Apply(effect, source.Id, 0.5f, 0f, Interval, DamageType.Physical, true, r => applied = r);
            yield return WaitTicks(2);
            Assert.That(applied, Is.True);
            Assert.That((bool)movement.IsImmobilized, Is.True);
            yield return WaitUntil(() => !effect.IsActive);

            Assert.That(recorder.Events, Is.Empty);
            Assert.That(target.Health, Is.EqualTo(health));
            Assert.That((bool)movement.IsImmobilized, Is.False);
        }

        // ---- Purification and cancellation -------------------------------------------------

        [UnityTest]
        public IEnumerator Purify_OnAPurifiableEffect_EndsImmobilizeAndDamageAtOnce_WithoutRevertingDamageDealt()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();

            yield return Apply(effect, source.Id, 30f, 3f, Interval, DamageType.Physical, true);
            yield return WaitUntil(() => recorder.Events.Count >= 2);
            bool purified = false;
            float healthAtPurify = 0f;
            int ticksAtPurify = 0;
            yield return InSimulation(() =>
            {
                purified = effect.TryPurify();
                healthAtPurify = target.Health;
                ticksAtPurify = recorder.Events.Count;
            });
            yield return WaitTicks(_intervalTicks * 3);

            Assert.That(purified, Is.True);
            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That(recorder.Events.Count, Is.EqualTo(ticksAtPurify), "No tick after purification.");
            Assert.That(target.Health, Is.EqualTo(healthAtPurify), "Damage already dealt is not reverted, nothing more is dealt.");
            Assert.That(healthAtPurify, Is.LessThan(100f));
        }

        [UnityTest]
        public IEnumerator Purify_OnANonPurifiableEffect_IsIgnored_AndCancelStillEndsIt()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();

            yield return Apply(effect, source.Id, 30f, 1f, Interval, DamageType.Physical, false);
            yield return WaitUntil(() => recorder.Events.Count >= 1);
            bool purified = true;
            yield return InSimulation(() => purified = effect.TryPurify());
            int afterPurify = recorder.Events.Count;
            yield return WaitUntil(() => recorder.Events.Count > afterPurify);

            Assert.That(purified, Is.False);
            Assert.That(effect.IsActive, Is.True);
            Assert.That((bool)movement.IsImmobilized, Is.True);

            yield return InSimulation(() => effect.Cancel());
            int afterCancel = recorder.Events.Count;
            yield return WaitTicks(_intervalTicks * 3);

            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That(recorder.Events.Count, Is.EqualTo(afterCancel), "Cancel stops the damage as well.");
        }

        // ---- Target death and configuration ------------------------------------------------

        [UnityTest]
        public IEnumerator TargetDeath_EndsTheEffect_AndNoFurtherDamageIsDealt()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();

            yield return Apply(effect, source.Id, 30f, 1f, Interval, DamageType.Physical, true);
            yield return WaitUntil(() => recorder.Events.Count >= 1);
            // Death without the full enemy death flow (loot, FSM), which is not under test here.
            yield return InSimulation(() => SetNetworked(target, "Health", 0f, typeof(CharacterBase)));
            yield return WaitTicks(_intervalTicks * 3);
            int afterDeath = recorder.Events.Count;
            yield return WaitTicks(_intervalTicks * 3);

            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That(recorder.Events.Count, Is.EqualTo(afterDeath));
            Assert.That(target.Health, Is.Zero);

            bool applied = true;
            yield return Apply(effect, source.Id, 5f, 1f, Interval, DamageType.Physical, true, r => applied = r);
            Assert.That(applied, Is.False, "A dead creature cannot be immobilized.");
        }

        [UnityTest]
        public IEnumerator Apply_WithInvalidConfiguration_IsRejectedWithoutAnyState()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f));
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            // Rejections are logged as errors; only the returned state is under test.
            LogAssert.ignoreFailingMessages = true;

            var applied = new List<bool>();
            yield return Apply(effect, source.Id, 0f, 1f, Interval, DamageType.Physical, true, applied.Add);
            yield return Apply(effect, source.Id, 3f, -1f, Interval, DamageType.Physical, true, applied.Add);
            yield return Apply(effect, source.Id, 3f, 1f, 0f, DamageType.Physical, true, applied.Add);
            yield return Apply(effect, default, 3f, 1f, Interval, DamageType.Physical, true, applied.Add);
            yield return WaitTicks(2);

            Assert.That(applied, Is.EqualTo(new[] { false, false, false, false }));
            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False);
        }

        // ---- Reapplication -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Reapply_WhileActive_RefreshesDurationAndSource_WithoutDoubleTicks()
        {
            yield return Begin();
            var first = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var second = SpawnEnemy(new Vector3(0f, 80f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var firstRecorder = first.GetComponent<DamageRecorder>();
            var secondRecorder = second.GetComponent<DamageRecorder>();

            yield return Apply(effect, first.Id, Duration, 1f, Interval, DamageType.Physical, true);
            yield return WaitUntil(() => firstRecorder.Events.Count >= 2);
            int reappliedAt = 0;
            yield return Apply(effect, second.Id, Duration, 1f, Interval, DamageType.Physical, true, appliedAtTick: t => reappliedAt = t);
            int firstTicks = firstRecorder.Events.Count;
            yield return WaitUntil(() => !effect.IsActive);
            yield return WaitTicks(_intervalTicks * 2);

            Assert.That(firstRecorder.Events.Count, Is.EqualTo(firstTicks), "The previous source stops dealing damage.");
            Assert.That(secondRecorder.Events.Count, Is.EqualTo(4), "A fresh, full duration under the new source.");
            var all = new List<DamageResolvedEvent>(firstRecorder.Events);
            all.AddRange(secondRecorder.Events);
            all.Sort((a, b) => a.Request.SimulationTick.CompareTo(b.Request.SimulationTick));
            for (int i = 1; i < all.Count; i++)
                Assert.That(all[i].Request.SimulationTick, Is.GreaterThan(all[i - 1].Request.SimulationTick),
                    "Never two ticks on the same simulation tick.");
            Assert.That(secondRecorder.Events[0].Request.SimulationTick - reappliedAt, Is.EqualTo(_intervalTicks).Within(1),
                "Timers restart at the reapplication.");
            Assert.That((bool)movement.IsImmobilized, Is.False);
        }

        // ---- Host Migration hooks ---------------------------------------------------------

        [UnityTest]
        public IEnumerator RestoredSource_IsRemapped_AndOnlyTheRemappedSourceIsCreditedAfterwards()
        {
            yield return Begin();
            var stale = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var remapped = SpawnEnemy(new Vector3(0f, 80f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var staleRecorder = stale.GetComponent<DamageRecorder>();
            var remappedRecorder = remapped.GetComponent<DamageRecorder>();

            yield return Apply(effect, stale.Id, 30f, 1f, Interval, DamageType.Physical, true);
            int oldSource = 0;
            yield return InSimulation(() =>
            {
                oldSource = effect.GetRestoredSourceEntityIdValue();
                effect.SetRestoredSourceEntityId(remapped.Id);
            });
            yield return WaitUntil(() => remappedRecorder.Events.Count >= 2);

            Assert.That(oldSource, Is.EqualTo(stale.Id.Value));
            Assert.That(effect.SourceId, Is.EqualTo(remapped.Id));
            Assert.That(staleRecorder.Events, Is.Empty);
            Assert.That(effect.IsActive, Is.True, "Remapping keeps the running effect and its timers.");
        }

        [UnityTest]
        public IEnumerator CancelRestoredEffect_EndsImmobilizeAndDamage_WhenTheSourceCannotBeRemapped()
        {
            yield return Begin();
            var source = SpawnEnemy(new Vector3(0f, 60f, 0f), withRecorder: true);
            var target = SpawnEnemy(new Vector3(0f, 0f, 0f));
            var effect = target.GetComponent<ImmobilizeEffect>();
            var movement = target.GetComponent<EnemyMovementAIController>();
            var recorder = source.GetComponent<DamageRecorder>();

            yield return Apply(effect, source.Id, 30f, 1f, Interval, DamageType.Physical, true);
            yield return WaitUntil(() => recorder.Events.Count >= 1);
            yield return InSimulation(() => effect.CancelRestoredEffect());
            int afterCancel = recorder.Events.Count;
            yield return WaitTicks(_intervalTicks * 3);

            Assert.That(effect.IsActive, Is.False);
            Assert.That((bool)movement.IsImmobilized, Is.False);
            Assert.That(recorder.Events.Count, Is.EqualTo(afterCancel));
        }

        // ---- Fixture -------------------------------------------------------------------------

        private int _intervalTicks;

        private IEnumerator Begin()
        {
            var runnerObject = new GameObject("ImmobilizeEffectTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"immobilize-effect-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            _intervalTicks = TickTimer.CreateFromSeconds(_runner, Interval).RemainingTicks(_runner) ?? 0;
            Assert.That(_intervalTicks, Is.GreaterThan(1));
        }

        /// <summary>An inert creature: only the test (and the effect under test) acts on it.</summary>
        private EnemyCharacter SpawnEnemy(Vector3 position, bool withRecorder = false)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            if (withRecorder) enemy.gameObject.AddComponent<DamageRecorder>();
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        /// <summary>A creature whose movement controller runs and walks a far-away patrol waypoint (no pathfinding grid in tests).</summary>
        private EnemyCharacter SpawnPatrollingEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }

            var movement = enemy.GetComponent<EnemyMovementAIController>();
            SetField(movement, "_pathfindingNavigator", null);

            var routeObject = new GameObject("ImmobilizeTestRoute");
            var waypoint = new GameObject("ImmobilizeTestWaypoint");
            waypoint.transform.position = position + new Vector3(0f, -400f, 0f);
            _sceneObjects.Add(routeObject);
            _sceneObjects.Add(waypoint);
            var route = routeObject.AddComponent<EnemyPatrolRoute>();
            SetField(route, "_waypoints", new[] { waypoint.transform });
            movement.InitializePatrolRoute(route);
            movement.IsPatrolActive = true;
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private IEnumerator WaitTicks(int count)
        {
            int until = _runner.Tick.Raw + count;
            yield return WaitUntil(() => _runner.Tick.Raw >= until, 20f);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 12f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The expected state was not reached.");
        }

        private IEnumerator InSimulation(Action operation)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(operation);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private IEnumerator Apply(ImmobilizeEffect effect, EntityId source, float duration, float damage, float interval,
            DamageType type, bool purifiable, Action<bool> result = null, Action<int> appliedAtTick = null)
        {
            return InSimulation(() =>
            {
                appliedAtTick?.Invoke(_runner.Tick.Raw);
                bool applied = effect.TryApply(source, duration, damage, interval, type, purifiable);
                result?.Invoke(applied);
            });
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void SetNetworked(object target, string name, object value, Type declaringType) =>
            declaringType.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);

        private sealed class DamageRecorder : MonoBehaviour, IResolvedDamageFeedbackSink
        {
            public readonly List<DamageResolvedEvent> Events = new List<DamageResolvedEvent>();

            public void RecordResolvedDamage(in DamageResolvedEvent resolvedDamage)
            {
                if (resolvedDamage.Result.IsApplied) Events.Add(resolvedDamage);
            }
        }
    }
}
#endif

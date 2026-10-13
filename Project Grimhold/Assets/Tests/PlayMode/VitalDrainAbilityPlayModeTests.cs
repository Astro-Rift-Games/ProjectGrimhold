#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Abilities
{
    // Uses the existing single-runner ability fixture pattern. Runtime-only test composition does not edit prefabs.
    public sealed class VitalDrainAbilityPlayModeTests
    {
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private PlayerAbilityRuntimeNetworkController _avatar;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.Destroy(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator NoEnemyOrOnlyPlayer_RejectsWithoutManaSequenceOrCooldown()
        {
            yield return Begin();
            ExpectAvatarValidationError();
            _runner.Spawn(LoadPrefab("fea3a7b256f965a4eb9b965832939741"), Vector3.right);
            Physics2D.SyncTransforms();
            float mana = Mana();
            yield return InSimulation(() => Assert.That(_avatar.SandboxCast(UniversalAbilitySlot.Slot1),
                Is.EqualTo(AbilityActivationFailure.BehaviourRejected)));
            Assert.That(Mana(), Is.EqualTo(mana));
            _avatar.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Resolution_RebuildsTargetsAndHealsFromActualDamageOnlyOnce()
        {
            yield return Begin();
            var initial = SpawnEnemy(Vector3.right);
            var entering = SpawnEnemy(new Vector3(40f, 0f, 0f));
            var staying = SpawnEnemy(Vector3.left);
            float stayingHealth = staying.Health;
            float initialHealth = initial.Health;
            float enteringHealth = entering.Health;
            float beforeMana = Mana();
            float beforeHealth = 0f;
            yield return InSimulation(() =>
            {
                var caster = _avatar.GetComponent<PlayerCharacter>();
                caster.ApplyDamage(new DamageRequest(default, caster.Id, 20f, DamageType.TrueDamage,
                    Vector2.zero, Vector2.zero, _runner.Tick.Raw));
                beforeHealth = caster.Health;
                Assert.That(_avatar.SandboxCast(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            });
            Assert.That(initial.Health, Is.EqualTo(initialHealth), "No preparation damage.");
            yield return InSimulation(() =>
            {
                initial.GetComponent<NetworkTransform>().Teleport(new Vector3(40f, 0f, 0f));
                initial.transform.position = new Vector3(40f, 0f, 0f);
                entering.GetComponent<NetworkTransform>().Teleport(Vector3.up);
                entering.transform.position = Vector3.up;
                Physics2D.SyncTransforms();
            });
            yield return WaitUntil(() => !_avatar.HasActiveExecution);
            float damage = enteringHealth - entering.Health;
            float stayingDamage = stayingHealth - staying.Health;
            Assert.That(damage, Is.GreaterThan(0f));
            Assert.That(stayingDamage, Is.GreaterThan(0f));
            Assert.That(initial.Health, Is.EqualTo(initialHealth), "Departing target is rebuilt away.");
            Assert.That(_avatar.GetComponent<PlayerCharacter>().Health, Is.EqualTo(beforeHealth + (damage + stayingDamage) * 0.5f).Within(0.001f));
            _avatar.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot);
            Assert.That(beforeMana - Mana(), Is.EqualTo(slot.Definition.Cost));
            float resolvedHealth = entering.Health;
            float healedHealth = _avatar.GetComponent<PlayerCharacter>().Health;
            int until = _runner.Tick.Raw + 5;
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
            Assert.That(entering.Health, Is.EqualTo(resolvedHealth));
            Assert.That(_avatar.GetComponent<PlayerCharacter>().Health, Is.EqualTo(healedHealth));
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator LostOnlyTarget_CompletesWithoutDamageHealOrRefund()
        {
            yield return Begin();
            var enemy = SpawnEnemy(Vector3.right);
            float casterHealth = _avatar.GetComponent<PlayerCharacter>().Health;
            yield return InSimulation(() => Assert.That(_avatar.SandboxCast(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None)));
            float paidMana = Mana();
            yield return InSimulation(() => _runner.Despawn(enemy.Object));
            yield return WaitUntil(() => !_avatar.HasActiveExecution);
            Assert.That(_avatar.GetComponent<PlayerCharacter>().Health, Is.EqualTo(casterHealth));
            Assert.That(Mana(), Is.EqualTo(paidMana));
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator InterruptedPreparation_AppliesNothingAndRetainsPayment()
        {
            yield return Begin();
            var enemy = SpawnEnemy(Vector3.right);
            float enemyHealth = enemy.Health;
            float casterHealth = _avatar.GetComponent<PlayerCharacter>().Health;
            yield return InSimulation(() => Assert.That(_avatar.SandboxCast(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None)));
            float paidMana = Mana();
            yield return InSimulation(() => Assert.That(_avatar.TryInterrupt(UniversalAbilitySlot.Slot1, 1,
                AbilityExecutionStopReason.Knockback), Is.True));
            int until = _runner.Tick.Raw + Mathf.CeilToInt(2f / _runner.DeltaTime);
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
            Assert.That(enemy.Health, Is.EqualTo(enemyHealth));
            Assert.That(_avatar.GetComponent<PlayerCharacter>().Health, Is.EqualTo(casterHealth));
            Assert.That(Mana(), Is.EqualTo(paidMana));
            Assert.That(_avatar.HasActiveExecution, Is.False);
            Assert.That(_avatar.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        private IEnumerator Begin()
        {
            var owner = new GameObject("VitalDrainTestRunner");
            _runner = owner.AddComponent<NetworkRunner>();
            owner.AddComponent<EntityRegistry>();
            _driver = owner.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-drain-{Guid.NewGuid():N}",
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True);
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out var attributes), Is.True);
            RaidParticipantId.TryCreate(1, out var id);
            var participant = _runner.Spawn(LoadPrefab("c39d451563bae6e43934008a0dadc6d6"),
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "ability-drain", id, attributes, ExperienceCurve.InitialLevel, 0, "ability-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId("life_drain"), default)))
                .GetComponent<NetworkRaidParticipant>();
            ExpectAvatarValidationError();
            var avatar = _runner.Spawn(LoadPrefab("fea3a7b256f965a4eb9b965832939741"), Vector3.zero, Quaternion.identity,
                _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    var runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    SetField(runtime, "_playerCharacter", instance.GetComponent<PlayerCharacter>());
                    SetField(runtime, "_staminaController", instance.GetComponent<PlayerStaminaNetworkController>());
                    SetField(runtime, "_manaController", instance.GetComponent<PlayerManaNetworkController>());
                    var catalog = (AbilityDefinitionCatalog)typeof(PlayerAbilityRuntimeNetworkController)
                        .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                    Assert.That(catalog.TryGet(new AbilityId("life_drain"), out var definition), Is.True);
                    var behaviour = instance.GetComponent<VitalDrainAbilityBehaviour>();
                    Assert.That(behaviour, Is.Not.Null, "Production avatar must compose Vital Drain.");
                    Assert.That(behaviour.Definition, Is.SameAs(definition), "Production binding must use the canonical life_drain definition.");
                    var bindings = (AbilityExecutionBehaviour[])typeof(PlayerAbilityRuntimeNetworkController)
                        .GetField("_executionBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                    Assert.That(bindings, Does.Contain(behaviour), "Production runtime must bind Vital Drain before test isolation.");
                    SetField(behaviour, "_preparationSeconds", 1.5f);
                    SetField(runtime, "_executionBehaviours", new AbilityExecutionBehaviour[] { behaviour });
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            _avatar = avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
            yield return WaitUntil(() => _avatar.IsInitialized && _avatar.GetComponent<PlayerManaNetworkController>().IsInitialized);
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab("5deca87613df0fa409d98702aec643d4"), position, Quaternion.identity);
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private NetworkObject LoadPrefab(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

        private float Mana() => _avatar.GetComponent<PlayerManaNetworkController>().CurrentMana;

        private IEnumerator InSimulation(Action action)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestOperation(action);
            yield return WaitUntil(() => _driver.CompletionSequence != sequence);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Expected simulation state was not reached.");
        }

        private static void SetField(object target, string name, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void ExpectAvatarValidationError() => LogAssert.Expect(LogType.Error,
            "PlayerExtractionProgressController requires character, extraction controller, " +
            "registry, assignment service, and valid receiver/reader registrations.");

        [Test]
        public void MissingAreaFinder_ProducesInvalidPlanBeforeAnyEffect()
        {
            var owner = new GameObject("VitalDrainLifecycleTest");
            try
            {
                var behaviour = owner.AddComponent<VitalDrainAbilityBehaviour>();
                LogAssert.Expect(LogType.Error,
                    "VitalDrainAbilityBehaviour requires a configured AbilityAreaTargetFinder on the caster.");
                Assert.That(behaviour.TryPlanStart(default, out var plan), Is.True);
                Assert.That(plan.Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void BeginRebindAndStop_DoNotResolveOrRequireLiveTargets()
        {
            var owner = new GameObject("VitalDrainLifecycleTest");
            try
            {
                var behaviour = owner.AddComponent<VitalDrainAbilityBehaviour>();
                behaviour.Begin(default, default);
                behaviour.Rebind(default, default);
                behaviour.Stop(default, default, AbilityExecutionStopReason.Knockback);
                Assert.That(behaviour.Simulate(default, default).Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
                Assert.That(behaviour.CanInterrupt(AbilityExecutionStopReason.Knockback), Is.True);
                Assert.That(behaviour.CanInterrupt(AbilityExecutionStopReason.Stun), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
#endif

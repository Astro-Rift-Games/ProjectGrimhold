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

namespace Tests.PlayMode.Abilities
{
    /// <summary>Single-runner acceptance coverage using the real avatar and support effects.</summary>
    public sealed class SupportAbilitiesPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const float Preparation = 1.5f;
        private const float Duration = 3f;
        private const float Bonus = 0.3f;
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private SupportInputDriver _input;
        private PlayerAbilityRuntimeNetworkController _runtime;
        private CharacterAttributeState _attributes;
        private int _participantSequence;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Restoration_FullHealthSelfWithoutOtherAllies_RejectsWithoutManaCooldownOrSequence()
        {
            yield return Begin("restoration");
            Assert.That(Character.Health, Is.EqualTo(Character.MaxHealth));
            float mana = Mana();
            yield return Press();
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1),
                Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(Mana(), Is.EqualTo(mana));
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
            Assert.That(_runtime.HasActiveExecution, Is.False);
        }

        [UnityTest]
        public IEnumerator Restoration_InjuredSelf_PaysOnceAndHealsOnlyAfterPreparationWithoutOverheal()
        {
            yield return Begin("restoration");
            yield return InSimulation(() => SetHealth(Character, Character.MaxHealth - 5f));
            float health = Character.Health;
            float mana = Mana();
            yield return Press();
            AssertPaid(mana);
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            Assert.That(Character.Health, Is.EqualTo(health));
            yield return WaitSimulationSeconds(0.25f);
            Assert.That(Character.Health, Is.EqualTo(health), "No healing during preparation.");
            yield return WaitForCompletion();
            Assert.That(Character.Health, Is.EqualTo(Character.MaxHealth), "The configured 30 heal is capped.");
            float afterPayment = Mana();
            yield return WaitSimulationSeconds(0.25f);
            Assert.That(Character.Health, Is.EqualTo(Character.MaxHealth));
            Assert.That(Mana(), Is.EqualTo(afterPayment));
        }

        [UnityTest]
        public IEnumerator Restoration_FullHealthBeforeResolution_CompletesNormallyWithoutRefund()
        {
            yield return Begin("restoration");
            yield return InSimulation(() => SetHealth(Character, Character.MaxHealth - 5f));
            float mana = Mana();
            yield return Press();
            AssertPaid(mana);
            float afterPayment = Mana();
            yield return InSimulation(() => Character.ApplyHealing(new HealRequest(Character.MaxHealth)));
            Assert.That(_runtime.HasActiveExecution, Is.True, "The external heal occurs before resolution.");
            yield return WaitForCompletion();
            Assert.That(Character.Health, Is.EqualTo(Character.MaxHealth));
            Assert.That(Mana(), Is.EqualTo(afterPayment), "Payment is not refunded for an empty resolution.");
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
            Assert.That(_runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
        }

        [UnityTest]
        public IEnumerator Empower_MalformedRecipient_RejectsInvalidPlanBeforeManaCooldownOrSequence()
        {
            yield return Begin("empower");
            var effect = Character.GetComponent<EmpowerNetworkEffect>();
            SetField(effect, "_character", null);
            float mana = Mana();
            LogAssert.Expect(UnityEngine.LogType.Error,
                "EmpowerAbilityBehaviour configuration is invalid: every intended ally requires a ready same-root EmpowerNetworkEffect before payment.");
            yield return Press();
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1),
                Is.EqualTo(AbilityActivationFailure.InvalidPlan));
            Assert.That(Mana(), Is.EqualTo(mana));
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Sequence, Is.Zero);
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
            Assert.That(snapshot.Cooldown.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator SupportEffects_OutsideSimulationAndForeignOwner_RejectWithoutChangingDeadlines()
        {
            yield return Begin("empower");
            var empower = Character.GetComponent<EmpowerNetworkEffect>();
            var purification = Character.GetComponent<PurificationNetworkEffect>();
            Assert.That(_runner.IsSimulationUpdating, Is.False);
            Assert.That(empower.TryApply(Bonus, Duration), Is.False);
            Assert.That(purification.TryApply(Duration), Is.False);
            var other = SpawnAvatar("empower", new Vector3(20f, 0f, 0f), false).GetComponent<PlayerCharacter>();
            yield return InSimulation(() =>
            {
                Assert.That(empower.TryApply(Bonus, Duration), Is.True);
                Assert.That(purification.TryApply(Duration), Is.True);
                TickTimer originalEmpower = Read<TickTimer>(empower, "ExpiryTimer");
                TickTimer originalProtection = Read<TickTimer>(purification, "ProtectionTimer");
                SetField(empower, "_character", other);
                SetField(purification, "_character", other);
                Assert.That(empower.IsReadyToApply, Is.False);
                Assert.That(purification.IsReadyToApply, Is.False);
                Assert.That(empower.TryApply(Bonus * 2f, Duration * 2f), Is.False);
                Assert.That(purification.TryApply(Duration * 2f), Is.False);
                Assert.That(Read<TickTimer>(empower, "ExpiryTimer"), Is.EqualTo(originalEmpower));
                Assert.That(Read<TickTimer>(purification, "ProtectionTimer"), Is.EqualTo(originalProtection));
                SetField(empower, "_character", Character);
                SetField(purification, "_character", Character);
            });
        }

        [UnityTest]
        public IEnumerator Empower_SoloCast_RefreshesDurationWithoutStackingAndExpiresToUnitMultiplier()
        {
            yield return Begin("empower");
            var effect = Character.GetComponent<EmpowerNetworkEffect>();
            Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f));
            float mana = Mana();
            yield return Press();
            AssertPaid(mana);
            yield return WaitForCompletion();
            Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f + Bonus).Within(0.001f));
            TickTimer original = Read<TickTimer>(effect, "ExpiryTimer");
            yield return WaitSimulationSeconds(1f);
            yield return InSimulation(() => Assert.That(effect.TryApply(Bonus, Duration), Is.True));
            TickTimer refreshed = Read<TickTimer>(effect, "ExpiryTimer");
            Assert.That(refreshed, Is.Not.EqualTo(original));
            Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f + Bonus).Within(0.001f));
            yield return WaitUntil(() => original.Expired(_runner));
            Assert.That(effect.IsActive, Is.True, "Refresh outlives the original deadline.");
            yield return WaitUntil(() => refreshed.Expired(_runner));
            Assert.That(effect.IsActive, Is.False, "Refresh replaces rather than adds duration.");
            Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Purification_PreventiveSoloCast_ProtectsAfterCastRefreshesAndExpires()
        {
            yield return Begin("purification");
            var effect = Character.GetComponent<PurificationNetworkEffect>();
            Assert.That(effect.HasPurifiableState, Is.False);
            Assert.That(effect.IsProtectedFromPurifiableStates, Is.False);
            float mana = Mana();
            yield return Press();
            AssertPaid(mana);
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            Assert.That(effect.IsProtectedFromPurifiableStates, Is.False, "Protection starts at resolution, not acceptance.");
            yield return WaitForCompletion();
            Assert.That(effect.IsProtectedFromPurifiableStates, Is.True);
            TickTimer original = Read<TickTimer>(effect, "ProtectionTimer");
            yield return WaitSimulationSeconds(1f);
            yield return InSimulation(() => Assert.That(effect.TryApply(Duration), Is.True));
            TickTimer refreshed = Read<TickTimer>(effect, "ProtectionTimer");
            Assert.That(refreshed, Is.Not.EqualTo(original));
            yield return WaitUntil(() => original.Expired(_runner));
            Assert.That(effect.IsProtectedFromPurifiableStates, Is.True);
            yield return WaitUntil(() => refreshed.Expired(_runner));
            Assert.That(effect.IsProtectedFromPurifiableStates, Is.False, "No stacked duration survives the refreshed deadline.");
        }

        [UnityTest]
        public IEnumerator SupportEffects_BehaviourCopyStateFromAndSpawnedReplay_PreserveOriginalDeadlines()
        {
            yield return Begin("empower");
            var empower = Character.GetComponent<EmpowerNetworkEffect>();
            var purification = Character.GetComponent<PurificationNetworkEffect>();
            yield return InSimulation(() =>
            {
                Assert.That(empower.TryApply(Bonus, Duration), Is.True);
                Assert.That(purification.TryApply(Duration), Is.True);
            });
            TickTimer empowerDeadline = Read<TickTimer>(empower, "ExpiryTimer");
            TickTimer protectionDeadline = Read<TickTimer>(purification, "ProtectionTimer");
            yield return WaitSimulationSeconds(1f);
            var target = SpawnAvatar("empower", new Vector3(20f, 0f, 0f), false);
            var copiedEmpower = target.GetComponent<EmpowerNetworkEffect>();
            var copiedPurification = target.GetComponent<PurificationNetworkEffect>();
            yield return InSimulation(() =>
            {
                // Real Fusion behaviour storage copy, not a multiplayer/Host Migration resume claim.
                copiedEmpower.CopyStateFrom(empower);
                copiedPurification.CopyStateFrom(purification);
                copiedEmpower.Spawned();
                copiedPurification.Spawned();
                copiedEmpower.Spawned();
                copiedPurification.Spawned();
            });
            Assert.That(Read<TickTimer>(copiedEmpower, "ExpiryTimer"), Is.EqualTo(empowerDeadline));
            Assert.That(Read<TickTimer>(copiedPurification, "ProtectionTimer"), Is.EqualTo(protectionDeadline));
            Assert.That(copiedEmpower.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f + Bonus).Within(0.001f));
            Assert.That(copiedPurification.IsProtectedFromPurifiableStates, Is.True);
            yield return WaitUntil(() => empowerDeadline.Expired(_runner) && protectionDeadline.Expired(_runner));
            Assert.That(empower.IsActive || copiedEmpower.IsActive, Is.False);
            Assert.That(purification.IsProtectedFromPurifiableStates || copiedPurification.IsProtectedFromPurifiableStates, Is.False);
        }

        [UnityTest]
        public IEnumerator Empower_CombatControllerMelee_ScalesWeaponDamageButNotDirectAbilityImpact()
        {
            yield return Begin("empower");
            var target = SpawnAvatar("empower", Vector3.right * 10f, false).GetComponent<PlayerCharacter>();
            var combat = Character.GetComponent<PlayerCombatNetworkController>();
            var recorder = new DamageProbe { Target = target, Inner = Character.GetComponent<DamageResolver>() };
            var melee = Character.GetComponent<MeleeAttack>();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<MeleeAttackConfig>(
                "Assets/Scriptable Objects/PlayerMeleeAttackConfig.asset");
            Assert.That(config, Is.Not.Null);
            // Deterministic target selection mirrors the combat fixture; damage still uses the real resolver.
            melee.Initialize(config, new AttackExecutionParameters(5f, DamageType.TrueDamage, 0.5f, 2f, 0f), recorder, recorder);
            yield return InSimulation(() => Assert.That(combat.TrySetActiveAttack(melee), Is.True));
            float initial = target.Health;
            _input.Buttons.Set(PlayerInputButton.PrimaryAttack, true);
            yield return WaitUntil(() => recorder.Count == 1);
            _input.Buttons = default;
            Assert.That(recorder.Last.Amount, Is.EqualTo(5f));
            Assert.That(target.Health, Is.EqualTo(initial - 5f).Within(0.001f));
            yield return WaitSimulationSeconds(0.6f);
            yield return Press();
            yield return WaitForCompletion();
            float beforeBoosted = target.Health;
            _input.Buttons.Set(PlayerInputButton.PrimaryAttack, true);
            yield return WaitUntil(() => recorder.Count == 2);
            _input.Buttons = default;
            Assert.That(recorder.Last.Amount, Is.EqualTo(5f * (1f + Bonus)).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(beforeBoosted - 5f * (1f + Bonus)).Within(0.001f));
            float beforeAbility = target.Health;
            yield return InSimulation(() =>
            {
                Assert.That(Character.GetComponent<EmpowerNetworkEffect>().IsActive, Is.True);
                Assert.That(AbilityImpact.TryApply(recorder, Character.Id, target.Id, target, 5f,
                    DamageType.TrueDamage, 0f, Vector2.right, target.transform.position, _runner.Tick.Raw, Character), Is.True);
            });
            Assert.That(recorder.Count, Is.EqualTo(3));
            Assert.That(recorder.Last.Amount, Is.EqualTo(5f), "The same configured ability damage is not multiplied.");
            Assert.That(target.Health, Is.EqualTo(beforeAbility - 5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Empower_RangedTryAcceptRelease_CapturesBoostedDamageAcrossEffectExpiry()
        {
            yield return Begin("empower");
            var ranged = Character.GetComponent<RangedAttack>();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<RangedAttackConfig>(
                "Assets/Scriptable Objects/RangePlayerAttackConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(ranged.TryConfigure(config,
                new AttackExecutionParameters(5f, DamageType.Physical, 0.5f, 6f, 0f, 4f)), Is.True);
            var effect = Character.GetComponent<EmpowerNetworkEffect>();
            RangedAttackRelease captured = default;
            yield return InSimulation(() =>
            {
                Assert.That(effect.TryApply(Bonus, 0.5f), Is.True);
                var request = new AttackRequest(Character.Id, Character.transform.position, Vector2.right, _runner.Tick.Raw);
                Assert.That(ranged.TryAcceptRelease(request, _runner.DeltaTime, out captured,
                    damageMultiplier: effect.GetBasicAttackDamageMultiplier()), Is.True);
            });
            Assert.That(captured.Damage, Is.EqualTo(5f * (1f + Bonus)).Within(0.001f));
            yield return WaitUntil(() => !effect.IsActive);
            Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f));
            Assert.That(captured.Damage, Is.EqualTo(5f * (1f + Bonus)).Within(0.001f), "Accepted payload remains boosted.");
            yield return InSimulation(() =>
            {
                var request = new AttackRequest(Character.Id, Character.transform.position, Vector2.right, _runner.Tick.Raw);
                Assert.That(ranged.TryAcceptRelease(request, _runner.DeltaTime, out var unboosted,
                    damageMultiplier: effect.GetBasicAttackDamageMultiplier()), Is.True);
                Assert.That(unboosted.Damage, Is.EqualTo(5f));
            });
        }

        [UnityTest]
        public IEnumerator Restoration_InitialTeammatesEnteringAndLeavingPreparation_UsesResolutionArea()
        {
            yield return Begin("restoration", initialTeam: true);
            var leaving = SpawnAvatar("restoration", Vector3.right, false).GetComponent<PlayerCharacter>();
            var entering = SpawnAvatar("restoration", Vector3.right * 20f, false).GetComponent<PlayerCharacter>();
            Assert.That(AbilityTargetPredicate.IsValidAlly(Character, leaving), Is.True);
            Assert.That(AbilityTargetPredicate.IsValidAlly(Character, entering), Is.True);
            // Use real player hitboxes and the real finder; no injected affiliation predicate.
            SetField(Character.GetComponent<AbilityAreaTargetFinder>(), "_targetLayerMask",
                new LayerMask { value = LayerMask.GetMask("Character") });
            yield return InSimulation(() =>
            {
                SetHealth(leaving, leaving.MaxHealth - 5f);
                SetHealth(entering, entering.MaxHealth - 5f);
                Physics2D.SyncTransforms();
            });
            float leavingHealth = leaving.Health;
            float mana = Mana();
            yield return Press();
            AssertPaid(mana);
            Assert.That(_runtime.HasActiveExecution, Is.True);
            yield return InSimulation(() =>
            {
                leaving.GetComponent<NetworkTransform>().Teleport(Vector3.right * 20f);
                entering.GetComponent<NetworkTransform>().Teleport(Vector3.right);
                leaving.transform.position = Vector3.right * 20f;
                entering.transform.position = Vector3.right;
                Physics2D.SyncTransforms();
            });
            yield return WaitForCompletion();
            Assert.That(leaving.Health, Is.EqualTo(leavingHealth), "The initial target left before resolution.");
            Assert.That(entering.Health, Is.EqualTo(entering.MaxHealth), "The initially distant teammate entered before resolution.");
            Assert.That(Character.Health, Is.EqualTo(Character.MaxHealth));
        }

        private sealed class DamageProbe : IAttackTargetQuery, IDamageResolver
        {
            public PlayerCharacter Target;
            public IDamageResolver Inner;
            public int Count;
            public DamageRequest Last;
            public System.Collections.Generic.IReadOnlyList<AttackTarget> FindTargets(in AttackTargetQuery query) =>
                new[] { new AttackTarget(Target.Id, Target.transform.position) };
            public DamageResult Resolve(in DamageRequest request)
            {
                Count++;
                Last = request;
                return Inner.Resolve(request);
            }
        }

        // Same Single runner, participant/avatar spawn and simulation-operation driver as the sibling fixtures.
        private IEnumerator Begin(string abilityId, bool initialTeam = false)
        {
            _participantSequence = 0;
            var runnerObject = new GameObject("SupportAbilitiesTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            _input = runnerObject.AddComponent<SupportInputDriver>();
            _runner.AddCallbacks(_input);
            _runner.ProvideInput = true;
            if (initialTeam)
            {
                Assert.That(RaidCode.TryParse("123456", out var code), Is.True);
                Assert.That(RaidTeamId.TryCreate(1, out var team), Is.True);
                var host = new ProfileId("support-player-1");
                var roster = new[]
                {
                    new RaidLaunchParticipant(host, team),
                    new RaidLaunchParticipant(new ProfileId("support-player-2"), team),
                    new RaidLaunchParticipant(new ProfileId("support-player-3"), team)
                };
                Assert.That(RaidLaunchContext.TryCreate(code, host, roster, host, 1, out var launch), Is.True);
                Assert.That(runnerObject.AddComponent<NetworkSpawnManager>().InitializeForRunner(_runner,
                    default, default, Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);
            }
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"ability-support-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out _attributes), Is.True);
            _runtime = SpawnAvatar(abilityId, Vector3.zero, true);
            yield return WaitUntil(() => _runtime.IsInitialized && _runtime.GetComponent<PlayerManaNetworkController>().IsInitialized);
        }

        private PlayerAbilityRuntimeNetworkController SpawnAvatar(string abilityId, Vector3 position, bool inputAuthority)
        {
            Assert.That(RaidParticipantId.TryCreate(++_participantSequence, out var id), Is.True);
            var participant = _runner.Spawn(LoadPrefab(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                instance.GetComponent<NetworkRaidParticipant>().Initialize($"support-player-{id.Value}", id, _attributes,
                    ExperienceCurve.InitialLevel, 0, "support-test",
                    preparedAbilities: new PreparedAbilityLoadout(new AbilityId(abilityId), default)))
                .GetComponent<NetworkRaidParticipant>();
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
            var avatar = _runner.Spawn(LoadPrefab(PlayerGuid), position, Quaternion.identity,
                inputAuthority ? _runner.LocalPlayer : PlayerRef.None, onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                    var runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    AbilityExecutionBehaviour behaviour;
                    if (abilityId == "restoration")
                    {
                        behaviour = instance.GetComponent<RestorationAbilityBehaviour>();
                        SetField(behaviour, "_preparationSeconds", Preparation);
                        SetField(behaviour, "_healAmount", 30f);
                    }
                    else if (abilityId == "empower")
                    {
                        behaviour = instance.GetComponent<EmpowerAbilityBehaviour>();
                        SetField(behaviour, "_damageBonusFraction", Bonus);
                        SetField(behaviour, "_durationSeconds", Duration);
                    }
                    else
                    {
                        Assert.That(abilityId, Is.EqualTo("purification"));
                        behaviour = instance.GetComponent<PurificationAbilityBehaviour>();
                        SetField(behaviour, "_castSeconds", Preparation);
                        SetField(behaviour, "_protectionSeconds", Duration);
                    }
                    Assert.That(behaviour, Is.Not.Null, "The prefab must compose the support behaviour.");
                    SetField(behaviour, "_radius", 3f);
                    SetField(runtime, "_executionBehaviours", new[] { behaviour });
                });
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            return avatar.GetComponent<PlayerAbilityRuntimeNetworkController>();
        }

        private void AssertPaid(float before)
        {
            Assert.That(_runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(_runtime.TryGetSlot(UniversalAbilitySlot.Slot1, out var slot), Is.True);
            Assert.That(Mana(), Is.EqualTo(before - slot.Definition.Cost).Within(0.001f));
            Assert.That(_runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
            Assert.That(_runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
        }

        private PlayerCharacter Character => _runtime.GetComponent<PlayerCharacter>();
        private float Mana() => _runtime.GetComponent<PlayerManaNetworkController>().CurrentMana;
        private NetworkObject LoadPrefab(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

        private IEnumerator Press()
        {
            _input.Buttons = default;
            yield return WaitSimulationSeconds(_runner.DeltaTime * 3f);
            _input.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitSimulationSeconds(_runner.DeltaTime * 3f);
            _input.Buttons = default;
        }

        private IEnumerator WaitForCompletion() => WaitUntil(() => !_runtime.HasActiveExecution);
        private IEnumerator WaitSimulationSeconds(float seconds)
        {
            int until = _runner.Tick.Raw + Mathf.CeilToInt(seconds / _runner.DeltaTime);
            yield return WaitUntil(() => _runner.Tick.Raw >= until, Mathf.Max(8f, seconds * 3f));
        }

        private IEnumerator InSimulation(Action action)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(action);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeout = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Expected authoritative support state was not reached.");
        }

        private static void SetHealth(PlayerCharacter character, float health) =>
            typeof(CharacterBase).GetProperty("Health", Members).SetValue(character, health);

        private static T Read<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name, Members);
            Assert.That(property, Is.Not.Null, $"Missing observable state {name}.");
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            Assert.That(target, Is.Not.Null);
            var field = target.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null, $"Missing authored configuration {name}.");
            field.SetValue(target, value);
        }

        private sealed class SupportInputDriver : NetworkRunnerCallbacksAdapter
        {
            public NetworkButtons Buttons;
            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput { Buttons = Buttons, AimWorldPosition = Vector2.right * 10f });
        }
    }
}
#endif

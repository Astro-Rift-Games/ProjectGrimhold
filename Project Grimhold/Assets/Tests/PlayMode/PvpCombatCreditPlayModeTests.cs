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

namespace Tests.PlayMode.Combat
{
    /// <summary>Single-authority integration coverage; state copies are not real Host Migration proof.</summary>
    public sealed class PvpCombatCreditPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string SlimeGuid = "5deca87613df0fa409d98702aec643d4";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private readonly PlayerCharacter[] _players = new PlayerCharacter[4];
        private readonly NetworkRaidParticipant[] _participants = new NetworkRaidParticipant[4];

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.DestroyImmediate(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator T02_01_DowningHitThenDepletion_ConfirmsExactlyOneKill()
        {
            yield return Begin();
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.EqualTo(Id(0).Value));
            AssertAll();
            yield return Hit(0, 1, 10000f);
            Assert.That((bool)Credit(1).IsConfirmed, Is.True);
            Assert.That(Credit(1).PendingGrantMask, Is.Zero);
            yield return InSimulation(() => Credit(1).NotifyDownedCycleDefeated(Credit(1).CreditCycle));
            yield return WaitTicks(90);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T02_01_Drain_ConfirmsOriginalLastHit()
        {
            yield return Begin();
            yield return Hit(0, 1, 10000f);
            SetField(Downed(1), "_downedDrainPerSecond", 100000f);
            yield return WaitFor(() => ! _players[1].IsAlive);
            Assert.That(Downed(1).DefeatCauserEntityId, Is.Zero);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T02_02_T02_06_TeammateFinisher_CannotStealKillOrOpenAssist()
        {
            yield return Begin();
            yield return Hit(0, 1, 10000f);
            yield return Hit(2, 1, 1f);
            yield return Hit(2, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.EqualTo(Id(0).Value));
            Assert.That(Credit(1).AssistMask, Is.Zero);
            var contributors = new HashSet<RaidParticipantId>();
            _players[1].GetComponent<CombatContributionTracker>().GetValidContributors(_runner.Tick, contributors);
            Assert.That(contributors, Is.Empty, "Neither entry nor reserve depletion opens new contributions.");
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T02_03_T03_06_Revive_CancelsAllProvisionalCredits()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).AssistMask, Is.EqualTo(Bit(2)));
            yield return InSimulation(() => Assert.That(_players[1].TryRestoreFromDowned(1f), Is.True));
            Assert.That(Credit(1).LastHitParticipantValue, Is.Zero);
            Assert.That(Credit(1).AssistMask, Is.Zero);
            Assert.That((bool)Credit(1).IsConfirmed, Is.False);
            Assert.That(Credit(1).PendingGrantMask, Is.Zero);
            yield return InSimulation(() => Credit(1).NotifyDownedCycleDefeated(0));
            yield return WaitTicks(90);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator T02_04_AfterRevive_NewDownerOwnsIndependentRegister()
        {
            yield return Begin();
            // A's only hit is the entry hit, so it never becomes an assist contribution.
            yield return Hit(0, 1, 10000f);
            int cycle = Credit(1).CreditCycle;
            yield return InSimulation(() => Assert.That(_players[1].TryRestoreFromDowned(1f), Is.True));
            yield return Hit(2, 1, 10000f);
            Assert.That(Credit(1).CreditCycle, Is.EqualTo(cycle + 1));
            Assert.That(Credit(1).AssistMask, Is.Zero);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 2);
        }

        [UnityTest]
        public IEnumerator ActiveContributionInsideWindow_RemainsEligibleAfterReviveInNewCycle()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return InSimulation(() => Assert.That(_players[1].TryRestoreFromDowned(1f), Is.True));
            AssertAll();
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).AssistMask, Is.EqualTo(Bit(2)));
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator T02_05_T02_07_OrderedLethalHitsInSameTick_FirstHitWins()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Assert.That(Resolve(0, 1, 10000f).IsFatal, Is.False);
                Assert.That(Resolve(2, 1, 10000f).IsFatal, Is.True);
            });
            Assert.That(Credit(1).LastHitParticipantValue, Is.EqualTo(Id(0).Value));
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T03_01_DamageWithinWindow_GrantsHalfBaseAssist()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator T03_01_DamageOutsideWindow_DoesNotGrantAssist()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return WaitTicks(_players[1].GetComponent<CombatContributionTracker>().AssistWindowTicks + 1);
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T03_03_RestorationAtFullHealth_DoesNotRecordSupport()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                HealResult result = RestorationAbilityBehaviour.ApplyRestoration(_players[2], _players[0], 30f, _runner.Tick);
                Assert.That(result.AmountHealed, Is.Zero);
                Assert.That(Credit(0).TryGetLatestSupportReceived(Id(2), out _, out _), Is.False);
            });
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator T03_04_EffectiveRestorationOnLastHitter_GrantsAssist()
        {
            yield return Begin();
            yield return Hit(1, 0, 10f);
            yield return InSimulation(() =>
            {
                HealResult result = RestorationAbilityBehaviour.ApplyRestoration(_players[2], _players[0], 30f, _runner.Tick);
                Assert.That(result.Success, Is.True);
                Assert.That(result.AmountHealed, Is.EqualTo(10f));
                Assert.That(Credit(0).TryGetLatestSupportReceived(Id(2), out int tick, out var kind), Is.True);
                Assert.That(tick, Is.EqualTo(_runner.Tick.Raw));
                Assert.That(kind, Is.EqualTo(CombatContributionKind.Healing));
            });
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator SupportOnDamageContributor_IsIncludedButSelfHealingIsNot()
        {
            yield return Begin();
            SetAssistLimit(0);
            yield return Hit(1, 2, 10f);
            yield return InSimulation(() =>
            {
                RestorationAbilityBehaviour.ApplyRestoration(_players[3], _players[2], 5f, _runner.Tick);
                RestorationAbilityBehaviour.ApplyRestoration(_players[2], _players[2], 5f, _runner.Tick);
            });
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2) | Bit(3));
        }

        [UnityTest]
        public IEnumerator T03_05_UnlimitedOption_AllTeammatesReceiveAssist()
        {
            yield return Begin();
            SetAssistLimit(0);
            yield return Hit(2, 1, 1f);
            yield return Hit(3, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2) | Bit(3));
        }

        [UnityTest]
        public IEnumerator DefaultLimit_OnlyMostRecentEligibleContributorReceivesAssist()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return WaitTicks(2);
            yield return Hit(3, 1, 1f);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).AssistMask, Is.EqualTo(Bit(3)));
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(3));
        }

        [UnityTest]
        public IEnumerator DefaultLimit_SameTickContributorsTieBreakToLowestParticipantId()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Assert.That(Resolve(3, 1, 1f).IsApplied, Is.True);
                Assert.That(Resolve(2, 1, 1f).IsApplied, Is.True);
            });
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            int lowest = Id(2).Value < Id(3).Value ? Bit(2) : Bit(3);
            AssertAll(killer: 0, assistMask: lowest);
        }

        [Test]
        public void SelectAssistMask_ZeroIsUnlimitedAndLimitKeepsLatestThenLowestId()
        {
            var ticks = new int[RaidSessionRules.MaxParticipants];
            ticks[1] = 50;
            ticks[2] = 70;
            ticks[4] = 70;
            const int eligible = (1 << 1) | (1 << 2) | (1 << 4);
            Assert.That(PlayerPvpCombatCreditNetworkController.SelectAssistMask(eligible, ticks, 0), Is.EqualTo(eligible));
            Assert.That(PlayerPvpCombatCreditNetworkController.SelectAssistMask(eligible, ticks, 1), Is.EqualTo(1 << 2));
            Assert.That(PlayerPvpCombatCreditNetworkController.SelectAssistMask(eligible, ticks, 2), Is.EqualTo((1 << 2) | (1 << 4)));
            Assert.That(PlayerPvpCombatCreditNetworkController.SelectAssistMask(eligible, ticks, 5), Is.EqualTo(eligible));
            Assert.That(PlayerPvpCombatCreditNetworkController.SelectAssistMask(0, ticks, 1), Is.Zero);
        }

        [UnityTest]
        public IEnumerator T03_07_ContributorDownedLater_KeepsAssist()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(1, 2, 10000f);
            Assert.That(_players[2].IsDowned, Is.True);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator EmpowerSource_EffectivePvpDamageAddsSupporter_NotSelf()
        {
            yield return Begin();
            yield return InSimulation(() =>
            {
                Resolve(0, 1, 1f, Id(2));
                var tracker = _players[1].GetComponent<CombatContributionTracker>();
                Assert.That(tracker.TryGetLatestContribution(Id(0), out int tick, out var damageKind), Is.True);
                Assert.That(tick, Is.EqualTo(_runner.Tick.Raw));
                Assert.That(damageKind, Is.EqualTo(CombatContributionKind.Damage));
                Assert.That(tracker.TryGetLatestContribution(Id(2), out tick, out var supportKind), Is.True);
                Assert.That(tick, Is.EqualTo(_runner.Tick.Raw));
                Assert.That(supportKind, Is.EqualTo(CombatContributionKind.Empower));
            });
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator EmpowerEffect_SourceSurvivesStateCopyAndCasterDefeat_ExpiresWithBuff()
        {
            yield return Begin();
            var effect = _players[0].GetComponent<EmpowerNetworkEffect>();
            var restored = _players[3].GetComponent<EmpowerNetworkEffect>();
            yield return InSimulation(() =>
            {
                Assert.That(effect.TryApply(0.3f, 0.5f, Id(2)), Is.True);
                Assert.That(effect.GetBasicAttackDamageMultiplier(), Is.EqualTo(1.3f).Within(0.00001f));
                restored.CopyStateFrom(effect);
                Assert.That(_participants[2].TryMarkDefeated(_players[2].Object), Is.True);
                Assert.That(restored.TryGetBasicAttackEmpowerSource(out var source), Is.True);
                Assert.That(source, Is.EqualTo(Id(2)));
            });
            yield return WaitFor(() => !restored.IsActive);
            Assert.That(restored.TryGetBasicAttackEmpowerSource(out _), Is.False);
            Assert.That(restored.GetBasicAttackDamageMultiplier(), Is.EqualTo(1f));
            AssertAll();
        }

        [UnityTest]
        public IEnumerator EmpoweredProjectile_CarriesSourceToEffectivePvpImpact()
        {
            yield return Begin();
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<RangedAttackConfig>(
                "Assets/Scriptable Objects/RangePlayerAttackConfig.asset");
            Assert.That(config, Is.Not.Null);
            float before = _players[1].Health;
            yield return InSimulation(() =>
            {
                var spawner = _players[0].GetComponent<FusionProjectileSpawner>();
                Assert.That(spawner.TryConfigure(config), Is.True);
                var shot = new ProjectileSpawnRequest(_players[0].Id,
                    (Vector2)_players[1].transform.position + Vector2.left * 2f, Vector2.right,
                    1f, DamageType.TrueDamage, 20f, 2f, 5f, _runner.Tick,
                    projectilePrefab: config.ProjectilePrefab, impactLayerMask: config.ImpactLayerMask.value);
                Assert.That(spawner.Spawn(shot, Id(2)).WasSpawned, Is.True);
            });
            yield return WaitFor(() => _players[1].Health < before);
            Assert.That(_players[1].GetComponent<CombatContributionTracker>()
                .TryGetLatestContribution(Id(2), out _, out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(CombatContributionKind.Empower));
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator EmpowerSource_OnCreatureDoesNotChangePveAssist()
        {
            yield return Begin();
            NetworkObject slime = _runner.Spawn(Load(SlimeGuid), Vector3.up * 50f, Quaternion.identity);
            yield return InSimulation(() =>
            {
                var enemy = slime.GetComponent<EnemyCharacter>();
                var resolver = slime.GetComponent<DamageResolver>();
                resolver.Resolve(new DamageRequest(_players[0].Id, enemy.Id, 1f, DamageType.TrueDamage,
                    Vector2.right, enemy.transform.position, _runner.Tick, empowerSourceId: Id(2)));
                resolver.Resolve(new DamageRequest(_players[3].Id, enemy.Id, 10000f, DamageType.TrueDamage,
                    Vector2.right, enemy.transform.position, _runner.Tick));
            });
            Assert.That(_participants[0].ExperienceLedger.AssistExperience, Is.EqualTo(5));
            Assert.That(_participants[0].ExperienceLedger.PveAssistCount, Is.EqualTo(1));
            Assert.That(_participants[3].ExperienceLedger.KillExperience, Is.EqualTo(10));
            Assert.That(_participants[3].ExperienceLedger.PveKillCount, Is.EqualTo(1));
            AssertLedger(1, 0, 0);
            AssertLedger(2, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(_participants[i].ExperienceLedger.PvpKillCount, Is.Zero);
                Assert.That(_participants[i].ExperienceLedger.PvpAssistCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator DefeatedOriginalSource_StillOwnsLastHitButGetsNoNewXp()
        {
            yield return Begin();
            yield return InSimulation(() => Assert.That(_participants[0].TryMarkDefeated(_players[0].Object), Is.True));
            yield return Hit(0, 1, 10000f); // A previously created effect can resolve after its creator's Defeat.
            Assert.That(Credit(1).LastHitParticipantValue, Is.EqualTo(Id(0).Value));
            yield return Hit(2, 1, 10000f);
            Assert.That(Credit(1).PendingGrantMask, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator DefeatedDamageSourceAndItsHealer_KeepRelationshipsButOnlyRaiderGetsXp()
        {
            yield return Begin();
            SetAssistLimit(0);
            yield return Hit(1, 2, 10f);
            yield return InSimulation(() => RestorationAbilityBehaviour.ApplyRestoration(_players[3], _players[2], 30f, _runner.Tick));
            yield return InSimulation(() => Assert.That(_participants[2].TryMarkDefeated(_players[2].Object), Is.True));
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).AssistMask, Is.EqualTo(Bit(2) | Bit(3)));
            yield return Hit(0, 1, 10000f);
            AssertAll(killer: 0, assistMask: Bit(3));
        }

        [UnityTest]
        public IEnumerator DownedOriginalSource_StillOwnsLastHit()
        {
            yield return Begin();
            yield return Hit(1, 0, 10000f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(2, 1, 10000f);
            Assert.That(_players[0].IsDowned, Is.True);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator CreatureDownsTarget_NoPlayerCredit()
        {
            yield return Begin();
            var creature = _runner.Spawn(Load(SlimeGuid), Vector3.up * 50f, Quaternion.identity).GetComponent<EnemyCharacter>();
            yield return Hit(2, 1, 1f);
            yield return InSimulation(() => _players[1].GetComponent<DamageResolver>().Resolve(new DamageRequest(
                creature.Id, _players[1].Id, 10000f, DamageType.TrueDamage, Vector2.right,
                _players[1].transform.position, _runner.Tick)));
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator UnresolvedOriginalSource_CannotReceiveLastHit()
        {
            yield return Begin();
            yield return InSimulation(() =>
                _players[0].GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(default));
            yield return Hit(0, 1, 10000f);
            yield return Hit(2, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator UnresolvedAffiliation_FailsClosedForLastHit()
        {
            yield return Begin();
            // The target link resolves, but its admitted identity is absent from the frozen cohort.
            RaidParticipantId.TryCreate(16, out var unknown);
            yield return InSimulation(() => typeof(NetworkRaidParticipant).GetProperty("RaidParticipantId", Members)
                .SetValue(_participants[1], unknown));
            yield return Hit(0, 1, 10000f);
            yield return Hit(2, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator SelfEmpowerAndExpiredHealing_DoNotCreateAssists()
        {
            yield return Begin();
            yield return Hit(1, 0, 10f);
            yield return InSimulation(() => RestorationAbilityBehaviour.ApplyRestoration(_players[2], _players[0], 30f, _runner.Tick));
            yield return WaitTicks(_players[1].GetComponent<CombatContributionTracker>().AssistWindowTicks + 1);
            yield return InSimulation(() => Resolve(0, 1, 1f, Id(0)));
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).AssistMask, Is.Zero);
            AssertAll(killer: 0);
        }

        [UnityTest]
        public IEnumerator EnvironmentDownsTarget_NoLastHitOrAssist()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(-1, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).LastHitParticipantValue, Is.Zero);
            Assert.That(Credit(1).AssistMask, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator TerminalLastHitter_DropsPendingBitWithoutExperienceOrRetryGrowth()
        {
            yield return Begin();
            yield return Hit(0, 1, 10000f);
            yield return InSimulation(() => Assert.That(_participants[0].TryAbortForClosure(), Is.True));
            yield return Hit(2, 1, 10000f);
            Assert.That((bool)Credit(1).IsConfirmed, Is.True);
            Assert.That(Credit(1).PendingGrantMask, Is.Zero);
            yield return WaitTicks(90);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator ZeroBase_DropsAllPendingBitsWithoutRewardOrActivityCounts()
        {
            yield return Begin();
            SetField(Credit(1), "_pvpBaseExperience", 0L);
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return Hit(0, 1, 10000f);
            Assert.That((bool)Credit(1).IsConfirmed, Is.True);
            Assert.That(Credit(1).AssistMask, Is.EqualTo(Bit(2)));
            Assert.That(Credit(1).PendingGrantMask, Is.Zero);
            AssertAll();
        }

        [UnityTest]
        public IEnumerator RejectedLedger_RetriesPendingGrantOnceAfterUnlock()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            SetField(_participants[0].ExperienceLedger, "_participant", null);
            yield return Hit(0, 1, 10000f);
            Assert.That(Credit(1).PendingGrantMask, Is.EqualTo(Bit(0)));
            AssertAll(assistMask: Bit(2));
            SetField(_participants[0].ExperienceLedger, "_participant", _participants[0]);
            yield return WaitFor(() => Credit(1).PendingGrantMask == 0);
            yield return WaitTicks(90);
            AssertAll(killer: 0, assistMask: Bit(2));
        }

        [UnityTest]
        public IEnumerator CopyStateFrom_PreservesCycleMasksConfirmationAndSupportTicks()
        {
            yield return Begin();
            yield return Hit(2, 1, 1f);
            yield return Hit(0, 1, 10000f);
            yield return InSimulation(() =>
            {
                Credit(1).RecordSupportReceived(Id(3), _runner.Tick);
                Credit(3).CopyStateFrom(Credit(1));
                Assert.That(Credit(3).CreditCycle, Is.EqualTo(Credit(1).CreditCycle));
                Assert.That(Credit(3).LastHitParticipantValue, Is.EqualTo(Credit(1).LastHitParticipantValue));
                Assert.That(Credit(3).AssistMask, Is.EqualTo(Credit(1).AssistMask));
                Assert.That(Credit(3).SupportReceivedTicks[Id(3).Value - 1], Is.EqualTo(_runner.Tick.Raw));
                Assert.That(Credit(3).TryGetLatestSupportReceived(Id(3), out int tick, out var kind), Is.True);
                Assert.That(tick, Is.EqualTo(_runner.Tick.Raw));
                Assert.That(kind, Is.EqualTo(CombatContributionKind.Healing));
                var sourceTracker = _players[1].GetComponent<CombatContributionTracker>();
                var restoredTracker = _players[3].GetComponent<CombatContributionTracker>();
                restoredTracker.CopyStateFrom(sourceTracker);
                Assert.That(restoredTracker.TryGetLatestContribution(Id(2), out _, out var restoredKind), Is.True);
                Assert.That(restoredKind, Is.EqualTo(CombatContributionKind.Damage));
            });
            SetField(_participants[0].ExperienceLedger, "_participant", null);
            yield return Hit(0, 1, 10000f);
            yield return InSimulation(() =>
            {
                Credit(3).CopyStateFrom(Credit(1));
                Assert.That((bool)Credit(3).IsConfirmed, Is.True);
                Assert.That(Credit(3).PendingGrantMask, Is.EqualTo(Bit(0)));
                var timerProperty = typeof(PlayerPvpCombatCreditNetworkController).GetProperty("GrantRetryTimer", Members);
                Assert.That(timerProperty.GetValue(Credit(3)), Is.EqualTo(timerProperty.GetValue(Credit(1))));
            });
            AssertAll(assistMask: Bit(2));
            // Do not unlock two copies of one producer: this test copies snapshots, not live producer ownership.
        }

        [UnityTest]
        public IEnumerator TrackerWindowBoundaryAndKinds_PreserveDefaultDamageSemantics()
        {
            yield return Begin();
            var tracker = _players[1].GetComponent<CombatContributionTracker>();
            var contributors = new HashSet<RaidParticipantId>();
            yield return InSimulation(() =>
            {
                int tick = _runner.Tick;
                tracker.TryRecordContribution(Id(2), tick);
                tracker.GetValidContributors(tick + tracker.AssistWindowTicks, contributors);
                Assert.That(contributors, Does.Contain(Id(2)));
                contributors.Clear();
                tracker.GetValidContributors(tick + tracker.AssistWindowTicks + 1, contributors);
                Assert.That(contributors, Is.Empty);
                Assert.That(tracker.TryGetLatestContribution(Id(2), out int recordedTick, out var kind), Is.True);
                Assert.That(recordedTick, Is.EqualTo(tick));
                Assert.That(kind, Is.EqualTo(CombatContributionKind.Damage));
            });
            AssertAll();
        }

        private IEnumerator Begin()
        {
            var owner = new GameObject("PvpCombatCreditTestRunner");
            _runner = owner.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            owner.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
            _driver = owner.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            RaidCode.TryParse("123456", out var code);
            RaidTeamId.TryCreate(1, out var attackers);
            RaidTeamId.TryCreate(2, out var defenders);
            var host = new ProfileId("pvp-1");
            var roster = new[]
            {
                new RaidLaunchParticipant(host, attackers),
                new RaidLaunchParticipant(new ProfileId("pvp-2"), defenders),
                new RaidLaunchParticipant(new ProfileId("pvp-3"), attackers),
                new RaidLaunchParticipant(new ProfileId("pvp-4"), attackers)
            };
            Assert.That(RaidLaunchContext.TryCreate(code, host, roster, host, 1, out var launch), Is.True);
            var manager = owner.AddComponent<NetworkSpawnManager>();
            Assert.That(manager.InitializeForRunner(_runner, default, default,
                Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-246-{Guid.NewGuid():N}",
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                RaidParticipantIdAssignment.TryResolve(launch.ParticipantProfileIds, roster[i].ProfileId, out var id);
                var participantObject = _runner.Spawn(Load(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize($"pvp-{index + 1}", id,
                        ProgressionBalanceDefaults.InitialCharacterAttributeState, ExperienceCurve.InitialLevel, 0, "pvp-test"));
                _participants[i] = participantObject.GetComponent<NetworkRaidParticipant>();
                manager.Test_RegisterParticipant(PlayerRef.FromEncoded(i + 1), participantObject);
                var avatar = _runner.Spawn(Load(PlayerGuid), Vector3.right * (i * 10f), Quaternion.identity,
                    onBeforeSpawned: (_, instance) => instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
                _players[i] = avatar.GetComponent<PlayerCharacter>();
                Assert.That(_participants[i].TrySetCurrentAvatar(avatar), Is.True);
                Assert.That(avatar.GetComponent<CombatContributionTracker>(), Is.Not.Null, "Parent must compose NetworkPlayer first.");
                Assert.That(Credit(i), Is.Not.Null, "Parent must compose NetworkPlayer first.");
                SetField(Downed(i), "_downedDrainPerSecond", 0f);
            }
            yield return null;
        }

        private DamageResult Resolve(int attacker, int target, float damage, RaidParticipantId empowerSource = default) =>
            _players[target].GetComponent<DamageResolver>().Resolve(new DamageRequest(
                attacker < 0 ? default : _players[attacker].Id, _players[target].Id, damage, DamageType.TrueDamage,
                Vector2.right, _players[target].transform.position, _runner.Tick, empowerSourceId: empowerSource));

        private IEnumerator Hit(int attacker, int target, float damage) =>
            InSimulation(() => Assert.That(Resolve(attacker, target, damage).IsApplied, Is.True));

        private PlayerPvpCombatCreditNetworkController Credit(int i) => _players[i].GetComponent<PlayerPvpCombatCreditNetworkController>();
        private PlayerDownedStateNetworkController Downed(int i) => _players[i].GetComponent<PlayerDownedStateNetworkController>();
        private RaidParticipantId Id(int i) => _participants[i].RaidParticipantId;
        private int Bit(int i) => 1 << (Id(i).Value - 1);
        private NetworkObject Load(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

        private IEnumerator InSimulation(Action operation)
        {
            int before = _driver.CompletionSequence;
            _driver.RequestOperation(operation);
            yield return WaitFor(() => _driver.CompletionSequence != before);
        }

        private IEnumerator WaitTicks(int count)
        {
            int until = _runner.Tick.Raw + count;
            yield return WaitFor(() => _runner.Tick.Raw >= until, Math.Max(8f, count * _runner.DeltaTime * 3f));
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeout = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Expected authoritative PvP credit state was not reached.");
        }

        private void AssertAll(int killer = -1, int assistMask = 0)
        {
            for (int i = 0; i < 4; i++) AssertLedger(i, i == killer ? 25 : 0, (assistMask & Bit(i)) != 0 ? 12 : 0);
        }

        private void AssertLedger(int i, long kill, long assist)
        {
            var ledger = _participants[i].ExperienceLedger;
            Assert.That(ledger.KillExperience, Is.EqualTo(kill), $"player {i} kill XP");
            Assert.That(ledger.AssistExperience, Is.EqualTo(assist), $"player {i} assist XP");
            Assert.That(ledger.PvpKillCount, Is.EqualTo(kill > 0 ? 1 : 0));
            Assert.That(ledger.PvpAssistCount, Is.EqualTo(assist > 0 ? 1 : 0));
            Assert.That(ledger.PveKillCount, Is.Zero);
            Assert.That(ledger.PveAssistCount, Is.Zero);
            Assert.That(ledger.ExplorationExperience, Is.Zero);
            Assert.That(ledger.ExtractedLootExperience, Is.Zero);
            Assert.That(ledger.Snapshot.TotalExperience, Is.EqualTo(kill + assist));
        }

        private void SetAssistLimit(int limit)
        {
            for (int i = 0; i < 4; i++) SetField(Credit(i), "_maxAssistsPerTarget", limit);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Members).SetValue(target, value);
    }
}
#endif

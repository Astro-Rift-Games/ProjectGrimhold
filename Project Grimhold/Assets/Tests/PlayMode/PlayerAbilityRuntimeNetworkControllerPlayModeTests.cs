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
        private readonly System.Collections.Generic.List<ScriptableObject> _manaTestAssets = new();

        [UnityTest]
        public IEnumerator AbilityCycle_EffectiveAttributeChangesAreRevalidatedAtUse()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-attributes", new PreparedAbilityLoadout(new AbilityId("charge"), default));
            var runtime = SpawnAvatar(participant, true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            var attributeOverride = participant.GetComponent<RuntimeAttributeOverrideNetworkController>();
            for (int index = 0; index < 3; index++)
            {
                Assert.That(attributeOverride.RequestAdjustment(CharacterAttribute.Strength, -5), Is.True);
                yield return WaitTicks();
            }
            yield return PressSlot1();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.RequirementsNotMet));
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.Zero);
            Assert.That(runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.False);
            Assert.That(attributeOverride.RequestReset(CharacterAttribute.Strength), Is.True);
            yield return WaitTicks();
            yield return PressSlot1();
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_DownedStopsExecutionAndRetainedAvatarKeepsCooldown()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-downed",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return PressSlot1();
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
            float before = runtime.GetRemainingCooldownSeconds(UniversalAbilitySlot.Slot1);
            yield return InSimulation(() =>
            {
                runtime.Object.AssignInputAuthority(PlayerRef.None);
                Assert.That(runtime.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
            });
            yield return WaitTicks();
            var behaviour = runtime.GetComponent<TestAbilityExecutionBehaviour>();
            Assert.That(behaviour.LastStop, Is.EqualTo(AbilityExecutionStopReason.Downed));
            Assert.That(behaviour.Stops, Is.EqualTo(1));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var stopped);
            Assert.That(stopped.Cooldown.TargetTick, Is.EqualTo(accepted.Cooldown.TargetTick));
            Assert.That(runtime.GetRemainingCooldownSeconds(UniversalAbilitySlot.Slot1), Is.LessThan(before));
            yield return WaitTicks();
            Assert.That(behaviour.Begins, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_AcceptedAbilityInterruptsReviveButRejectedAbilityDoesNot()
        {
            yield return StartRunner();
            var reviver = SpawnAvatar(SpawnParticipant("ability-reviver",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            var target = SpawnAvatar(SpawnParticipant("ability-revive-target", default), true);
            yield return WaitUntil(() => reviver.IsInitialized && target.IsInitialized);
            var recovery = target.GetComponent<PlayerDownedRecoveryNetworkController>();
            typeof(PlayerDownedRecoveryNetworkController).GetProperty("TestTeamOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(recovery, (bool?)true);
            SetField(recovery, "_reviveDurationSeconds", 30f);
            Assert.That(target.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
            _inputDriver.Buttons.Set(PlayerInputButton.Interact, true);
            yield return WaitTicks();
            // The real interaction press may already have opened this exact session.
            if (!recovery.HasValidSession)
                Assert.That(recovery.TryBeginAssisted(reviver.GetComponent<PlayerCharacter>()), Is.True);
            Assert.That(recovery.HasValidSession, Is.True);
            Assert.That(recovery.ReviverId, Is.EqualTo(reviver.Object.Id));
            var behaviour = reviver.GetComponent<TestAbilityExecutionBehaviour>();
            behaviour.RejectStart = true;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(recovery.HasValidSession, Is.True);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, false);
            yield return WaitTicks();
            behaviour.RejectStart = false;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(behaviour.Begins, Is.EqualTo(1));
            Assert.That(recovery.HasValidSession, Is.False);
            Assert.That(recovery.CanBeginAssisted(reviver.GetComponent<PlayerCharacter>()), Is.False);
            behaviour.Complete = true;
            yield return WaitTicks();
            Assert.That(recovery.CanBeginAssisted(reviver.GetComponent<PlayerCharacter>()), Is.True);
        }

        [UnityTest]
        public IEnumerator AbilityCycle_WeaponSetChangeWaitsForExecutionToFinish()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-weaponset",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return PressSlot1();
            var equipment = runtime.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var catalog = (LootDefinitionCatalog)typeof(PlayerWeaponEquipmentNetworkController)
                .GetField("_lootCatalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(equipment);
            var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            Assert.That(catalog.TryGetIndex(sword.LootId, out int index), Is.True);
            yield return InSimulation(() =>
            {
                SetNetworked(equipment, "WeaponSetAMainHandCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "WeaponSetBMainHandCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "ActiveWeaponSetSlotValue", (int)WeaponSetSlot.SetA);
            });
            _inputDriver.Buttons.Set(PlayerInputButton.WeaponSetB, true);
            yield return WaitTicks();
            Assert.That(equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
            runtime.GetComponent<TestAbilityExecutionBehaviour>().Complete = true;
            _inputDriver.Buttons.Set(PlayerInputButton.WeaponSetB, false);
            yield return WaitTicks();
            _inputDriver.Buttons.Set(PlayerInputButton.WeaponSetB, true);
            yield return WaitTicks();
            Assert.That(equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetB));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_EquipIntoActiveMainHandIsRejectedWhileExecutionIsActive()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-equip-active",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return PressSlot1();
            var equipment = runtime.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var catalog = (LootDefinitionCatalog)typeof(PlayerWeaponEquipmentNetworkController)
                .GetField("_lootCatalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(equipment);
            var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            Assert.That(catalog.TryGetIndex(sword.LootId, out int index), Is.True);
            yield return InSimulation(() =>
            {
                SetNetworked(equipment, "WeaponSetAMainHandCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "ActiveWeaponSetSlotValue", (int)WeaponSetSlot.SetA);
            });
            Assert.That(runtime.HasActiveExecution, Is.True);
            EquipmentOperationResult result = default;
            yield return InSimulation(() => result = (EquipmentOperationResult)typeof(PlayerWeaponEquipmentNetworkController)
                .GetMethod("TryEquipAuthority", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(equipment, new object[] { index, EquipmentSlot.WeaponSetAMainHand }));
            Assert.That(result, Is.EqualTo(EquipmentOperationResult.PlayerUnavailable),
                "Replacing the active main-hand weapon is forbidden during an ability execution.");
            Assert.That(equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_StopIsDeliveredWhenSlotsWereUnboundBeforeParticipationEnded()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("ability-stop-unbound",
                new PreparedAbilityLoadout(new AbilityId("charge"), default));
            var runtime = SpawnAvatar(participant, true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return PressSlot1();
            var behaviour = runtime.GetComponent<TestAbilityExecutionBehaviour>();
            Assert.That(behaviour.Begins, Is.EqualTo(1));
            var link = runtime.GetComponent<RaidAvatarParticipantLink>();
            var participantId = link.ParticipantId;
            var idProperty = typeof(RaidAvatarParticipantLink).GetProperty("ParticipantId");
            // Transient unresolve: the runtime clears its slots but keeps its cached behaviours.
            yield return InSimulation(() => idProperty.SetValue(link, default(NetworkId)));
            yield return WaitTicks();
            Assert.That(runtime.IsInitialized, Is.False);
            Assert.That(behaviour.Stops, Is.Zero);
            yield return InSimulation(() =>
            {
                idProperty.SetValue(link, participantId);
                Assert.That(participant.TryMarkDefeated(runtime.Object), Is.True);
            });
            yield return WaitTicks();
            Assert.That(behaviour.Stops, Is.EqualTo(1), "Stop must reach the behaviour even when slots were unbound.");
            Assert.That(behaviour.LastStop, Is.EqualTo(AbilityExecutionStopReason.ParticipationEnded));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_MissingBehaviourRejectsAndManaPaysOnce()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-closed",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("arcane_projectile"))),
                true, "arcane_projectile");
            yield return WaitUntil(() => runtime.IsInitialized);
            var stamina = runtime.GetComponent<PlayerStaminaNetworkController>();
            float before = stamina.CurrentStamina;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot2, true);
            yield return WaitTicks();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.MissingBehaviour));
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot2), Is.EqualTo(AbilityActivationFailure.None),
                "A valid Mana ability must be accepted by the existing authoritative runtime.");
            Assert.That(stamina.CurrentStamina, Is.EqualTo(before));
            var mana = GetMana(runtime);
            runtime.TryGetSlot(UniversalAbilitySlot.Slot2, out var prepared);
            Assert.That(ReadMana(mana), Is.EqualTo(100f - prepared.Definition.Cost));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var rejected);
            Assert.That(rejected.Sequence, Is.Zero);
            Assert.That(rejected.Cooldown.IsRunning, Is.False);
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot2, out var accepted);
            Assert.That(accepted.Sequence, Is.EqualTo(1));
            Assert.That(accepted.Cooldown.IsRunning, Is.True);
            yield return WaitTicks();
            Assert.That(ReadMana(mana), Is.EqualTo(100f - prepared.Definition.Cost), "Held input cannot repeat payment.");
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_InsufficientManaLeavesExecutionAndCooldownUnchanged()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-mana-poor",
                new PreparedAbilityLoadout(new AbilityId("arcane_projectile"), default)), true, "arcane_projectile");
            yield return WaitUntil(() => runtime.IsInitialized);
            var mana = GetMana(runtime);
            yield return InSimulation(() => mana.GetType().GetProperty("CurrentMana").SetValue(mana, 1f));
            yield return PressSlot1();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.InsufficientResource));
            Assert.That(ReadMana(mana), Is.EqualTo(1f));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var state);
            Assert.That(state.Sequence, Is.Zero);
            Assert.That(state.Cooldown.IsRunning, Is.False);
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.Zero);
        }

        private static PlayerManaNetworkController GetMana(PlayerAbilityRuntimeNetworkController runtime)
        {
            var mana = runtime.GetComponent<PlayerManaNetworkController>();
            Assert.That(mana, Is.Not.Null, "The production Raid avatar must compose its Mana owner.");
            return mana;
        }

        private static float ReadMana(PlayerManaNetworkController mana) => mana.CurrentMana;

        [UnityTest]
        public IEnumerator ManaResource_NoPassiveRegenerationAndExplicitRestore()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("mana-resource", default), true);
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized);
            Assert.That(mana.CurrentMana, Is.EqualTo(100f));
            Assert.That(mana.TrySpend(0f), Is.False, "Zero spend must not bypass the simulation boundary.");
            Assert.That(mana.TryRestore(0f), Is.False);
            yield return InSimulation(() =>
            {
                Assert.That(mana.TrySpend(60f), Is.True);
                Assert.That(mana.TrySpend(float.NaN), Is.False);
                Assert.That(mana.TryRestore(float.PositiveInfinity), Is.False);
                Assert.That(mana.CurrentMana, Is.EqualTo(40f));
            });
            yield return WaitTicks();
            Assert.That(mana.CurrentMana, Is.EqualTo(40f));
            yield return InSimulation(() =>
            {
                Assert.That(mana.TryRestore(10f), Is.True);
                Assert.That(mana.CurrentMana, Is.EqualTo(50f));
                Assert.That(mana.TryRestore(1000f), Is.True);
                Assert.That(mana.CurrentMana, Is.EqualTo(100f));
            });
        }

        [UnityTest]
        public IEnumerator ManaResource_DownedReviveAndReenablePreserveBalance()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("mana-downed", default), true);
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized);
            yield return InSimulation(() =>
            {
                Assert.That(mana.TrySpend(60f), Is.True);
                Assert.That(runtime.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
                runtime.Object.AssignInputAuthority(PlayerRef.None);
            });
            yield return WaitTicks();
            Assert.That(mana.CurrentMana, Is.EqualTo(40f));
            yield return InSimulation(() => Assert.That(typeof(PlayerCharacter)
                .GetMethod("TryRestoreFromDowned", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime.GetComponent<PlayerCharacter>(), new object[] { 20f }), Is.True));
            mana.enabled = false;
            yield return WaitTicks();
            mana.enabled = true;
            yield return WaitTicks();
            Assert.That(mana.IsInitialized, Is.True);
            Assert.That(mana.CurrentMana, Is.EqualTo(40f));
        }

        [UnityTest]
        public IEnumerator ManaResource_CopiedStateWaitsForFixupWithoutRefill()
        {
            yield return StartRunner();
            var source = SpawnAvatar(SpawnParticipant("mana-copy-source", default), true);
            var sourceMana = GetMana(source);
            yield return WaitUntil(() => sourceMana.IsInitialized);
            yield return InSimulation(() => Assert.That(sourceMana.TrySpend(73f), Is.True));
            var target = SpawnAvatar(null, false);
            var targetMana = GetMana(target);
            SetField(targetMana, "_restoreSpawn", true);
            yield return InSimulation(() => targetMana.CopyStateFrom(sourceMana));
            yield return WaitTicks();
            Assert.That(targetMana.CurrentMana, Is.EqualTo(27f));
            Assert.That(targetMana.IsInitialized, Is.False);
            var participant = SpawnParticipant("mana-copy-target", default);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitUntil(() => targetMana.IsInitialized);
            Assert.That(targetMana.CurrentMana, Is.EqualTo(27f));
            targetMana.enabled = false;
            targetMana.enabled = true;
            yield return WaitTicks();
            Assert.That(targetMana.CurrentMana, Is.EqualTo(27f));
        }

        [UnityTest]
        public IEnumerator ManaResource_UninitializedCopiedStateStaysFrozen()
        {
            yield return StartRunner();
            var source = SpawnAvatar(null, false);
            var target = SpawnAvatar(null, false);
            var mana = GetMana(target);
            SetField(mana, "_restoreSpawn", true);
            yield return InSimulation(() => mana.CopyStateFrom(GetMana(source)));
            var participant = SpawnParticipant("mana-copy-uninitialized", default);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitTicks();
            Assert.That(mana.IsInitialized, Is.False);
            Assert.That(mana.CurrentMana, Is.Zero);
            yield return InSimulation(() =>
            {
                Assert.That(mana.TrySpend(0f), Is.False);
                Assert.That(mana.TryRestore(100f), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator ManaResource_TerminalParticipationClearsAndNewExpeditionStartsFull()
        {
            yield return StartRunner();
            var participant = SpawnParticipant("mana-terminal", default);
            var runtime = SpawnAvatar(participant, true);
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized);
            yield return InSimulation(() =>
            {
                Assert.That(mana.TrySpend(20f), Is.True);
                Assert.That(participant.TryMarkDefeated(runtime.Object), Is.True);
            });
            yield return WaitTicks();
            Assert.That(mana.IsInitialized, Is.False);
            Assert.That(mana.CurrentMana, Is.Zero);
            yield return InSimulation(() => Assert.That(mana.TryRestore(100f), Is.False));
            var next = SpawnAvatar(SpawnParticipant("mana-new-expedition", default), true);
            yield return WaitUntil(() => GetMana(next).IsInitialized);
            Assert.That(GetMana(next).CurrentMana, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_ManaRejectedPlanAndInterruptionDoNotPayOrRefund()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("mana-interruption",
                new PreparedAbilityLoadout(new AbilityId("arcane_projectile"), default)), true, "arcane_projectile");
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized && runtime.IsInitialized);
            var behaviour = runtime.GetComponent<TestAbilityExecutionBehaviour>();
            behaviour.RejectStart = true;
            yield return PressSlot1();
            Assert.That(mana.CurrentMana, Is.EqualTo(100f));
            behaviour.RejectStart = false;
            yield return PressSlot1();
            float paidBalance = mana.CurrentMana;
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var accepted);
            yield return InSimulation(() => Assert.That(runtime.TryInterrupt(UniversalAbilitySlot.Slot1,
                accepted.Sequence, AbilityExecutionStopReason.Stun), Is.True));
            Assert.That(mana.CurrentMana, Is.EqualTo(paidBalance));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var stopped);
            Assert.That(stopped.Cooldown.TargetTick, Is.EqualTo(accepted.Cooldown.TargetTick));
            Assert.That(stopped.Sequence, Is.EqualTo(accepted.Sequence));
        }

        [UnityTest]
        public IEnumerator ManaResource_MaximumChangesClampWithoutRefill()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("mana-equipment", default), true);
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized);
            var armor = ScriptableObject.CreateInstance<ArmorDefinition>();
            var loot = ScriptableObject.CreateInstance<LootDefinition>();
            var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _manaTestAssets.AddRange(new ScriptableObject[] { armor, loot, catalog });
            SetField(armor, "_maximumResourceModifier", new MaximumResourceModifier(MaximumResourceType.Mana, 50));
            SetField(loot, "_id", "mana_test_armor");
            SetField(loot, "_category", LootCategory.Armor);
            SetField(loot, "_armorDefinition", armor);
            SetField(catalog, "_definitions", new System.Collections.Generic.List<LootDefinition> { loot });
            Assert.That(catalog.TryGetIndex(loot.LootId, out int index), Is.True);
            var equipment = runtime.GetComponent<PlayerWeaponEquipmentNetworkController>();
            yield return InSimulation(() =>
            {
                Assert.That(mana.TrySpend(60f), Is.True);
                SetField(equipment, "_lootCatalog", catalog);
                SetNetworked(equipment, "ArmorCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "EquipmentRevision", equipment.EquipmentRevision + 1);
                Assert.That(mana.TryGetMaximumMana(out float maximum), Is.True);
                Assert.That(maximum, Is.EqualTo(150f));
                Assert.That(mana.CurrentMana, Is.EqualTo(40f), "A maximum increase is not restoration.");
                Assert.That(mana.TryRestore(200f), Is.True);
                Assert.That(mana.CurrentMana, Is.EqualTo(150f));
                SetNetworked(equipment, "ArmorCatalogIndexPlusOne", 0);
                SetNetworked(equipment, "EquipmentRevision", equipment.EquipmentRevision + 1);
                Assert.That(mana.CanSpend(120f), Is.False, "Payment must use the new maximum in the same tick.");
                Assert.That(mana.TrySpend(120f), Is.False);
                Assert.That(mana.CurrentMana, Is.EqualTo(100f), "An unaffordable payment still clamps obsolete excess; clamping is not partial payment.");
                Assert.That(mana.TrySpend(1f), Is.True);
                Assert.That(mana.CurrentMana, Is.EqualTo(99f));
            });
            yield return WaitTicks();
            Assert.That(mana.CurrentMana, Is.EqualTo(99f));
            yield return InSimulation(() =>
            {
                SetNetworked(equipment, "ArmorCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "EquipmentRevision", equipment.EquipmentRevision + 1);
                Assert.That(mana.TryRestore(200f), Is.True);
                SetNetworked(equipment, "ArmorCatalogIndexPlusOne", 0);
                SetNetworked(equipment, "EquipmentRevision", equipment.EquipmentRevision + 1);
            });
            yield return WaitTicks();
            Assert.That(mana.CurrentMana, Is.EqualTo(100f), "Idle simulation must clamp without any payment or restoration request.");
            yield return InSimulation(() =>
            {
                SetNetworked(equipment, "ArmorCatalogIndexPlusOne", index + 1);
                SetNetworked(equipment, "EquipmentRevision", equipment.EquipmentRevision + 1);
            });
            yield return WaitTicks();
            Assert.That(mana.CurrentMana, Is.EqualTo(100f), "Increasing the maximum after a clamp must not restore discarded excess.");
        }

        [UnityTest]
        public IEnumerator AbilityCycle_TwoManaSlotsRecheckTheSharedBalance()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("mana-two-slots",
                new PreparedAbilityLoadout(new AbilityId("arcane_projectile"), new AbilityId("empower"))),
                true, "arcane_projectile", "empower");
            var mana = GetMana(runtime);
            yield return WaitUntil(() => mana.IsInitialized && runtime.IsInitialized);
            yield return InSimulation(() => Assert.That(mana.TrySpend(80f), Is.True));
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot2, true);
            yield return WaitTicks();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.None));
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot2), Is.EqualTo(AbilityActivationFailure.InsufficientResource));
            Assert.That(mana.CurrentMana, Is.EqualTo(5f));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot2, out var rejected);
            Assert.That(rejected.Sequence, Is.Zero);
            Assert.That(rejected.Cooldown.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator AbilityCycle_CopiedManaExecutionRebindsWithoutRepayment()
        {
            yield return StartRunner();
            var prepared = new PreparedAbilityLoadout(new AbilityId("arcane_projectile"), default);
            var source = SpawnAvatar(SpawnParticipant("mana-execution-source", prepared), true, "arcane_projectile");
            yield return WaitUntil(() => source.IsInitialized && GetMana(source).IsInitialized);
            yield return PressSlot1();
            float balance = GetMana(source).CurrentMana;
            source.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var original);
            var target = SpawnAvatar(null, false, "arcane_projectile");
            SetRestoreGuard(target);
            SetField(GetMana(target), "_restoreSpawn", true);
            yield return CopyState(target, source);
            yield return InSimulation(() => GetMana(target).CopyStateFrom(GetMana(source)));
            var participant = SpawnParticipant("mana-execution-target", prepared);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitUntil(() => target.IsInitialized && GetMana(target).IsInitialized);
            yield return WaitTicks();
            Assert.That(GetMana(target).CurrentMana, Is.EqualTo(balance));
            Assert.That(target.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.Zero);
            target.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var restored);
            Assert.That(restored.Sequence, Is.EqualTo(original.Sequence));
            Assert.That(restored.Cooldown.TargetTick, Is.EqualTo(original.Cooldown.TargetTick));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_RejectedPlanAndInsufficientStaminaHaveNoCostOrCooldown()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-rejected",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            var behaviour = runtime.GetComponent<TestAbilityExecutionBehaviour>();
            var stamina = runtime.GetComponent<PlayerStaminaNetworkController>();
            float before = stamina.CurrentStamina;
            behaviour.RejectStart = true;
            yield return PressSlot1();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.BehaviourRejected));
            Assert.That(stamina.CurrentStamina, Is.EqualTo(before));
            behaviour.RejectStart = false;
            yield return InSimulation(() => typeof(PlayerStaminaNetworkController).GetProperty("CurrentStamina").SetValue(stamina, 1f));
            yield return PressSlot1();
            Assert.That(runtime.GetLastActivationFailure(UniversalAbilitySlot.Slot1), Is.EqualTo(AbilityActivationFailure.InsufficientResource));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var state);
            Assert.That(state.Sequence, Is.Zero);
            Assert.That(state.Cooldown.IsRunning, Is.False);
            Assert.That(stamina.CurrentStamina, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_IndependentSlotsCompleteAndCooldownExpiresWithoutQueuedHeldPress()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-independent",
                new PreparedAbilityLoadout(new AbilityId("charge"), new AbilityId("trap"))), true, "charge", "trap");
            yield return WaitUntil(() => runtime.IsInitialized);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot2, true);
            yield return WaitTicks();
            var behaviours = runtime.GetComponents<TestAbilityExecutionBehaviour>();
            Assert.That(behaviours[0].Begins, Is.EqualTo(1));
            Assert.That(behaviours[1].Begins, Is.EqualTo(1));
            Assert.That(runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), Is.True);
            Assert.That(runtime.IsOnCooldown(UniversalAbilitySlot.Slot2), Is.True);
            Assert.That(runtime.GetRemainingCooldownSeconds(UniversalAbilitySlot.Slot1),
                Is.GreaterThan(runtime.GetRemainingCooldownSeconds(UniversalAbilitySlot.Slot2)));
            behaviours[0].Complete = true;
            yield return WaitTicks();
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var first);
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot2, out var second);
            Assert.That(first.Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
            Assert.That(second.Phase, Is.EqualTo(AbilityExecutionPhase.Executing));
            Assert.That(behaviours[0].Stops, Is.EqualTo(1));
            Assert.That(runtime.IsSlotAvailable(UniversalAbilitySlot.Slot1), Is.True, "Prepared-slot availability retains its original meaning.");
            yield return WaitUntil(() => !runtime.IsOnCooldown(UniversalAbilitySlot.Slot1), 12f);
            Assert.That(runtime.GetRemainingCooldownSeconds(UniversalAbilitySlot.Slot1), Is.Zero);
            Assert.That(behaviours[0].Begins, Is.EqualTo(1), "Held input cannot queue across cooldown expiry.");
            behaviours[0].Complete = false;
            yield return PressSlot1();
            Assert.That(behaviours[0].Begins, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_PreparationProgressesWithoutInputAndInterruptIsSequenceScoped()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-preparing",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            var behaviour = runtime.GetComponent<TestAbilityExecutionBehaviour>();
            behaviour.PreparingSeconds = 0.2f;
            yield return PressSlot1();
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var preparing);
            Assert.That(preparing.Phase, Is.EqualTo(AbilityExecutionPhase.Preparing));
            var targetTick = preparing.Cooldown.TargetTick;
            _inputDriver.ProvidePayload = false;
            yield return WaitUntil(() => runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var s) &&
                s.Phase == AbilityExecutionPhase.Executing);
            bool staleAccepted = true;
            bool accepted = false;
            yield return InSimulation(() =>
            {
                staleAccepted = runtime.TryInterrupt(UniversalAbilitySlot.Slot1, preparing.Sequence + 1, AbilityExecutionStopReason.Knockback);
                accepted = runtime.TryInterrupt(UniversalAbilitySlot.Slot1, preparing.Sequence, AbilityExecutionStopReason.Stun);
            });
            Assert.That(staleAccepted, Is.False);
            Assert.That(accepted, Is.True);
            Assert.That(behaviour.Stops, Is.EqualTo(1));
            runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var stopped);
            Assert.That(stopped.Phase, Is.EqualTo(AbilityExecutionPhase.Idle));
            Assert.That(stopped.Cooldown.TargetTick, Is.EqualTo(targetTick));
            Assert.That(runtime.TryInterrupt(UniversalAbilitySlot.Slot1, preparing.Sequence, AbilityExecutionStopReason.Stun), Is.False,
                "An operation outside simulation is rejected.");
        }

        [UnityTest]
        public IEnumerator AbilityCycle_CopiedActiveStateWaitsForFixupAndRebindsWithoutStartingOrPaying()
        {
            yield return StartRunner();
            var prepared = new PreparedAbilityLoadout(new AbilityId("charge"), default);
            var source = SpawnAvatar(SpawnParticipant("ability-cycle-source", prepared), true, "charge");
            yield return WaitUntil(() => source.IsInitialized);
            yield return PressSlot1();
            source.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var original);
            var target = SpawnAvatar(null, false, "charge");
            SetRestoreGuard(target);
            yield return CopyState(target, source);
            yield return WaitTicks();
            Assert.That(target.IsInitialized, Is.False);
            var participant = SpawnParticipant("ability-cycle-copy", prepared);
            target.GetComponent<RaidAvatarParticipantLink>().SetRestoredParticipant(participant.Object.Id);
            Assert.That(participant.TrySetCurrentAvatar(target.Object), Is.True);
            yield return WaitUntil(() => target.IsInitialized);
            yield return WaitTicks();
            var behaviour = target.GetComponent<TestAbilityExecutionBehaviour>();
            Assert.That(behaviour.Begins, Is.Zero);
            Assert.That(behaviour.Rebinds, Is.EqualTo(1));
            target.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var copied);
            Assert.That(copied.Sequence, Is.EqualTo(original.Sequence));
            Assert.That(copied.Cooldown.TargetTick, Is.EqualTo(original.Cooldown.TargetTick));
            target.enabled = false;
            Assert.That(target.HasActiveExecution, Is.True,
                "Disabling the runtime must not make authoritative action gates ignore its copied active phase.");
            target.enabled = true;
            yield return WaitTicks();
            Assert.That(behaviour.Rebinds, Is.EqualTo(2));
            Assert.That(behaviour.Begins, Is.Zero);
            Assert.That(participant.TryMarkDefeated(target.Object), Is.True);
            yield return WaitTicks();
            Assert.That(behaviour.Stops, Is.EqualTo(1));
            Assert.That(target.IsInitialized, Is.False);
        }

        [UnityTest]
        public IEnumerator AbilityCycle_ConsumablesAndInteractionsRejectWhileExecutionIsActive()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-actions",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized);
            yield return PressSlot1();
            ConsumableResult result = default;
            yield return InSimulation(() => result = (ConsumableResult)typeof(PlayerConsumableNetworkController)
                .GetMethod("ProcessAuthoritativeConsume", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime.GetComponent<PlayerConsumableNetworkController>(), new object[] { -1 }));
            Assert.That(result.FailureReason, Is.EqualTo(ConsumableFailureReason.TargetUnavailable));
            var interaction = runtime.GetComponent<PlayerInteractionNetworkController>();
            _inputDriver.Buttons.Set(PlayerInputButton.Interact, true);
            yield return WaitTicks();
            var failure = (int)typeof(PlayerInteractionNetworkController)
                .GetProperty("LastInteractionFailureReasonValue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(interaction);
            Assert.That(failure, Is.EqualTo((int)InteractionFailureReason.InteractorUnavailable));
        }

        [UnityTest]
        public IEnumerator AbilityCycle_ValidIntentPaysAndStartsExactlyOnce()
        {
            yield return StartRunner();
            var runtime = SpawnAvatar(SpawnParticipant("ability-cycle",
                new PreparedAbilityLoadout(new AbilityId("charge"), default)), true, "charge");
            yield return WaitUntil(() => runtime.IsInitialized && runtime.GetComponent<PlayerStaminaNetworkController>().CanSpend(20f));
            var stamina = runtime.GetComponent<PlayerStaminaNetworkController>();
            float before = stamina.CurrentStamina;
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.EqualTo(1),
                "A valid authoritative intent must accept one execution.");
            Assert.That(runtime.TryGetExecutionSnapshot(UniversalAbilitySlot.Slot1, out var snapshot), Is.True);
            Assert.That(snapshot.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Phase, Is.EqualTo(AbilityExecutionPhase.Executing));
            Assert.That(snapshot.Cooldown.IsRunning, Is.True);
            Assert.That(stamina.CurrentStamina, Is.EqualTo(before - 20f).Within(0.01f));
            yield return WaitTicks();
            Assert.That(runtime.GetComponent<TestAbilityExecutionBehaviour>().Begins, Is.EqualTo(1));
        }

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
            foreach (var asset in _manaTestAssets) Object.DestroyImmediate(asset);
            _manaTestAssets.Clear();
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

        private PlayerAbilityRuntimeNetworkController SpawnAvatar(NetworkRaidParticipant participant, bool publishAvatar,
            params string[] behaviourIds)
        {
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");
            var avatar = _runner.Spawn(LoadPrefab("fea3a7b256f965a4eb9b965832939741"),
                Vector3.zero, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant != null ? participant.Object : null);
                    var runtime = instance.GetComponent<PlayerAbilityRuntimeNetworkController>();
                    SetField(runtime, "_playerCharacter", instance.GetComponent<PlayerCharacter>());
                    SetField(runtime, "_staminaController", instance.GetComponent<PlayerStaminaNetworkController>());
                    SetField(instance.GetComponent<PlayerStaminaNetworkController>(), "_regenerationPerSecond", 0f);
                    if (behaviourIds.Length > 0)
                    {
                        var catalog = (AbilityDefinitionCatalog)typeof(PlayerAbilityRuntimeNetworkController)
                            .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                        var behaviours = new AbilityExecutionBehaviour[behaviourIds.Length];
                        for (int index = 0; index < behaviours.Length; index++)
                        {
                            Assert.That(catalog.TryGet(new AbilityId(behaviourIds[index]), out var definition), Is.True);
                            var behaviour = instance.gameObject.AddComponent<TestAbilityExecutionBehaviour>();
                            SetField(behaviour, "_definition", definition, typeof(AbilityExecutionBehaviour));
                            behaviours[index] = behaviour;
                        }
                        SetField(runtime, "_executionBehaviours", behaviours);
                    }
                });
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

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Ability runtime did not reach the expected state.");
        }

        private IEnumerator PressSlot1()
        {
            _inputDriver.Buttons = default;
            yield return WaitTicks();
            _inputDriver.Buttons.Set(PlayerInputButton.AbilitySlot1, true);
            yield return WaitTicks();
        }

        private IEnumerator InSimulation(Action operation)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestOperation(operation);
            yield return WaitUntil(() => _driver.CompletionSequence != previous);
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

        private static void SetField(object target, string name, object value, Type declaringType = null) =>
            (declaringType ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static void SetNetworked(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

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

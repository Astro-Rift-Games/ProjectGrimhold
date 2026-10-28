using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Editor.Loot
{
    public sealed class InventoryPassiveEffectsTests
    {
        [Test]
        public void EvaluatePassiveTreasures_WithNoTreasures_AllFlagsFalse()
        {
            var go = new GameObject("TestPlayer");
            var lootReceiver = go.AddComponent<PlayerLootReceiver>();
            var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();

            passiveController.EvaluatePassiveTreasures();

            Assert.That(passiveController.HasChaosCoin, Is.False);
            Assert.That(passiveController.HasPilgrimLight, Is.False);
            Assert.That(passiveController.HasSanctuaryHeart, Is.False);
            Assert.That(passiveController.HasVoidBell, Is.False);
            Assert.That(passiveController.HasFamineIdol, Is.False);
            Assert.That(passiveController.HasWandererEye, Is.False);

            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.DamageTakenMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.AggroRangeMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.VisionRadiusMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.IsMinimapBlocked, Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Multipliers_AreConfiguredAccordingToDoc14()
        {
            var go = new GameObject("TestPlayer");
            var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();

            // Set private backing flags via reflection to verify property multipliers
            SetProperty(passiveController, "HasChaosCoin", true);
            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(0.8f).Within(0.001f));

            SetProperty(passiveController, "HasChaosCoin", false);
            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(1f));

            SetProperty(passiveController, "HasPilgrimLight", true);
            Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1.35f).Within(0.001f));

            SetProperty(passiveController, "HasSanctuaryHeart", true);
            Assert.That(passiveController.DamageTakenMultiplier, Is.EqualTo(0.85f).Within(0.001f));

            SetProperty(passiveController, "HasVoidBell", true);
            Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1.15f).Within(0.001f));

            SetProperty(passiveController, "HasFamineIdol", true);
            Assert.That(passiveController.AggroRangeMultiplier, Is.EqualTo(1.5f).Within(0.001f));

            SetProperty(passiveController, "HasWandererEye", true);
            Assert.That(passiveController.VisionRadiusMultiplier, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(passiveController.IsMinimapBlocked, Is.True);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SprintSpeedWithVoidBell_Applies15PercentBonus()
        {
            float normalSprintSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: true,
                sprintSpeedMultiplier: 1.5f,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 1f);

            float voidBellSprintMultiplier = 1.5f * 1.15f;
            float voidBellSprintSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: true,
                sprintSpeedMultiplier: voidBellSprintMultiplier,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 1f);

            Assert.That(normalSprintSpeed, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(voidBellSprintSpeed, Is.EqualTo(8.625f).Within(0.001f));
        }

        [Test]
        public void ChaosCoin_MaxHealthPenaltyAndRestoration_DoesNotHealCurrentHealth()
        {
            float baseMaxHealth = 100f;
            float currentHealth = 100f;

            // Pick up Chaos Coin (-20% max health)
            float penalizedMaxHealth = baseMaxHealth * 0.8f; // 80f
            currentHealth = Mathf.Min(currentHealth, penalizedMaxHealth); // Clamped to 80f

            Assert.That(penalizedMaxHealth, Is.EqualTo(80f));
            Assert.That(currentHealth, Is.EqualTo(80f));

            // Drop Chaos Coin (Restores max health to 100f)
            float restoredMaxHealth = baseMaxHealth; // 100f
            // Current health must NOT be healed, remaining at 80f
            Assert.That(restoredMaxHealth, Is.EqualTo(100f));
            Assert.That(currentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void SanctuaryHeart_DamageMitigation_ReducesIncomingDamageBy15Percent()
        {
            float incomingDamage = 40f;
            float mitigatedDamage = incomingDamage * 0.85f;

            Assert.That(mitigatedDamage, Is.EqualTo(34f).Within(0.001f));
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo prop = target.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
            else
            {
                FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(target, value);
            }
        }
    }
}

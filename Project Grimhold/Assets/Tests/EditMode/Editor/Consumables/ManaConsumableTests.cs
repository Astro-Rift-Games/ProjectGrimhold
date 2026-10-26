using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Editor.Consumables
{
    public sealed class ManaConsumableTests
    {
        private sealed class TestManaReceiverCharacter : ICharacter, IManaReceiver
        {
            public EntityId Id => new(1);
            public bool IsAlive { get; set; } = true;
            public float CurrentMana { get; set; }
            public float MaximumMana { get; set; }

            public TestManaReceiverCharacter(float currentMana, float maximumMana)
            {
                CurrentMana = currentMana;
                MaximumMana = maximumMana;
            }

            public bool TryRestoreMana(float amount, out string failureReason)
            {
                if (!IsAlive)
                {
                    failureReason = "El objetivo está muerto.";
                    return false;
                }

                if (CurrentMana >= MaximumMana)
                {
                    failureReason = "El maná ya está al máximo.";
                    return false;
                }

                CurrentMana = Mathf.Min(CurrentMana + amount, MaximumMana);
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class TestNonManaCharacter : ICharacter
        {
            public EntityId Id => new(2);
            public bool IsAlive => true;
        }

        [Test]
        public void CanApply_WhenManaIsFull_ReturnsFalseWithCorrectMessage()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            var target = new TestManaReceiverCharacter(100f, 100f);

            bool canApply = effect.CanApply(target, out string reason);

            Assert.That(canApply, Is.False);
            Assert.That(reason, Is.EqualTo("El maná ya está al máximo."));
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void CanApply_WhenManaIsNotFull_ReturnsTrue()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            var target = new TestManaReceiverCharacter(60f, 100f);

            bool canApply = effect.CanApply(target, out string reason);

            Assert.That(canApply, Is.True);
            Assert.That(reason, Is.Empty);
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void CanApply_WhenTargetIsDead_ReturnsFalse()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            var target = new TestManaReceiverCharacter(50f, 100f) { IsAlive = false };

            bool canApply = effect.CanApply(target, out string reason);

            Assert.That(canApply, Is.False);
            Assert.That(reason, Is.EqualTo("El objetivo está muerto."));
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void CanApply_WhenTargetIsNotManaReceiver_ReturnsFalse()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            var target = new TestNonManaCharacter();

            bool canApply = effect.CanApply(target, out string reason);

            Assert.That(canApply, Is.False);
            Assert.That(reason, Is.EqualTo("El objetivo no puede recibir maná."));
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void TryApplyEffect_RestoresManaAndCapsAtMaximum()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            // Use reflection or serialized object to set _manaAmount to 40
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_manaAmount").floatValue = 40f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var target = new TestManaReceiverCharacter(70f, 100f);

            bool success = effect.TryApplyEffect(target, out string reason);

            Assert.That(success, Is.True);
            Assert.That(reason, Is.Empty);
            Assert.That(target.CurrentMana, Is.EqualTo(100f)); // 70 + 40 clamped to 100
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void TryApplyEffect_WhenManaFull_FailsWithoutRestoring()
        {
            var effect = ScriptableObject.CreateInstance<ManaConsumableEffect>();
            var target = new TestManaReceiverCharacter(100f, 100f);

            bool success = effect.TryApplyEffect(target, out string reason);

            Assert.That(success, Is.False);
            Assert.That(reason, Is.EqualTo("El maná ya está al máximo."));
            Assert.That(target.CurrentMana, Is.EqualTo(100f));
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void ResolveVoluntarySpeed_WithConsumableMultiplier_AppliesReduction()
        {
            float normalSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: false,
                sprintSpeedMultiplier: 1.5f,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 1f);

            float potionSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: false,
                sprintSpeedMultiplier: 1.5f,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 0.5f);

            Assert.That(normalSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(potionSpeed, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void ShouldSprint_DuringConsumableAction_IsSuppressed()
        {
            var input = new PlayerNetworkInput { MoveDirection = Vector2.right };
            input.Buttons.Set(PlayerInputButton.Sprint, true);

            bool sprintWithAction = PlayerMovementNetworkController.ShouldSprint(
                in input,
                hasInput: true,
                moveDirection: Vector2.right,
                canMove: true,
                isDowned: false,
                isActionExecuting: true); // True when IsConsuming is true

            bool sprintWithoutAction = PlayerMovementNetworkController.ShouldSprint(
                in input,
                hasInput: true,
                moveDirection: Vector2.right,
                canMove: true,
                isDowned: false,
                isActionExecuting: false);

            Assert.That(sprintWithAction, Is.False);
            Assert.That(sprintWithoutAction, Is.True);
        }

        [Test]
        public void ManaPotionAssets_AreConfiguredAccordingToDoc14()
        {
            var effect = AssetDatabase.LoadAssetAtPath<ManaConsumableEffect>(
                "Assets/Scriptable Objects/ManaPotionEffect.asset");
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.ManaAmount, Is.EqualTo(40f));

            var definition = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(
                "Assets/Scriptable Objects/ManaPotionDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.ConsumptionDurationSeconds, Is.EqualTo(2.5f));
            Assert.That(definition.MovementSpeedMultiplier, Is.EqualTo(0.5f));
            Assert.That(definition.Effect, Is.EqualTo(effect));

            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/CON_MANA_POTION.asset");
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.UseMode, Is.EqualTo(ItemUseMode.Channeled));
            Assert.That(loot.ConsumableDefinition, Is.EqualTo(definition));
        }

        [Test]
        public void ManaFragmentAssets_AreConfiguredAccordingToDoc14()
        {
            var effect = AssetDatabase.LoadAssetAtPath<ManaConsumableEffect>(
                "Assets/Scriptable Objects/ManaFragmentEffect.asset");
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.ManaAmount, Is.EqualTo(10f));

            var definition = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(
                "Assets/Scriptable Objects/ManaFragmentDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.ConsumptionDurationSeconds, Is.EqualTo(1.0f));
            Assert.That(definition.MovementSpeedMultiplier, Is.EqualTo(1.0f));
            Assert.That(definition.Effect, Is.EqualTo(effect));

            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/CON_MANA_FRAGMENT.asset");
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.UseMode, Is.EqualTo(ItemUseMode.Instant));
            Assert.That(loot.ConsumableDefinition, Is.EqualTo(definition));
        }

        [Test]
        public void HealthPotionAssets_AreConfiguredAccordingToDoc14()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(
                "Assets/Scriptable Objects/HealthConsumableDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.ConsumptionDurationSeconds, Is.EqualTo(2.5f));
            Assert.That(definition.MovementSpeedMultiplier, Is.EqualTo(0.5f));

            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/CON_HEALTH_POTION.asset");
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.UseMode, Is.EqualTo(ItemUseMode.Channeled));
            Assert.That(loot.ConsumableDefinition, Is.EqualTo(definition));
        }
    }
}

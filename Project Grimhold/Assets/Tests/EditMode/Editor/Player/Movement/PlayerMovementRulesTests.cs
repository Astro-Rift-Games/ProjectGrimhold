using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Player.Movement
{
    public sealed class PlayerMovementRulesTests
    {
        private const string MovementControllerPath =
            "Assets/Scripts/Player/Movement/PlayerMovementNetworkController.cs";

        private const string StaminaControllerPath =
            "Assets/Scripts/Player/PlayerStaminaNetworkController.cs";

        private const string CombatControllerPath =
            "Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs";

        private const string ExtractionZonePath =
            "Assets/Scripts/Scenario/Extraction/ExtractionZone.cs";

        private const string ExtractionControllerPath =
            "Assets/Scripts/Player/Extraction/PlayerExtractionController.cs";

        [Test]
        public void PlayerControllers_UseTheRequiredSimulationExecutionOrder()
        {
            MonoScript movementScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(MovementControllerPath);
            MonoScript staminaScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(StaminaControllerPath);
            MonoScript combatScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(CombatControllerPath);
            MonoScript extractionZoneScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(ExtractionZonePath);
            MonoScript extractionControllerScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(ExtractionControllerPath);

            Assert.That(movementScript, Is.Not.Null);
            Assert.That(staminaScript, Is.Not.Null);
            Assert.That(combatScript, Is.Not.Null);
            Assert.That(extractionZoneScript, Is.Not.Null);
            Assert.That(extractionControllerScript, Is.Not.Null);
            Assert.That(MonoImporter.GetExecutionOrder(movementScript), Is.EqualTo(-10));
            Assert.That(
                MonoImporter.GetExecutionOrder(staminaScript),
                Is.LessThan(MonoImporter.GetExecutionOrder(movementScript)));
            Assert.That(MonoImporter.GetExecutionOrder(combatScript), Is.EqualTo(-9));
            Assert.That(MonoImporter.GetExecutionOrder(extractionZoneScript), Is.EqualTo(100));
            Assert.That(MonoImporter.GetExecutionOrder(extractionControllerScript), Is.EqualTo(110));
        }

        [Test]
        public void FusionConfiguration_RegistersPlayerControllerExecutionOrder()
        {
            NetworkProjectConfig config = NetworkProjectConfig.Global;

            Assert.That(config, Is.Not.Null);
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerStaminaNetworkController)),
                Is.LessThan(config.GetExecutionOrder(typeof(PlayerMovementNetworkController))));
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerMovementNetworkController)),
                Is.EqualTo(-10));
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerCombatNetworkController)),
                Is.EqualTo(-9));
            Assert.That(
                config.GetExecutionOrder(typeof(ExtractionZone)),
                Is.EqualTo(100));
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerExtractionController)),
                Is.EqualTo(110));
        }

        [Test]
        public void SprintIntent_RequiresHeldInputMovementIntentAndEnabledMovement()
        {
            var input = new PlayerNetworkInput
            {
                MoveDirection = Vector2.right
            };
            input.Buttons.Set(PlayerInputButton.Sprint, true);

            Assert.That(
                PlayerMovementNetworkController.ShouldSprint(
                    in input,
                    hasInput: true,
                    moveDirection: Vector2.right,
                    canMove: true),
                Is.True);
            Assert.That(
                PlayerMovementNetworkController.ShouldSprint(
                    in input,
                    hasInput: true,
                    moveDirection: Vector2.zero,
                    canMove: true),
                Is.False);
            Assert.That(
                PlayerMovementNetworkController.ShouldSprint(
                    in input,
                    hasInput: true,
                    moveDirection: Vector2.right,
                    canMove: false),
                Is.False);
        }
    }
}

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

        [Test]
        public void Facing_MovementWithoutContextualAction_IgnoresCursor()
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.up,
                Vector2.right * 10f);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.left);

            Assert.That(facing, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void Facing_IdleCursorMovement_PreservesPreviousFacing()
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.zero,
                Vector2.right * 10f);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.left);

            Assert.That(facing, Is.EqualTo(Vector2.left));
        }

        [TestCase(PlayerInputButton.PrimaryAttack, 1f, 0f, 1f, 0f)]
        [TestCase(PlayerInputButton.Interact, -1f, 0f, -1f, 0f)]
        public void Facing_ContextualActionWithValidCursor_OverridesMovement(
            PlayerInputButton button,
            float aimX,
            float aimY,
            float expectedX,
            float expectedY)
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.up,
                new Vector2(aimX, aimY) * 10f,
                button);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.left);

            Assert.That(facing, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void Facing_InvalidContextualCursor_FallsBackToValidMovement()
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.up,
                new Vector2(float.NaN, 0f),
                PlayerInputButton.PrimaryAttack);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.left);

            Assert.That(facing, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void Facing_ContextualCursorAtFinalPosition_FallsBackToValidMovement()
        {
            Vector2 finalPosition = new Vector2(3f, -2f);
            PlayerNetworkInput input = CreateInput(
                Vector2.up,
                finalPosition,
                PlayerInputButton.PrimaryAttack);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition,
                previousFacing: Vector2.left);

            Assert.That(facing, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void Facing_ContextualCursorAtFinalPositionWithoutMovement_PreservesPreviousFacing()
        {
            Vector2 finalPosition = new Vector2(3f, -2f);
            PlayerNetworkInput input = CreateInput(
                Vector2.zero,
                finalPosition,
                PlayerInputButton.Interact);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition,
                previousFacing: Vector2.down);

            Assert.That(facing, Is.EqualTo(Vector2.down));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0.009f, 0f)]
        public void Facing_InvalidContextualCursorWithoutMovement_PreservesPreviousFacing(
            float aimX,
            float aimY)
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.zero,
                new Vector2(aimX, aimY),
                PlayerInputButton.Interact);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.down);

            Assert.That(facing, Is.EqualTo(Vector2.down));
        }

        [Test]
        public void Facing_NewMovementAfterContextualAction_RegainsPriority()
        {
            PlayerNetworkInput attackInput = CreateInput(
                Vector2.up,
                Vector2.right * 10f,
                PlayerInputButton.PrimaryAttack);
            Vector2 contextualFacing = ResolveFacing(
                in attackInput,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.down);
            PlayerNetworkInput movementInput = CreateInput(
                Vector2.left,
                Vector2.right * 10f);

            Vector2 facing = ResolveFacing(
                in movementInput,
                finalPosition: Vector2.zero,
                previousFacing: contextualFacing);

            Assert.That(facing, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void Facing_IdleTickAfterContextualAction_PreservesContextualFacing()
        {
            PlayerNetworkInput attackInput = CreateInput(
                Vector2.up,
                Vector2.right * 10f,
                PlayerInputButton.PrimaryAttack);
            Vector2 contextualFacing = ResolveFacing(
                in attackInput,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.down);
            PlayerNetworkInput idleInput = CreateInput(
                Vector2.zero,
                Vector2.left * 10f);

            Vector2 facing = ResolveFacing(
                in idleInput,
                finalPosition: Vector2.zero,
                previousFacing: contextualFacing);

            Assert.That(facing, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void Facing_WorldZeroCursorWithUsableDirection_RemainsValid()
        {
            PlayerNetworkInput input = CreateInput(
                Vector2.up,
                Vector2.zero,
                PlayerInputButton.Interact);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.right,
                previousFacing: Vector2.down);

            Assert.That(facing, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void Facing_DiagonalMovement_NormalizesDirection()
        {
            PlayerNetworkInput input = CreateInput(
                new Vector2(1f, 1f),
                Vector2.left * 10f);

            Vector2 facing = ResolveFacing(
                in input,
                finalPosition: Vector2.zero,
                previousFacing: Vector2.down);

            Assert.That(facing, Is.EqualTo(new Vector2(1f, 1f).normalized));
        }

        private static PlayerNetworkInput CreateInput(
            Vector2 moveDirection,
            Vector2 aimWorldPosition,
            PlayerInputButton? button = null)
        {
            var input = new PlayerNetworkInput
            {
                MoveDirection = moveDirection,
                AimWorldPosition = aimWorldPosition
            };

            if (button.HasValue)
            {
                input.Buttons.Set(button.Value, true);
            }

            return input;
        }

        private static Vector2 ResolveFacing(
            in PlayerNetworkInput input,
            Vector2 finalPosition,
            Vector2 previousFacing)
        {
            return PlayerMovementNetworkController.ResolveFacingDirection(
                in input,
                Vector2.ClampMagnitude(input.MoveDirection, 1f),
                finalPosition,
                previousFacing);
        }
    }
}

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

        private const string CombatControllerPath =
            "Assets/Scripts/Player/Combat/PlayerCombatNetworkController.cs";

        [Test]
        public void PlayerControllers_UseTheRequiredSimulationExecutionOrder()
        {
            MonoScript movementScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(MovementControllerPath);
            MonoScript combatScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(CombatControllerPath);

            Assert.That(movementScript, Is.Not.Null);
            Assert.That(combatScript, Is.Not.Null);
            Assert.That(MonoImporter.GetExecutionOrder(movementScript), Is.EqualTo(-10));
            Assert.That(MonoImporter.GetExecutionOrder(combatScript), Is.EqualTo(-9));
        }

        [Test]
        public void FusionConfiguration_RegistersPlayerControllerExecutionOrder()
        {
            NetworkProjectConfig config = NetworkProjectConfig.Global;

            Assert.That(config, Is.Not.Null);
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerMovementNetworkController)),
                Is.EqualTo(-10));
            Assert.That(
                config.GetExecutionOrder(typeof(PlayerCombatNetworkController)),
                Is.EqualTo(-9));
        }
    }
}

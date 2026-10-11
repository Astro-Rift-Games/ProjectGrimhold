using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Player.Movement
{
    public sealed class ArcaneProjectileMovementTests
    {
        [Test]
        public void NoAbilityModifier_PreservesExistingWalkingAndSprintSpeeds()
        {
            Assert.That(ResolveSpeed(false, false, 1f), Is.EqualTo(5f));
            Assert.That(ResolveSpeed(true, false, 1f), Is.EqualTo(7.5f));
        }

        [Test]
        public void PreparingArcane_ReducesVoluntaryWalkingAndSprintWithoutChangingBaseSpeed()
        {
            Assert.That(ResolveSpeed(false, false, 0.4f), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(ResolveSpeed(true, false, 0.4f), Is.EqualTo(3f).Within(0.0001f));
            Assert.That(ResolveSpeed(false, false, 1f), Is.EqualTo(5f), "Ending preparation restores the unmodified base calculation.");
        }

        [Test]
        public void Downed_StillUsesItsExistingMultiplierAndNeverSprints()
        {
            Assert.That(ResolveSpeed(false, true, 1f), Is.EqualTo(1.75f).Within(0.0001f));
            Assert.That(ResolveSpeed(true, true, 1f), Is.EqualTo(1.75f).Within(0.0001f));
        }

        [Test]
        public void ExistingNonArcaneBehaviour_DefaultsToNoVoluntarySpeedReduction()
        {
            MethodInfo method = typeof(AbilityExecutionBehaviour).GetMethod("GetVoluntaryMovementSpeedMultiplier");
            Assert.That(method, Is.Not.Null,
                "Other abilities need a default multiplier of 1, rather than slowing every Preparing execution.");
            var caster = new GameObject("ExistingBehaviour");
            try
            {
                var behaviour = caster.AddComponent<SeismicStrikeAbilityBehaviour>();
                var snapshot = new AbilityExecutionSnapshot { Phase = AbilityExecutionPhase.Preparing, Sequence = 1 };
                Assert.That((float)method.Invoke(behaviour, new object[] { snapshot }), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(caster);
            }
        }

        private static float ResolveSpeed(bool sprinting, bool downed, float abilityMultiplier)
        {
            MethodInfo method = typeof(PlayerMovementNetworkController).GetMethod("ResolveVoluntarySpeed",
                BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(float), typeof(bool), typeof(float), typeof(bool), typeof(float), typeof(float) }, null);
            Assert.That(method, Is.Not.Null,
                "Append an optional abilitySpeedMultiplier = 1f to ResolveVoluntarySpeed; knockback/forced steps stay separate.");
            ParameterInfo parameter = method.GetParameters()[5];
            Assert.That(parameter.IsOptional, Is.True, "Preserve the existing five-argument callers.");
            Assert.That(parameter.DefaultValue, Is.EqualTo(1f));
            return (float)method.Invoke(null, new object[] { 5f, sprinting, 1.5f, downed, 0.35f, abilityMultiplier });
        }
    }
}

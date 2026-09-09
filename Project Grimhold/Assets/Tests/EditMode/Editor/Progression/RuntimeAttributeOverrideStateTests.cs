using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class RuntimeAttributeOverrideStateTests
    {
        [TestCase(CharacterAttribute.Vitality)]
        [TestCase(CharacterAttribute.Resistance)]
        [TestCase(CharacterAttribute.Strength)]
        [TestCase(CharacterAttribute.Dexterity)]
        [TestCase(CharacterAttribute.Intelligence)]
        [TestCase(CharacterAttribute.Luck)]
        public void EveryAttribute_CanBeAdjustedIndependently(CharacterAttribute attribute)
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5, availablePoints: 4);
            var overrides = new RuntimeAttributeOverrideState(0, 0, 0, 0, 0, 0);

            Assert.That(overrides.TryAdjust(attribute, 1, persistent, out var adjusted), Is.True);
            Assert.That(adjusted.TryApply(persistent, out CharacterAttributeState effective), Is.True);
            Assert.That(effective.TryGetValue(attribute, out int value), Is.True);
            Assert.That(value, Is.EqualTo(6));
            Assert.That(effective.AvailablePoints, Is.EqualTo(4));
        }

        [Test]
        public void StrengthAdjustment_ComposesOverPersistentStateWithoutChangingAvailablePoints()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5, availablePoints: 7);
            var overrides = new RuntimeAttributeOverrideState(0, 0, 0, 0, 0, 0);

            Assert.That(
                overrides.TryAdjust(
                    CharacterAttribute.Strength,
                    10,
                    persistent,
                    out RuntimeAttributeOverrideState adjusted),
                Is.True);
            Assert.That(adjusted.TryApply(persistent, out CharacterAttributeState effective), Is.True);

            Assert.That(persistent.Strength, Is.EqualTo(5));
            Assert.That(effective.Strength, Is.EqualTo(15));
            Assert.That(effective.AvailablePoints, Is.EqualTo(7));
        }

        [Test]
        public void NegativeAdjustment_ClampsEffectiveValueAtZero()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5);
            var overrides = new RuntimeAttributeOverrideState(0, 0, 0, 0, 0, 0);

            Assert.That(
                overrides.TryAdjust(
                    CharacterAttribute.Strength,
                    -10,
                    persistent,
                    out RuntimeAttributeOverrideState adjusted),
                Is.True);
            Assert.That(adjusted.TryApply(persistent, out CharacterAttributeState effective), Is.True);
            Assert.That(effective.Strength, Is.Zero);
        }

        [Test]
        public void Reset_RestoresPersistentValueImmediately()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5);
            var overrides = new RuntimeAttributeOverrideState(0, 0, 10, 0, 0, 0);

            RuntimeAttributeOverrideState reset = overrides.Reset(CharacterAttribute.Strength);

            Assert.That(reset.TryApply(persistent, out CharacterAttributeState effective), Is.True);
            Assert.That(effective, Is.EqualTo(persistent));
        }

        [Test]
        public void ResetAll_RestoresEveryPersistentValue()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5, availablePoints: 9);
            var overrides = new RuntimeAttributeOverrideState(1, -2, 10, 4, -3, 5);

            RuntimeAttributeOverrideState reset = overrides.ResetAll();

            Assert.That(reset.TryApply(persistent, out CharacterAttributeState effective), Is.True);
            Assert.That(effective, Is.EqualTo(persistent));
        }

        [Test]
        public void EffectiveSnapshot_IsSharedByWeaponRequirementsAndScaling()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5);
            var overrides = new RuntimeAttributeOverrideState(0, 0, 10, 0, 0, 0);
            var requirements = new WeaponAttributeRequirements(
                minimumStrength: 10,
                minimumDexterity: 0,
                minimumIntelligence: 0);
            var scaling = new WeaponOffensiveScaling(CharacterAttribute.Strength, 0.7f);

            Assert.That(overrides.TryApply(persistent, out CharacterAttributeState effective), Is.True);
            Assert.That(requirements.IsSatisfiedBy(effective), Is.True);
            Assert.That(scaling.TryResolveAttributeValue(effective, out int scalingValue), Is.True);
            Assert.That(scalingValue, Is.EqualTo(15));
            Assert.That(WeaponDamageCalculator.Calculate(30f, scalingValue, scaling.Coefficient),
                Is.EqualTo(40.5f).Within(0.0001f));
        }

        [Test]
        public void RuntimeSession_CarriesTownConfigurationWithoutMutatingPersistentState()
        {
            CharacterAttributeState persistent = CreatePersistent(strength: 5, availablePoints: 7);
            var session = new RuntimeAttributeOverrideSession();

            Assert.That(session.TryAdjust(CharacterAttribute.Strength, 5, persistent), Is.True);
            Assert.That(session.TryAdjust(CharacterAttribute.Strength, 5, persistent), Is.True);
            Assert.That(session.TryGetEffectiveState(persistent, out CharacterAttributeState effective), Is.True);

            Assert.That(effective.Strength, Is.EqualTo(15));
            Assert.That(effective.AvailablePoints, Is.EqualTo(7));
            Assert.That(persistent.Strength, Is.EqualTo(5));
        }

        private static CharacterAttributeState CreatePersistent(
            int strength,
            int availablePoints = 0)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    5, 5, strength, 5, 5, 5, availablePoints,
                    out CharacterAttributeState state),
                Is.True);
            return state;
        }
    }
}

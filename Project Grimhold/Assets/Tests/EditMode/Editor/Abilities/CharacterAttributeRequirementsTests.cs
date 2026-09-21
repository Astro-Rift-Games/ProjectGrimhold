using NUnit.Framework;

public sealed class CharacterAttributeRequirementsTests
{
    [Test]
    public void IsSatisfiedBy_EvaluatesEveryConfiguredMinimum()
    {
        var requirements = new CharacterAttributeRequirements(
            new CharacterAttributeRequirement(CharacterAttribute.Vitality, 7),
            new CharacterAttributeRequirement(CharacterAttribute.Intelligence, 7));

        CharacterAttributeState eligible = AbilityTestFactory.CreateAttributes(vitality: 7, intelligence: 8);
        CharacterAttributeState ineligible = AbilityTestFactory.CreateAttributes(vitality: 7, intelligence: 6);

        Assert.That(requirements.IsSatisfiedBy(eligible), Is.True);
        Assert.That(requirements.IsSatisfiedBy(ineligible), Is.False);
    }

    [Test]
    public void EmptyRequirements_AcceptEveryStructurallyValidDistribution()
    {
        var requirements = new CharacterAttributeRequirements();
        CharacterAttributeState attributes = AbilityTestFactory.CreateAttributes();

        Assert.That(requirements.TryValidate(out string error), Is.True, error);
        Assert.That(requirements.IsSatisfiedBy(attributes), Is.True);
    }

    [Test]
    public void DuplicateAttribute_IsInvalidAndNeverEligible()
    {
        var requirements = new CharacterAttributeRequirements(
            new CharacterAttributeRequirement(CharacterAttribute.Strength, 5),
            new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));

        Assert.That(requirements.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("duplicate attribute"));
        Assert.That(requirements.IsSatisfiedBy(AbilityTestFactory.CreateAttributes(strength: 20)), Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveMinimum_IsInvalid(int minimum)
    {
        var requirements = new CharacterAttributeRequirements(
            new CharacterAttributeRequirement(CharacterAttribute.Dexterity, minimum));

        Assert.That(requirements.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("must be positive"));
    }
}

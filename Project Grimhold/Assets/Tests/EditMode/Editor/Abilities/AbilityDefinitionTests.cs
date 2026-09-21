using NUnit.Framework;
using UnityEngine;

public sealed class AbilityDefinitionTests
{
    private AbilityDefinition _definition;

    [TearDown]
    public void TearDown()
    {
        if (_definition != null)
        {
            Object.DestroyImmediate(_definition);
        }
    }

    [Test]
    public void ValidStaticConfiguration_PassesValidationAndExposesIdentity()
    {
        _definition = AbilityTestFactory.CreateDefinition(
            "purification",
            AbilityResourceType.Mana,
            30,
            20f,
            new CharacterAttributeRequirement(CharacterAttribute.Vitality, 7),
            new CharacterAttributeRequirement(CharacterAttribute.Resistance, 7));

        Assert.That(_definition.TryValidate(out string error), Is.True, error);
        Assert.That(_definition.AbilityId, Is.EqualTo(new AbilityId("purification")));
        Assert.That(_definition.Resource, Is.EqualTo(AbilityResourceType.Mana));
        Assert.That(_definition.Cost, Is.EqualTo(30));
        Assert.That(_definition.CooldownSeconds, Is.EqualTo(20f));
    }

    [TestCase("Invalid", AbilityResourceType.Mana, 10, 5f)]
    [TestCase("invalid_resource", AbilityResourceType.None, 10, 5f)]
    [TestCase("invalid_cost", AbilityResourceType.Mana, 0, 5f)]
    [TestCase("invalid_cooldown", AbilityResourceType.Mana, 10, 0f)]
    public void InvalidStaticConfiguration_FailsValidation(
        string id,
        AbilityResourceType resource,
        int cost,
        float cooldownSeconds)
    {
        _definition = AbilityTestFactory.CreateDefinition(id, resource, cost, cooldownSeconds);

        Assert.That(_definition.TryValidate(out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void AttributeRequirements_UseConfirmedCharacterState()
    {
        _definition = AbilityTestFactory.CreateDefinition(
            "restoration",
            AbilityResourceType.Mana,
            25,
            15f,
            new CharacterAttributeRequirement(CharacterAttribute.Vitality, 10));

        Assert.That(
            _definition.AreAttributeRequirementsSatisfiedBy(
                AbilityTestFactory.CreateAttributes(vitality: 10)),
            Is.True);
        Assert.That(
            _definition.AreAttributeRequirementsSatisfiedBy(
                AbilityTestFactory.CreateAttributes(vitality: 9)),
            Is.False);
    }
}

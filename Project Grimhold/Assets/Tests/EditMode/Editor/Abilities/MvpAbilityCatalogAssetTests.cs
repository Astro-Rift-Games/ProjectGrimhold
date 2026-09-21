using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class MvpAbilityCatalogAssetTests
{
    private const string CatalogPath =
        "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset";

    private static IEnumerable<TestCaseData> MvpDefinitions()
    {
        yield return Definition("charge", CharacterAttribute.Strength, 10, AbilityResourceType.Stamina, 20, 8f);
        yield return Definition("seismic_strike", CharacterAttribute.Strength, 15, AbilityResourceType.Stamina, 25, 10f);
        yield return Definition("trap", CharacterAttribute.Dexterity, 10, AbilityResourceType.Stamina, 15, 6f);
        yield return Definition("empower", CharacterAttribute.Luck, 10, AbilityResourceType.Mana, 20, 20f);
        yield return Definition("arcane_projectile", CharacterAttribute.Intelligence, 10, AbilityResourceType.Mana, 15, 5f);
        yield return Definition("protective_orbs", CharacterAttribute.Resistance, 10, AbilityResourceType.Mana, 30, 25f);
        yield return Definition("restoration", CharacterAttribute.Vitality, 10, AbilityResourceType.Mana, 25, 15f);
    }

    private static TestCaseData Definition(
        string id,
        CharacterAttribute attribute,
        int minimum,
        AbilityResourceType resource,
        int cost,
        float cooldown) =>
        new TestCaseData(id, attribute, minimum, resource, cost, cooldown).SetName($"MvpDefinition_{id}_MatchesGameDesign");

    [Test]
    public void CatalogAsset_ContainsExactlyNineValidDefinitions()
    {
        AbilityDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(CatalogPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryValidate(out string error), Is.True, error);
        Assert.That(catalog.DefinitionCount, Is.EqualTo(9));
    }

    [TestCaseSource(nameof(MvpDefinitions))]
    public void SingleRequirementDefinition_MatchesGameDesign(
        string id,
        CharacterAttribute attribute,
        int minimum,
        AbilityResourceType resource,
        int cost,
        float cooldown)
    {
        AbilityDefinition definition = Resolve(id);

        Assert.That(definition.Resource, Is.EqualTo(resource));
        Assert.That(definition.Cost, Is.EqualTo(cost));
        Assert.That(definition.CooldownSeconds, Is.EqualTo(cooldown));
        Assert.That(definition.AttributeRequirements.Requirements, Has.Count.EqualTo(1));
        AssertRequirement(definition.AttributeRequirements.Requirements[0], attribute, minimum);
    }

    [TestCase("purification", CharacterAttribute.Vitality, 7, CharacterAttribute.Resistance, 7, 30, 20f)]
    [TestCase("life_drain", CharacterAttribute.Vitality, 7, CharacterAttribute.Intelligence, 7, 35, 20f)]
    public void DualRequirementDefinition_MatchesGameDesign(
        string id,
        CharacterAttribute firstAttribute,
        int firstMinimum,
        CharacterAttribute secondAttribute,
        int secondMinimum,
        int cost,
        float cooldown)
    {
        AbilityDefinition definition = Resolve(id);

        Assert.That(definition.Resource, Is.EqualTo(AbilityResourceType.Mana));
        Assert.That(definition.Cost, Is.EqualTo(cost));
        Assert.That(definition.CooldownSeconds, Is.EqualTo(cooldown));
        Assert.That(definition.AttributeRequirements.Requirements, Has.Count.EqualTo(2));
        AssertRequirement(definition.AttributeRequirements.Requirements[0], firstAttribute, firstMinimum);
        AssertRequirement(definition.AttributeRequirements.Requirements[1], secondAttribute, secondMinimum);
    }

    private static AbilityDefinition Resolve(string id)
    {
        AbilityDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGet(new AbilityId(id), out AbilityDefinition definition), Is.True);
        return definition;
    }

    private static void AssertRequirement(
        CharacterAttributeRequirement requirement,
        CharacterAttribute attribute,
        int minimum)
    {
        Assert.That(requirement.Attribute, Is.EqualTo(attribute));
        Assert.That(requirement.MinimumValue, Is.EqualTo(minimum));
    }
}

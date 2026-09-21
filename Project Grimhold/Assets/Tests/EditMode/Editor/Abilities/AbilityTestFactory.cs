using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

internal static class AbilityTestFactory
{
    public static AbilityDefinition CreateDefinition(
        string id = "test_ability",
        AbilityResourceType resource = AbilityResourceType.Mana,
        int cost = 10,
        float cooldownSeconds = 5f,
        params CharacterAttributeRequirement[] requirements)
    {
        var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
        definition.name = id ?? "InvalidAbilityDefinition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SetPrivateField(definition, "_id", id);
        SetPrivateField(
            definition,
            "_attributeRequirements",
            new CharacterAttributeRequirements(requirements ?? new CharacterAttributeRequirement[0]));
        SetPrivateField(definition, "_resource", resource);
        SetPrivateField(definition, "_cost", cost);
        SetPrivateField(definition, "_cooldownSeconds", cooldownSeconds);
        return definition;
    }

    public static AbilityDefinitionCatalog CreateCatalog(params AbilityDefinition[] definitions)
    {
        var catalog = ScriptableObject.CreateInstance<AbilityDefinitionCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        SetPrivateField(catalog, "_definitions", new List<AbilityDefinition>(definitions));
        SetPrivateField(catalog, "_isCacheDirty", true);
        return catalog;
    }

    public static CharacterAttributeState CreateAttributes(
        int vitality = 0,
        int resistance = 0,
        int strength = 0,
        int dexterity = 0,
        int intelligence = 0,
        int luck = 0)
    {
        bool created = CharacterAttributeState.TryCreate(
            vitality,
            resistance,
            strength,
            dexterity,
            intelligence,
            luck,
            0,
            out CharacterAttributeState state);
        Assert.That(created, Is.True);
        return state;
    }

    public static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}

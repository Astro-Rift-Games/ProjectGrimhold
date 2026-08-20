#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Builds Equipment loot definitions and a catalog entirely in memory so tests never depend on
/// production armor content, which does not exist yet.
/// </summary>
public static class EquipmentTestContent
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly List<UnityEngine.Object> Created = new();

    public static LootDefinition CreateArmorDefinition(string id, LootCategory category)
    {
        LootDefinition definition = ScriptableObject.CreateInstance<LootDefinition>();
        Created.Add(definition);
        SetField(definition, "_id", id);
        SetField(definition, "_displayName", id);
        SetField(definition, "_category", category);
        SetField(definition, "_rarity", LootRarity.Common);
        SetField(definition, "_extractionValuePerUnit", 1);
        SetField(definition, "_sellValuePerUnit", 1);
        SetField(definition, "_defaultPickupQuantity", 1);
        SetField(definition, "_worldSprite", CreateSprite());
        SetField(definition, "_icon", CreateSprite());
        return definition;
    }

    public static LootDefinition CreateNonEquippableDefinition(string id) =>
        CreateArmorDefinition(id, LootCategory.Miscellaneous);

    /// <summary>
    /// Produces a catalog containing the supplied definitions. Network indices stay deterministic
    /// because the catalog sorts ids ordinally on its own.
    /// </summary>
    public static LootDefinitionCatalog CreateCatalog(params LootDefinition[] definitions)
    {
        LootDefinitionCatalog catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        Created.Add(catalog);
        SetField(catalog, "_definitions", new List<LootDefinition>(definitions));
        return catalog;
    }

    /// <summary>Destroys everything created since the last cleanup.</summary>
    public static void Cleanup()
    {
        for (int index = 0; index < Created.Count; index++)
        {
            if (Created[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(Created[index]);
            }
        }

        Created.Clear();
    }

    public static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' no longer exists on {target.GetType().Name}.");
        }

        field.SetValue(target, value);
    }

    private static Sprite CreateSprite()
    {
        var texture = new Texture2D(4, 4);
        Created.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
        Created.Add(sprite);
        return sprite;
    }
}
#endif

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LootInventoryValueCalculatorTests
{
    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < _createdObjects.Count; index++)
        {
            Object.DestroyImmediate(_createdObjects[index]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void Calculate_DerivesValueOutsideInventorySource()
    {
        LootDefinitionCatalog catalog = CreateCatalog(("bone", 7), ("potion", 11));
        var content = new[]
        {
            new LootEntry(new LootId("bone"), 3),
            new LootEntry(new LootId("potion"), 2)
        };

        Assert.That(LootInventoryValueCalculator.TryCalculate(content, catalog, out long total), Is.True);
        Assert.That(total, Is.EqualTo(43));
    }

    [Test]
    public void Calculate_MissingDefinitionOrOverflow_IsUnavailable()
    {
        LootDefinitionCatalog incompleteCatalog = CreateCatalog(("known", 1));
        Assert.That(
            LootInventoryValueCalculator.TryCalculate(
                new[] { new LootEntry(new LootId("missing"), 1) },
                incompleteCatalog,
                out long missingTotal),
            Is.False);
        Assert.That(missingTotal, Is.Zero);

        var definitions = new (string id, int value)[16];
        var content = new LootEntry[16];
        for (int index = 0; index < definitions.Length; index++)
        {
            definitions[index] = ($"item_{index}", int.MaxValue);
            content[index] = new LootEntry(new LootId($"item_{index}"), int.MaxValue);
        }

        LootDefinitionCatalog overflowCatalog = CreateCatalog(definitions);
        Assert.That(LootInventoryValueCalculator.TryCalculate(content, overflowCatalog, out long overflowTotal), Is.False);
        Assert.That(overflowTotal, Is.Zero);
    }

    private LootDefinitionCatalog CreateCatalog(params (string id, int value)[] entries)
    {
        var catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        _createdObjects.Add(catalog);
        var definitions = new List<LootDefinition>(entries.Length);
        for (int index = 0; index < entries.Length; index++)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            _createdObjects.Add(definition);
            SetField(definition, "_id", entries[index].id);
            SetField(definition, "_sellValuePerUnit", entries[index].value);
            definitions.Add(definition);
        }

        SetField(catalog, "_definitions", definitions);
        return catalog;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
#endif

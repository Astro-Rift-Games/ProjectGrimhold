#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

/// <summary>TASK-283 T8: provisional Dungeon weapon-variant generation and extraction RPC bound.</summary>
public sealed class WeaponInstanceGenerationDefaultsTests
{
    private const string CatalogPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
    private static readonly string[] Gems = { "RES_ATTUNEMENT_GEM", "RES_RUBY_GEM", "RES_SAPPHIRE_GEM" };

    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
        Assert.That(_catalog, Is.Not.Null);
    }

    private List<int> WeaponIndices()
    {
        var indices = new List<int>();
        for (int index = 0; index < _catalog.DefinitionCount; index++)
        {
            if (_catalog.TryGetByIndex(index, out LootDefinition definition) &&
                definition.Category == LootCategory.Weapon)
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    [Test]
    public void Rules_AreValidForTheProductionCatalog()
    {
        Assert.That(WeaponInstanceGenerationDefaults.TryGetRules(_catalog, out WeaponInstanceGenerationRules rules), Is.True);
        Assert.That(rules.TryValidate(out string error), Is.True, error);
        Assert.That(rules.GemPoolCount, Is.EqualTo(3));
    }

    [Test]
    public void Units_AreDeterministicLegalAndMixPlainAndVariants()
    {
        Assert.That(WeaponInstanceGenerationDefaults.TryGetRules(_catalog, out WeaponInstanceGenerationRules rules), Is.True);
        int plain = 0;
        int variants = 0;
        foreach (int index in WeaponIndices())
        {
            _catalog.TryGetByIndex(index, out LootDefinition definition);
            for (ulong seed = 1; seed <= 300; seed++)
            {
                Assert.That(WeaponInstanceGenerationDefaults.TryGenerateUnit(
                    _catalog, rules, index, seed, 0, out WeaponInstanceModifiersNetworkData first, out string error), Is.True, error);
                Assert.That(WeaponInstanceGenerationDefaults.TryGenerateUnit(
                    _catalog, rules, index, seed, 0, out WeaponInstanceModifiersNetworkData again, out error), Is.True, error);
                Assert.That(again, Is.EqualTo(first), "The same seed and unit must reproduce the variant.");

                if (first.IsEmpty)
                {
                    plain++;
                    continue;
                }

                variants++;
                Assert.That(first.TryResolve(_catalog, definition.WeaponDefinition.NaturalScalingAttribute,
                    out WeaponInstanceModifiers modifiers, out error), Is.True, error);
                for (int socket = 0; socket < modifiers.GemSocketCount; socket++)
                {
                    if (modifiers.TryGetGemSocket(socket, out WeaponGemSocket gem) && gem.IsOccupied)
                    {
                        Assert.That(Gems, Contains.Item(gem.GemLootId.Value));
                    }
                }
            }
        }

        Assert.That(plain, Is.GreaterThan(0), "Some weapons stay plain.");
        Assert.That(variants, Is.GreaterThan(0), "Some weapons roll a variant.");
    }

    [Test]
    public void UnitsOfOneStack_RollIndependently()
    {
        Assert.That(WeaponInstanceGenerationDefaults.TryGetRules(_catalog, out WeaponInstanceGenerationRules rules), Is.True);
        int index = WeaponIndices()[0];
        var seen = new HashSet<int>();
        for (int unit = 0; unit < 40; unit++)
        {
            Assert.That(WeaponInstanceGenerationDefaults.TryGenerateUnit(
                _catalog, rules, index, 7, unit, out WeaponInstanceModifiersNetworkData data, out string error), Is.True, error);
            seen.Add(data.PackedValue);
        }

        Assert.That(seen.Count, Is.GreaterThan(1));
    }

    [Test]
    public void ExtractionRpc_WorstCasePayloadStaysBelowTheRpcLimit()
    {
        // Aggregate entries: one per non-weapon type plus one per weapon template; modified units: one
        // per backpack slot. Every modified unit and every non-weapon type occupies a distinct slot.
        int weaponTemplates = WeaponIndices().Count;
        int slots = PlayerLootReceiver.PhysicalSlotCount;
        int worstElements = 2 * (slots + weaponTemplates);
        int bytes = worstElements * sizeof(int) + 4 * sizeof(int) + 13 * sizeof(int);
        Assert.That(bytes, Is.LessThan(480),
            $"Worst-case extraction RPC payload is {bytes} bytes; keep headroom under Fusion's 512-byte limit.");
    }
}
#endif

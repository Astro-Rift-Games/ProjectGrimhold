#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class EquipmentVisualDefinitionTests
    {
        private const string GlovesDefinitionPath =
            "Assets/Scriptable Objects/Loot/Definitions/LightArmorGloves.asset";

        [Test]
        public void ResolveSprite_BaseSpriteReloadedAsNewManagedObject_ReturnsMappedEquipmentSprite()
        {
            LootDefinition gloves = AssetDatabase.LoadAssetAtPath<LootDefinition>(GlovesDefinitionPath);
            Assert.That(gloves, Is.Not.Null);
            EquipmentVisualDefinition visual = gloves.EquipmentVisualDefinition;
            EquipmentSpriteMapping mapping = GetMappings(visual)[0];

            // Unloading and reloading yields a different managed wrapper for the same native sprite,
            // as the animated base renderer can hold after Play Mode sessions without a domain reload.
            Sprite reloadedBase = ReloadSprite(mapping.BaseSprite);
            Assert.That(reloadedBase, Is.Not.SameAs(mapping.BaseSprite));
            Assert.That(reloadedBase == mapping.BaseSprite, Is.True);

            Assert.That(visual.ResolveSprite(reloadedBase), Is.EqualTo(mapping.EquipmentSprite));
        }

        [Test]
        public void ResolveSprite_UnmappedBaseSprite_ReturnsNull()
        {
            LootDefinition gloves = AssetDatabase.LoadAssetAtPath<LootDefinition>(GlovesDefinitionPath);
            Assert.That(gloves, Is.Not.Null);
            Sprite unmapped = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);

            try
            {
                Assert.That(gloves.EquipmentVisualDefinition.ResolveSprite(unmapped), Is.Null);
                Assert.That(gloves.EquipmentVisualDefinition.ResolveSprite(null), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(unmapped.texture);
                Object.DestroyImmediate(unmapped);
            }
        }

        private static Sprite ReloadSprite(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            string spriteName = sprite.name;
            Resources.UnloadAsset(sprite);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite candidate && candidate.name == spriteName)
                {
                    return candidate;
                }
            }

            Assert.Fail($"Sprite '{spriteName}' was not found at '{path}'.");
            return null;
        }

        private static List<EquipmentSpriteMapping> GetMappings(EquipmentVisualDefinition visual)
        {
            FieldInfo field = typeof(EquipmentVisualDefinition).GetField(
                "_spriteMappings", BindingFlags.Instance | BindingFlags.NonPublic);
            var mappings = (List<EquipmentSpriteMapping>)field.GetValue(visual);
            Assert.That(mappings, Is.Not.Empty);
            return mappings;
        }
    }
}
#endif

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class ConsumableUtilitySpriteAssetsTests
    {
        [TestCase("CON_HEALTH_POTION", "HealthPotion")]
        [TestCase("CON_MANA_POTION", "ManaPotion")]
        [TestCase("CON_MANA_FRAGMENT", "ManaFragment")]
        [TestCase("CON_EXPLOSIVE_BOMB", "ExplosiveBomb")]
        [TestCase("CON_SMOKE_BOMB", "SmokeBomb")]
        [TestCase("CON_THROWING_DAGGER", "ThrowingDagger")]
        [TestCase("CON_LOCKPICK", "Lockpick")]
        [TestCase("CON_SELF_REVIVE", "SelfRevive")]
        [TestCase("UTL_ARCANE_DETECTOR", "ArcaneDetector")]
        [TestCase("UTL_RUNE_COMPASS", "RuneCompass")]
        public void Definition_UsesExactSpritesAndRetainsCatalogValidation(string id, string family)
        {
            var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{id}.asset");
            Assert.That(definition, Is.Not.Null, id);

            string directory = $"Assets/Art/Consumables & Utilities/{family}/{family}";
            Sprite world = AssertSprite($"{directory}/{id}-Base.png");
            Sprite icon = AssertSprite($"{directory}/{id}-Icon.png");
            AssertSprite($"{directory}/{id}-Interactable.png");
            Assert.That(definition.WorldSprite, Is.EqualTo(world), id);
            Assert.That(definition.Icon, Is.SameAs(icon), id);
            Assert.That(definition.TryValidate(out string error), Is.True, error);

            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(id, out LootDefinition registered), Is.True, id);
            Assert.That(registered, Is.SameAs(definition), id);
            Assert.That(catalog.TryValidate(out string catalogError), Is.True, catalogError);
        }

        private static Sprite AssertSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 16, 16)), path);
            Assert.That(sprite.texture.width, Is.EqualTo(16), path);
            Assert.That(sprite.texture.height, Is.EqualTo(16), path);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16), path);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(8, 8)), path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16), path);
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
            return sprite;
        }
    }
}

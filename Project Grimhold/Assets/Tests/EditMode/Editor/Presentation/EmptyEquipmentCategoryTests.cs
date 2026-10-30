#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class EmptyEquipmentCategoryTests
    {
        private const string Art = "Assets/Art/UI/Charcoal/EquipmentCategories/equipment-categories.png";
        private readonly List<Object> _owned = new();
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear();
        }

        [Test]
        public void SixCategoryGlyphsHaveExactPixelImportAndDistinctCompositeWeaponSymbols()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().ToArray();
            Assert.That(sprites.Select(s => s.name), Is.EquivalentTo(new[]
                { "main_hand", "off_hand", "helmet", "armor", "gloves", "boots" }));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
            foreach (Sprite sprite in sprites)
            {
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
                Assert.That(sprite.border, Is.EqualTo(Vector4.zero));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
            }
            Assert.That(sprites.Single(s => s.name == "main_hand").rect,
                Is.Not.EqualTo(sprites.Single(s => s.name == "off_hand").rect));
            var pixels = new Texture2D(2, 2);
            _owned.Add(pixels);
            Assert.That(pixels.LoadImage(File.ReadAllBytes(Art)), Is.True);
            foreach (Color32 pixel in pixels.GetPixels32())
            {
                Assert.That(pixel.a == 0 || pixel.Equals(new Color32(73, 84, 95, 255)) ||
                    pixel.Equals(new Color32(104, 116, 128, 255)) || pixel.Equals(new Color32(132, 143, 153, 255)), Is.True,
                    "Glyphs use only opaque charcoal-palette ink or real transparency.");
            }
            foreach (int origin in new[] { 0, 24 })
            {
                var ink = pixels.GetPixels(origin, 0, 16, 16).Select(p => (Color32)p).ToArray();
                Assert.That(ink.Contains(new Color32(73, 84, 95, 255)), Is.True, "The rear object has a distinct dark neutral plane.");
                Assert.That(ink.Contains(new Color32(104, 116, 128, 255)), Is.True);
                Assert.That(ink.Contains(new Color32(132, 143, 153, 255)), Is.True, "The foreground object stays legible at native size.");
            }
            Assert.That(pixels.GetPixels(0, 0, 16, 16), Is.Not.EqualTo(pixels.GetPixels(24, 0, 16, 16)));
        }

        [TestCase(EquipmentSlot.WeaponSetAMainHand)]
        [TestCase(EquipmentSlot.WeaponSetBMainHand)]
        [TestCase(EquipmentSlot.WeaponSetAOffHand)]
        [TestCase(EquipmentSlot.WeaponSetBOffHand)]
        [TestCase(EquipmentSlot.Helmet)]
        [TestCase(EquipmentSlot.Armor)]
        [TestCase(EquipmentSlot.Gloves)]
        [TestCase(EquipmentSlot.Boots)]
        public void RaidAndStashIndicatorsFollowEquipmentEmptinessNotMissingItemArt(EquipmentSlot slot)
        {
            foreach (string path in new[] { "Assets/Prefabs/UI/PlayerUI/EquipmentPanel.prefab", "Assets/Prefabs/UI/StashInventory.prefab" })
            {
                GameObject root = Clone(path);
                var view = root.GetComponentsInChildren<RaidInventorySlotView>(true).Single(v => v.name == slot.ToString());
                var icon = Field<Image>(view, "_icon");
                Sprite category = Category(view, "_emptyCategory", slot);
                Sprite itemSprite = ItemSprite();
                view.PresentEquipmentSlot(slot, RaidInventorySlotData.Empty, false, false);
                Assert.That(icon.enabled, Is.True);
                Assert.That(icon.sprite, Is.SameAs(category));
                AssertStyle(view, "_emptyCategory", icon, true);
                var item = RaidInventorySlotData.Create(new LootEntry(new LootId("item"), 1), null, itemSprite);
                view.PresentEquipmentSlot(slot, item, false, true);
                Assert.That(Field<Image>(view, "_icon"), Is.SameAs(icon), "One retained Image displays both states.");
                Assert.That(icon.sprite, Is.SameAs(itemSprite));
                AssertStyle(view, "_emptyCategory", icon, false);
                var occupied = RaidInventorySlotData.Create(new LootEntry(new LootId("no-art"), 1), null, null);
                view.PresentEquipmentSlot(slot, occupied, false, true);
                Assert.That(icon.enabled, Is.False, "Missing occupied art must not display the empty-category sprite.");
                view.PresentEquipmentSlot(slot, item, false, false, true);
                Assert.That(icon.sprite, Is.SameAs(itemSprite));
                AssertStyle(view, "_emptyCategory", icon, false, 0.35f);
                Assert.That(view.IsEquipmentDropReserved, Is.True);
                view.PresentEquipmentSlot(slot, RaidInventorySlotData.Empty, false, false);
                Assert.That(icon.sprite, Is.SameAs(category));
                AssertStyle(view, "_emptyCategory", icon, true);
                view.Present(RaidInventorySlotData.Empty);
                Assert.That(icon.enabled, Is.False);
                view.PresentQuickSlot(0, false, default, null, string.Empty, 0, false);
                Assert.That(icon.enabled, Is.False);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "EmptyCategoryGlyph"), Is.False);
            }
        }

        [Test]
        public void CharacterSheetIndicatorsHideForOccupiedEntriesWithoutIcons()
        {
            var views = Clone("Assets/Prefabs/TownAttributeAssignmentView.prefab").GetComponentsInChildren<TownEquipmentSlotView>(true);
            Assert.That(views.Length, Is.EqualTo(8));
            foreach (var view in views)
            {
                Sprite category = Category(view, "_emptyCategory", view.Slot);
                Image icon = Field<Image>(view, "_icon");
                view.PresentEmpty();
                Assert.That(icon.sprite, Is.SameAs(category));
                AssertStyle(view, "_emptyCategory", icon, true);
                Sprite item = ItemSprite();
                view.Present(new TownEquipmentSlotEntry(view.Slot, new LootId("item"), "Item", item));
                Assert.That(icon.sprite, Is.SameAs(item));
                AssertStyle(view, "_emptyCategory", icon, false);
                view.Present(new TownEquipmentSlotEntry(view.Slot, new LootId("no-art"), "Occupied", null));
                Assert.That(icon.enabled, Is.False);
                view.PresentEmpty();
                Assert.That(icon.sprite, Is.SameAs(category));
            }
        }

        [Test]
        public void PreparationInspectionSeparatesEmptyOccupiedAndReserved()
        {
            var views = Clone("Assets/Prefabs/TownRaidPreparationView.prefab").GetComponentsInChildren<TownRaidPreparationInspectionSlotView>(true);
            var item = ScriptableObject.CreateInstance<LootDefinition>();
            _owned.Add(item);
            Assert.That(views.Length, Is.EqualTo(8));
            foreach (var view in views)
            {
                Sprite category = Category(view, "_emptyCategory", view.Slot);
                Image icon = Field<Image>(view, "_icon");
                view.Clear();
                Assert.That(icon.sprite, Is.SameAs(category));
                AssertStyle(view, "_emptyCategory", icon, true);
                view.Present(item, false);
                Assert.That(icon.enabled, Is.False);
                view.Present(null, true);
                Assert.That(icon.enabled, Is.False);
                AssertStyle(view, "_emptyCategory", icon, false, 0.35f);
                view.Clear();
                Assert.That(icon.sprite, Is.SameAs(category));
                AssertStyle(view, "_emptyCategory", icon, true);
            }
        }

        [Test]
        public void PreparationCardReusesItsThreeSummaryImagesWithoutCategoryObjects()
        {
            var root = Clone("Assets/Prefabs/UI/TownRaidPreparationMemberCard.prefab");
            var view = root.GetComponent<TownRaidPreparationMemberCardView>();
            typeof(TownRaidPreparationMemberCardView).GetMethod("Awake", Fields).Invoke(view, null);
            var member = new TownRaidPreparationPresentation.Member("Member", false, false);
            view.Present(member);
            Assert.That(Field<Image>(view, "_mainHandIcon").sprite, Is.SameAs(Category(view, "_mainHandCategory", EquipmentSlot.WeaponSetAMainHand)));
            Assert.That(Field<Image>(view, "_offHandIcon").sprite, Is.SameAs(Category(view, "_offHandCategory", EquipmentSlot.WeaponSetAOffHand)));
            Assert.That(Field<Image>(view, "_armorIcon").sprite, Is.SameAs(Category(view, "_armorCategory", EquipmentSlot.Armor)));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "EmptyCategoryGlyph"), Is.False);
            var reader = new PreparedReader();
            var iconless = ScriptableObject.CreateInstance<LootDefinition>();
            _owned.Add(iconless);
            foreach (var slot in new[] { EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.Armor })
                reader.Items[slot] = iconless;
            member = new TownRaidPreparationPresentation.Member(default, false, "Member", false, false, reader);
            view.Present(member);
            foreach (string field in new[] { "_mainHandIcon", "_offHandIcon", "_armorIcon" })
                Assert.That(Field<Image>(view, field).enabled, Is.False);
            reader.Items.Clear();
            var twoHanded = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/GreatHammer.asset");
            Assert.That(twoHanded, Is.Not.Null);
            reader.Items[EquipmentSlot.WeaponSetAMainHand] = twoHanded;
            member = new TownRaidPreparationPresentation.Member(default, false, "Member", false, false, reader);
            view.Present(member);
            Assert.That(Field<Image>(view, "_offHandIcon").sprite, Is.SameAs(twoHanded.Icon));
            Assert.That(Field<Image>(view, "_offHandIcon").color.a, Is.EqualTo(0.35f));
            AssertStyle(view, "_offHandCategory", Field<Image>(view, "_offHandIcon"), false, 0.35f);
        }

        [Test]
        public void OrdinaryInventoryAndQuickSlotsDoNotAcquireCategoryLayers()
        {
            var root = Clone("Assets/Prefabs/UI/PlayerUI/QuickSlots.prefab");
            Assert.That(root.GetComponentsInChildren<Image>(true).Any(i => i.name == "EmptyCategoryGlyph"), Is.False);
            var generic = Clone("Assets/Prefabs/UI/RaidInventorySlot.prefab").GetComponent<RaidInventorySlotView>();
            Assert.That(new SerializedObject(generic).FindProperty("_emptyCategory").FindPropertyRelative("_sprite").objectReferenceValue, Is.Null);
        }

        private GameObject Clone(string path)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            _owned.Add(root);
            return root;
        }

        private sealed class PreparedReader : IPreparedEquipmentReader
        {
            public readonly Dictionary<EquipmentSlot, LootDefinition> Items = new();
            public int ObservedEquipmentRevision => 0;
            public bool TryGetPreparedSlotDefinition(EquipmentSlot slot, out LootDefinition definition) =>
                Items.TryGetValue(slot, out definition);
        }

        private static T Field<T>(Object target, string name) where T : Object
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " is missing");
            return (T)field.GetValue(target);
        }

        private Sprite ItemSprite()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            _owned.Add(sprite);
            return sprite;
        }

        private static Sprite Category(Object view, string field, EquipmentSlot slot)
        {
            var config = new SerializedObject(view).FindProperty(field);
            Assert.That(config, Is.Not.Null, field);
            var sprite = (Sprite)config.FindPropertyRelative("_sprite").objectReferenceValue;
            Assert.That(sprite, Is.Not.Null, slot.ToString());
            string expected = slot switch
            {
                EquipmentSlot.WeaponSetAMainHand or EquipmentSlot.WeaponSetBMainHand => "main_hand",
                EquipmentSlot.WeaponSetAOffHand or EquipmentSlot.WeaponSetBOffHand => "off_hand",
                _ => slot.ToString().ToLowerInvariant()
            };
            Assert.That(sprite.name, Is.EqualTo(expected));
            Image image = Field<Image>(view, field == "_mainHandCategory" ? "_mainHandIcon"
                : field == "_offHandCategory" ? "_offHandIcon" : field == "_armorCategory" ? "_armorIcon" : "_icon");
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.transform.parent.GetComponentsInChildren<Transform>(true).Any(t => t.name == "EmptyCategoryGlyph"), Is.False);
            return sprite;
        }

        private static void AssertStyle(Object view, string field, Image icon, bool empty, float alpha = 1f)
        {
            var style = new SerializedObject(view).FindProperty(field).FindPropertyRelative(empty ? "_emptyStyle" : "_itemStyle");
            Assert.That(icon.rectTransform.anchorMin, Is.EqualTo(style.FindPropertyRelative("_anchorMin").vector2Value));
            Assert.That(icon.rectTransform.anchorMax, Is.EqualTo(style.FindPropertyRelative("_anchorMax").vector2Value));
            Assert.That(icon.rectTransform.anchoredPosition, Is.EqualTo(style.FindPropertyRelative("_anchoredPosition").vector2Value));
            Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(style.FindPropertyRelative("_sizeDelta").vector2Value));
            Assert.That(icon.rectTransform.pivot, Is.EqualTo(style.FindPropertyRelative("_pivot").vector2Value));
            Color color = style.FindPropertyRelative("_color").colorValue;
            color.a *= alpha;
            Assert.That(icon.color, Is.EqualTo(color));
        }
    }
}
#endif

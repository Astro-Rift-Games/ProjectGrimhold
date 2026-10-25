#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TownRaidPreparationMemberCardRulesTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCase(true, "Listo")]
        [TestCase(false, "Preparando")]
        public void StateLabel_UsesReadyState(bool ready, string expected) =>
            Assert.That(TownRaidPreparationMemberCardRules.StateLabel(ready), Is.EqualTo(expected));

        [TestCase(WeaponHandedness.TwoHanded, true)]
        [TestCase(WeaponHandedness.OneHanded, false)]
        public void OffHand_ReservesOnlyForTwoHandedMain(WeaponHandedness handedness, bool expectedReserved)
        {
            var main = Create<LootDefinition>();
            var off = Create<LootDefinition>();
            var weapon = Create<WeaponDefinition>();
            var serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("_handedness").intValue = (int)handedness;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            var serializedLoot = new SerializedObject(main);
            serializedLoot.FindProperty("_weaponDefinition").objectReferenceValue = weapon;
            serializedLoot.ApplyModifiedPropertiesWithoutUndo();
            var resolved = TownRaidPreparationMemberCardRules.ResolveOffHand(main, off, out bool reserved);
            Assert.That(reserved, Is.EqualTo(expectedReserved));
            Assert.That(resolved, Is.SameAs(expectedReserved ? main : off));
        }

        [Test]
        public void OffHand_MissingMainPreservesOffHandAndEmptyStaysEmpty()
        {
            var off = Create<LootDefinition>();
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOffHand(null, off, out bool reserved), Is.SameAs(off));
            Assert.That(reserved, Is.False);
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOffHand(null, null, out reserved), Is.Null);
            Assert.That(reserved, Is.False);
        }

        [TestCase(EquipmentSlot.WeaponSetAMainHand)]
        [TestCase(EquipmentSlot.WeaponSetAOffHand)]
        [TestCase(EquipmentSlot.Armor)]
        [TestCase(EquipmentSlot.WeaponSetBMainHand)]
        public void Summary_WithoutEquipmentIsEmptyAndNotReserved(EquipmentSlot slot)
        {
            var member = new TownRaidPreparationPresentation.Member("Player", false, false);
            Assert.That(TownRaidPreparationMemberCardRules.ResolveSummarySlot(member, slot, out bool reserved), Is.Null);
            Assert.That(reserved, Is.False);
        }

        [Test]
        public void Overlay_PlaceholderUsesBaseAndTint_AbsentDefinitionDisables()
        {
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _created.Add(sprite);
            var loot = Create<LootDefinition>();
            var serialized = new SerializedObject(loot);
            var visual = serialized.FindProperty("_equipmentVisualDefinition");
            visual.FindPropertyRelative("_usesBaseSpritesAsPlaceholder").boolValue = true;
            visual.FindPropertyRelative("_tint").colorValue = Color.red;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOverlay(sprite, loot, out var resolved, out var color), Is.True);
            Assert.That(resolved, Is.SameAs(sprite));
            Assert.That(color, Is.EqualTo(Color.red));
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOverlay(sprite, null, out resolved, out color), Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOverlay(null, loot, out _, out _), Is.False);

            visual.FindPropertyRelative("_usesBaseSpritesAsPlaceholder").boolValue = false;
            var mappings = visual.FindPropertyRelative("_spriteMappings");
            mappings.arraySize = 1;
            mappings.GetArrayElementAtIndex(0).FindPropertyRelative("BaseSprite").objectReferenceValue = sprite;
            mappings.GetArrayElementAtIndex(0).FindPropertyRelative("EquipmentSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOverlay(sprite, loot, out resolved, out color), Is.True);
            Assert.That(resolved, Is.SameAs(sprite));
            Assert.That(color, Is.EqualTo(Color.white));
            mappings.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(TownRaidPreparationMemberCardRules.ResolveOverlay(sprite, loot, out _, out _), Is.False);
        }

        private T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }
    }
}
#endif

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownRaidPreparationViewPlayModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/TownRaidPreparationView.prefab";
        private const string LootPath = "Assets/Scriptable Objects/Loot/Definitions/";
        private GameObject _instance;
        private GameObject _eventSystemObject;
        private TownRaidPreparationView _view;
        private TownRaidPreparationInspectionView _inspection;
        private RectTransform _container;
        private FakePreparedEquipmentReader _reader;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            _instance = Object.Instantiate(prefab);
            _view = _instance.GetComponent<TownRaidPreparationView>();
            Assert.That(_view, Is.Not.Null);
            _container = Field<RectTransform>(_view, "_memberCardContainer");
            _inspection = Field<TownRaidPreparationInspectionView>(_view, "_inspectionView");
            _view.Open();
            Assert.That(_view.IsPanelOpen, Is.True);
            if (EventSystem.current == null)
                _eventSystemObject = new GameObject("PreparationTestEventSystem", typeof(EventSystem));
            _reader = new FakePreparedEquipmentReader { ObservedEquipmentRevision = 1 };
            _reader.Definitions[EquipmentSlot.WeaponSetAMainHand] = LoadLoot("GreatHammer");
            // Set A off hand is intentionally empty: the two-handed main hand reserves it visually.
            _reader.Definitions[EquipmentSlot.WeaponSetBMainHand] = LoadLoot("ArmingSword");
            _reader.Definitions[EquipmentSlot.WeaponSetBOffHand] = LoadLoot("Shield");
            _reader.Definitions[EquipmentSlot.Helmet] = LoadLoot("HeavyArmorHelmet");
            _reader.Definitions[EquipmentSlot.Armor] = LoadLoot("HeavyArmorBreastplate");
            _reader.Definitions[EquipmentSlot.Gloves] = LoadLoot("HeavyArmorGauntlets");
            _reader.Definitions[EquipmentSlot.Boots] = LoadLoot("HeavyArmorLegPlate");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
            if (_eventSystemObject != null) Object.DestroyImmediate(_eventSystemObject);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Cards_GrowShrinkAndClearWithoutDestroyingPool(int localIndex)
        {
            Present(5, localIndex);
            var pooled = Cards();
            Assert.That(pooled, Has.Length.EqualTo(5));
            Assert.That(ActiveCardCount(), Is.EqualTo(5));

            Present(2, localIndex);
            Assert.That(ActiveCardCount(), Is.EqualTo(2));
            Assert.That(Cards(), Is.EqualTo(pooled), "Shrinking must retain the same pooled instances.");
            _view.PresentNoPreparation();
            Assert.That(ActiveCardCount(), Is.Zero);
            Assert.That(Cards(), Is.EqualTo(pooled));

            Present(5, localIndex);
            Assert.That(ActiveCardCount(), Is.EqualTo(5));
            Assert.That(Cards(), Is.EqualTo(pooled), "Growing again must reuse the pool.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Selection_LocalIsDisabled_RemoteRaisesIdentityAndOpensInspection(int localIndex)
        {
            Present(2, localIndex);
            var cards = Cards();
            int selections = 0;
            ProfileId selected = default;
            _view.MemberSelected += id => { selections++; selected = id; };
            var localButton = Field<Button>(cards[localIndex], "_selectionButton");
            Assert.That(localButton.interactable, Is.False);
            localButton.onClick.Invoke();
            Assert.That(selections, Is.Zero);
            Assert.That(_inspection.IsOpen, Is.False);

            int remoteIndex = 1 - localIndex;
            var remoteButton = Field<Button>(cards[remoteIndex], "_selectionButton");
            Assert.That(remoteButton.interactable, Is.True);
            remoteButton.onClick.Invoke();
            Assert.That(selections, Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(Profile(remoteIndex)));
            Assert.That(_inspection.IsOpen, Is.True);
            Assert.That(_inspection.InspectedProfileId, Is.EqualTo(selected));
            Assert.That(Slot(EquipmentSlot.WeaponSetBOffHand).Definition,
                Is.SameAs(_reader.Definitions[EquipmentSlot.WeaponSetBOffHand]));
        }

        [Test]
        public void Cards_ShowNamesReadinessHostAndReservedSummaryIcons()
        {
            Present(2);
            var cards = Cards();
            for (int index = 0; index < cards.Length; index++)
            {
                Assert.That(Field<TMP_Text>(cards[index], "_nameText").text, Is.EqualTo("Member " + index));
                Assert.That(Field<TMP_Text>(cards[index], "_stateText").text,
                    Is.EqualTo(index == 0 ? "Listo" : "Preparando"));
                Assert.That(Field<GameObject>(cards[index], "_hostIndicator").activeSelf, Is.EqualTo(index == 0));
                AssertIcon(Field<Image>(cards[index], "_mainHandIcon"), _reader.Definitions[EquipmentSlot.WeaponSetAMainHand]);
                var offHand = Field<Image>(cards[index], "_offHandIcon");
                AssertIcon(offHand, _reader.Definitions[EquipmentSlot.WeaponSetAMainHand]);
                Assert.That(offHand.color.a, Is.LessThan(1f));
                AssertIcon(Field<Image>(cards[index], "_armorIcon"), _reader.Definitions[EquipmentSlot.Armor]);
            }
        }

        [Test]
        public void Inspection_ShowsEightSlotsRefreshesRevisionAndClosesWhenMemberLeavesOrPreparationEnds()
        {
            Present(2);
            Select(1);
            var slots = Field<TownRaidPreparationInspectionSlotView[]>(_inspection, "_slots");
            Assert.That(slots, Has.Length.EqualTo(8));
            var seen = new HashSet<EquipmentSlot>();
            foreach (var slot in slots)
            {
                Assert.That(seen.Add(slot.Slot), Is.True, "Each equipment slot must occur once.");
                var expected = slot.Slot == EquipmentSlot.WeaponSetAOffHand
                    ? _reader.Definitions[EquipmentSlot.WeaponSetAMainHand] : _reader.Definitions[slot.Slot];
                Assert.That(slot.Definition, Is.SameAs(expected), slot.Slot.ToString());
                var icon = Field<Image>(slot, "_icon");
                AssertIcon(icon, expected);
                Assert.That(icon.color.a, Is.EqualTo(slot.Slot == EquipmentSlot.WeaponSetAOffHand ? 0.35f : 1f));
            }
            Assert.That(seen, Is.EquivalentTo(EquipmentSlotRules.AllSlots));

            var replacement = LoadLoot("ArmingSword");
            _reader.Definitions[EquipmentSlot.WeaponSetAMainHand] = replacement;
            _reader.ObservedEquipmentRevision++;
            Present(2);
            Assert.That(_inspection.IsOpen, Is.True);
            Assert.That(Slot(EquipmentSlot.WeaponSetAMainHand).Definition, Is.SameAs(replacement));
            Assert.That(Slot(EquipmentSlot.WeaponSetAOffHand).Definition, Is.Null);
            Assert.That(Field<Image>(Slot(EquipmentSlot.WeaponSetAOffHand), "_icon").color.a, Is.EqualTo(1f));

            Present(1);
            Assert.That(_inspection.IsOpen, Is.False);
            Present(2);
            Select(1);
            Assert.That(_inspection.IsOpen, Is.True);
            _view.PresentNoPreparation();
            Assert.That(_inspection.IsOpen, Is.False);
        }

        [Test]
        public void Tooltip_OpensOnOccupiedSlotClosesOnExitAndStaysClosedForEmptySlot()
        {
            _reader.Definitions.Remove(EquipmentSlot.Gloves);
            Present(2);
            Select(1);
            var tooltip = Field<EquipmentTooltipView>(_inspection, "_tooltip");
            var pointer = new PointerEventData(EventSystem.current);
            var occupied = Slot(EquipmentSlot.WeaponSetBOffHand);
            Assert.That(tooltip.IsOpen, Is.False);
            occupied.OnPointerEnter(pointer);
            Assert.That(tooltip.IsOpen, Is.True);
            occupied.OnPointerExit(pointer);
            Assert.That(tooltip.IsOpen, Is.False);
            var empty = Slot(EquipmentSlot.Gloves);
            Assert.That(empty.Definition, Is.Null);
            empty.OnPointerEnter(pointer);
            Assert.That(tooltip.IsOpen, Is.False);
            empty.OnPointerExit(pointer);
            Assert.That(tooltip.IsOpen, Is.False);

            occupied.OnPointerEnter(pointer);
            Assert.That(tooltip.IsOpen, Is.True);
            _view.PresentNoPreparation();
            Assert.That(tooltip.IsOpen, Is.False);
        }

        private void Present(int count, int localIndex = 0)
        {
            Assert.That(RaidCode.TryParse("100002", out var code), Is.True);
            var members = new TownRaidPreparationMember[count];
            for (int index = 0; index < count; index++)
                members[index] = new TownRaidPreparationMember(Profile(index), index == 0);
            var snapshot = new TownRaidPreparationSnapshot(code, Profile(0), TownRaidPreparationState.Waiting, members, 1);
            bool ResolveName(ProfileId id, out string name)
            {
                name = "Member " + id.Value.Substring("profile-".Length);
                return true;
            }
            bool ResolveEquipment(ProfileId id, out IPreparedEquipmentReader reader)
            {
                reader = _reader;
                return true;
            }
            Assert.That(TownRaidPreparationPresentation.TryCreate(snapshot, Profile(localIndex),
                ResolveName, ResolveEquipment, out var presentation), Is.True);
            Assert.That(presentation.IsHost, Is.EqualTo(localIndex == 0));
            _view.PresentPreparation(presentation);
        }

        private static ProfileId Profile(int index) => new("profile-" + index);
        private TownRaidPreparationMemberCardView[] Cards() =>
            _container.GetComponentsInChildren<TownRaidPreparationMemberCardView>(true);
        private int ActiveCardCount()
        {
            int count = 0;
            foreach (var card in Cards()) if (card.gameObject.activeInHierarchy) count++;
            return count;
        }
        private void Select(int index) => Field<Button>(Cards()[index], "_selectionButton").onClick.Invoke();
        private TownRaidPreparationInspectionSlotView Slot(EquipmentSlot slot)
        {
            foreach (var view in Field<TownRaidPreparationInspectionSlotView[]>(_inspection, "_slots"))
                if (view.Slot == slot) return view;
            Assert.Fail("Missing inspection slot: " + slot);
            return null;
        }
        private static LootDefinition LoadLoot(string name)
        {
            var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(LootPath + name + ".asset");
            Assert.That(definition, Is.Not.Null, name);
            Assert.That(definition.Icon, Is.Not.Null, name + " icon");
            return definition;
        }
        private static void AssertIcon(Image icon, LootDefinition definition)
        {
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.SameAs(definition.Icon));
        }
        private static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            var value = (T)field.GetValue(owner);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }
        private sealed class FakePreparedEquipmentReader : IPreparedEquipmentReader
        {
            public int ObservedEquipmentRevision { get; set; }
            public Dictionary<EquipmentSlot, LootDefinition> Definitions { get; } = new();
            public bool TryGetPreparedSlotDefinition(EquipmentSlot slot, out LootDefinition definition) =>
                Definitions.TryGetValue(slot, out definition);
        }
    }
}
#endif

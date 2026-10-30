#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Presentation
{
    public sealed class CharcoalUiStatePresentationTests
    {
        private readonly List<GameObject> _owned = new();
        private Dictionary<string, Sprite> _sprites;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp() => _sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/Charcoal/grimhold-ui-charcoal.png")
            .OfType<Sprite>().ToDictionary(s => s.name);

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _owned) if (root != null) Object.DestroyImmediate(root);
            _owned.Clear();
        }

        [UnityTest]
        public IEnumerator AuthoredSlotRestoresSemanticSpritesAlphaAndPulseWithoutChangingIcon()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaidInventorySlot.prefab"));
            _owned.Add(root);
            var view = root.GetComponent<RaidInventorySlotView>();
            var image = (Image)typeof(RaidInventorySlotView).GetField("_background", Fields).GetValue(view);
            var icon = (Image)typeof(RaidInventorySlotView).GetField("_icon", Fields).GetValue(view);
            var item = RaidInventorySlotData.Create(new LootEntry(new LootId("charcoal-test"), 1), null, null);
            view.Clear();
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_empty"]));
            view.Present(item);
            view.SetInteraction(true, false);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_occupied"]));
            var itemIcon = icon.sprite;
            view.OnPointerEnter(null);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_hovered"]));
            view.OnPointerExit(null);
            view.SetInteraction(true, true);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_selected"]));
            view.SetDropHighlight(DropHighlightState.Invalid);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_invalid"]));
            view.ShowTransferSuccess();
            Assert.That(image.color, Is.Not.EqualTo(Color.white));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(view.IsTransferFeedbackActive, Is.False);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_invalid"]));
            Assert.That(image.color, Is.EqualTo(Color.white));
            view.SetDropHighlight(DropHighlightState.Valid);
            Assert.That(image.sprite, Is.SameAs(_sprites["slot_hovered"]));
            view.SetDropHighlight(DropHighlightState.None);
            view.PresentEquipmentSlot(EquipmentSlot.WeaponSetAMainHand, item, false, true, false, true);
            Assert.That(image.color.a, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(icon.sprite, Is.SameAs(itemIcon));
            view.ShowTransferSuccess();
            root.SetActive(false);
            Assert.That(view.IsTransferFeedbackActive, Is.False);
            Assert.That(image.color.a, Is.EqualTo(0.45f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator TownTabsKeepPersistentIdentityAcrossPointerSelectionAndDisabledState()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab"));
            _owned.Add(root);
            var view = root.GetComponent<TownPlayerMenuView>();
            view.SetOpen(true);
            view.SetTabs(new[] { new KeyValuePair<string, string>("one", "One"), new KeyValuePair<string, string>("two", "Two") });
            view.SetSelected("one");
            var buttons = root.GetComponentsInChildren<Button>(true);
            var one = buttons.Single(b => b.name == "Tab_one");
            var two = buttons.Single(b => b.name == "Tab_two");
            Assert.That(((Image)one.targetGraphic).sprite, Is.SameAs(_sprites["tab_active"]));
            Assert.That(((Image)two.targetGraphic).sprite, Is.SameAs(_sprites["tab_inactive"]));
            one.OnPointerExit(null);
            two.OnSelect(null);
            yield return null;
            Assert.That(((Image)one.targetGraphic).sprite, Is.SameAs(_sprites["tab_active"]));
            Assert.That(((Image)two.targetGraphic).sprite, Is.SameAs(_sprites["tab_inactive"]));
            one.interactable = false;
            yield return null;
            Assert.That(((Image)one.targetGraphic).sprite, Is.SameAs(_sprites["tab_disabled"]));
            one.interactable = true;
            view.SetSelected("two");
            Assert.That(((Image)two.targetGraphic).sprite, Is.SameAs(_sprites["tab_active"]));
            string clicked = null;
            view.TabSelected += id => clicked = id;
            one.onClick.Invoke();
            Assert.That(clicked, Is.EqualTo("one"));
            Assert.That(view.ContentRoot, Is.Not.Null);
        }

        [Test]
        public void UnconfiguredSlotRetainsLegacyColorOnlyBehavior()
        {
            var root = new GameObject("LegacySlot", typeof(RectTransform), typeof(Image));
            _owned.Add(root);
            var view = root.AddComponent<RaidInventorySlotView>();
            var image = root.GetComponent<Image>();
            typeof(RaidInventorySlotView).GetField("_background", Fields).SetValue(view, image);
            view.SetInteraction(false, true);
            Assert.That(image.color, Is.EqualTo(new Color(0.28f, 0.22f, 0.08f, 1)));
            view.SetDropHighlight(DropHighlightState.Invalid);
            Assert.That(image.color, Is.EqualTo(new Color(0.6f, 0.2f, 0.2f, 0.8f)));
            Assert.That(image.sprite, Is.Null);
        }

        [Test]
        public void UnconfiguredTownTabsRetainLegacyColorsAndCallbacks()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab"));
            _owned.Add(root);
            var view = root.GetComponent<TownPlayerMenuView>();
            typeof(TownPlayerMenuView).GetField("_activeTabSprite", Fields).SetValue(view, null);
            typeof(TownPlayerMenuView).GetField("_inactiveTabSprite", Fields).SetValue(view, null);
            view.SetTabs(new[] { new KeyValuePair<string, string>("one", "One"), new KeyValuePair<string, string>("two", "Two") });
            view.SetSelected("two");
            var buttons = root.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Single(b => b.name == "Tab_one").targetGraphic.color,
                Is.EqualTo((Color)typeof(TownPlayerMenuView).GetField("_normalTabColor", Fields).GetValue(view)));
            Assert.That(buttons.Single(b => b.name == "Tab_two").targetGraphic.color,
                Is.EqualTo((Color)typeof(TownPlayerMenuView).GetField("_selectedTabColor", Fields).GetValue(view)));
            string selected = null;
            view.TabSelected += id => selected = id;
            buttons.Single(b => b.name == "Tab_one").onClick.Invoke();
            Assert.That(selected, Is.EqualTo("one"));
        }
    }
}
#endif

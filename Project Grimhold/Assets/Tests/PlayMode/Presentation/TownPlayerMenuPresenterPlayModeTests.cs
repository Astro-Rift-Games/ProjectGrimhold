#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownPlayerMenuPresenterPlayModeTests
    {
        private const string ViewPrefabPath = "Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab";

        private GameObject _host;
        private GameObject _readerObject;
        private GameObject _uiParent;
        private TownPlayerMenuPresenter _presenter;
        private PlayerInputReader _reader;
        private RectTransform _inventoryContent;
        private RectTransform _attributesContent;
        private RectTransform _optionsContent;
        private int _inventoryShown;
        private int _inventoryHidden;
        private int _attributesShown;
        private int _attributesHidden;
        private int _optionsShown;
        private int _optionsHidden;

        [SetUp]
        public void SetUp()
        {
            var viewPrefab = AssetDatabase.LoadAssetAtPath<TownPlayerMenuView>(ViewPrefabPath);
            Assert.That(viewPrefab, Is.Not.Null, "TownPlayerMenu prefab must exist with a TownPlayerMenuView root.");

            _host = new GameObject("TownPlayerMenuPresenterTests");
            _presenter = _host.AddComponent<TownPlayerMenuPresenter>();
            typeof(TownPlayerMenuPresenter)
                .GetField("_viewPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_presenter, viewPrefab);

            _readerObject = new GameObject("TownPlayerMenuInputReader");
            _reader = _readerObject.AddComponent<PlayerInputReader>();
            _uiParent = new GameObject("TownPlayerMenuCanvas", typeof(RectTransform), typeof(Canvas));
            _inventoryContent = new GameObject("InventoryContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _attributesContent = new GameObject("AttributesContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _optionsContent = new GameObject("OptionsContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _inventoryShown = _inventoryHidden = _attributesShown = _attributesHidden = 0;
            _optionsShown = _optionsHidden = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_readerObject);
            Object.DestroyImmediate(_uiParent);
            Object.DestroyImmediate(_inventoryContent != null ? _inventoryContent.gameObject : null);
            Object.DestroyImmediate(_attributesContent != null ? _attributesContent.gameObject : null);
            Object.DestroyImmediate(_optionsContent != null ? _optionsContent.gameObject : null);
        }

        [Test]
        public void InventoryHotkey_OpensOnInventoryTabWithSuppressionAndHostedContent()
        {
            BindWithBothTabs();

            PressInventory();

            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_presenter.SelectedTabId, Is.EqualTo(TownMenuTabIds.Inventory));
            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);
            Assert.That(_inventoryShown, Is.EqualTo(1));
            Assert.That(_attributesShown, Is.Zero);
            Assert.That(_inventoryContent.parent, Is.Not.Null);
            Assert.That(_inventoryContent.parent, Is.SameAs(_attributesContent.parent),
                "Both tabs are hosted under the same window content root.");
        }

        [Test]
        public void SameHotkeyAgain_ClosesAndReleasesSuppression()
        {
            BindWithBothTabs();
            PressInventory();

            PressInventory();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
            Assert.That(_inventoryHidden, Is.EqualTo(1));
        }

        [Test]
        public void OtherHotkeyWhileOpen_SwitchesTabWithoutSecondSuppression()
        {
            BindWithBothTabs();
            PressInventory();

            PressAttributes();

            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_presenter.SelectedTabId, Is.EqualTo(TownMenuTabIds.Attributes));
            Assert.That(_inventoryHidden, Is.EqualTo(1));
            Assert.That(_attributesShown, Is.EqualTo(1));

            PressAttributes();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False, "A single token must have been held.");
        }

        [Test]
        public void Escape_ClosesTheOpenMenu()
        {
            BindWithBothTabs();
            PressAttributes();

            InvokeReader("OnCloseInventoryPerformed");

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
            Assert.That(_attributesHidden, Is.EqualTo(1));
        }

        [Test]
        public void Hotkey_WhenAnotherPanelHoldsSuppression_DoesNotOpen()
        {
            BindWithBothTabs();
            using var otherPanel = _reader.AcquireGameplayInputSuppression();

            PressInventory();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_inventoryShown, Is.Zero);
        }

        [Test]
        public void Hotkey_ForUnregisteredTab_IsIgnored()
        {
            _presenter.RegisterTab(CreateInventoryTab());
            _presenter.Bind(_reader, _uiParent.transform);

            PressAttributes();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void UnregisteringTheSelectedTabWhileOpen_ClosesTheMenu()
        {
            BindWithBothTabs();
            PressInventory();

            _presenter.UnregisterTab(TownMenuTabIds.Inventory);

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void TabButtonClick_SwitchesTab()
        {
            BindWithBothTabs();
            PressInventory();
            Button attributesButton = FindTabButton(TownMenuTabIds.Attributes);

            attributesButton.onClick.Invoke();

            Assert.That(_presenter.SelectedTabId, Is.EqualTo(TownMenuTabIds.Attributes));
            Assert.That(_attributesShown, Is.EqualTo(1));
            Assert.That(_inventoryHidden, Is.EqualTo(1));
        }

        [Test]
        public void Unbind_ClosesReleasesSuppressionAndStopsListening()
        {
            BindWithBothTabs();
            PressInventory();

            _presenter.Unbind();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);

            PressInventory();

            Assert.That(_presenter.IsOpen, Is.False, "An unbound presenter must not react to hotkeys.");
        }

        [Test]
        public void Escape_WhenClosedAndNoOtherPanelIsOpen_OpensTheOptionsTab()
        {
            BindWithBothTabs();
            RegisterOptionsTab();

            PressEscape();

            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_presenter.SelectedTabId, Is.EqualTo(TownMenuTabIds.Options));
            Assert.That(_optionsShown, Is.EqualTo(1));
            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);
        }

        [Test]
        public void Escape_WhenOptionsIsOpen_ClosesTheMenu()
        {
            BindWithBothTabs();
            RegisterOptionsTab();
            PressEscape();

            PressEscape();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_optionsHidden, Is.EqualTo(1));
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void Escape_WhileAnotherTabIsOpen_ClosesInsteadOfSwitchingToOptions()
        {
            BindWithBothTabs();
            RegisterOptionsTab();
            PressInventory();

            PressEscape();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_optionsShown, Is.Zero);
        }

        [Test]
        public void Escape_WhenAnotherPanelHoldsSuppression_DoesNotOpenOptions()
        {
            BindWithBothTabs();
            RegisterOptionsTab();
            using var otherPanel = _reader.AcquireGameplayInputSuppression();

            PressEscape();

            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_optionsShown, Is.Zero);
        }

        [Test]
        public void Escape_ConsumedByAnotherPanel_DoesNotOpenOptions()
        {
            BindWithBothTabs();
            RegisterOptionsTab();
            bool otherPanelOpen = true;
            _reader.InventoryCloseRequested += () =>
            {
                bool consumed = otherPanelOpen;
                otherPanelOpen = false;
                return consumed;
            };

            PressEscape();
            Assert.That(_presenter.IsOpen, Is.False, "The press that closes another panel must not open Options.");

            PressEscape();
            Assert.That(_presenter.SelectedTabId, Is.EqualTo(TownMenuTabIds.Options));
            Assert.That(_presenter.IsOpen, Is.True);
        }

        [Test]
        public void Escape_WithoutAnOptionsTab_DoesNothing()
        {
            BindWithBothTabs();

            PressEscape();

            Assert.That(_presenter.IsOpen, Is.False);
        }

        [Test]
        public void OpeningTheMenu_HidesRegisteredHudAndClosingRestoresIt()
        {
            var hud = new GameObject("Hud", typeof(RectTransform));
            try
            {
                BindWithBothTabs();
                _presenter.RegisterHud(hud);

                PressInventory();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);

                PressInventory();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        [Test]
        public void SwitchingTabs_KeepsTheHudHiddenAndEscapeRestoresIt()
        {
            var hud = new GameObject("Hud", typeof(RectTransform));
            try
            {
                BindWithBothTabs();
                _presenter.RegisterHud(hud);
                PressInventory();

                PressAttributes();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);

                InvokeReader("OnCloseInventoryPerformed");
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        [Test]
        public void HudRegisteredWhileOpen_IsHiddenImmediately_AndUnbindRestoresIt()
        {
            var hud = new GameObject("Hud", typeof(RectTransform));
            try
            {
                BindWithBothTabs();
                PressInventory();

                _presenter.RegisterHud(hud);
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);

                _presenter.Unbind();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        [Test]
        public void UnregisteredHud_IsRestoredAndNoLongerControlled()
        {
            var hud = new GameObject("Hud", typeof(RectTransform));
            try
            {
                BindWithBothTabs();
                _presenter.RegisterHud(hud);
                PressInventory();

                _presenter.UnregisterHud(hud);
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

                PressInventory();
                PressInventory();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        private void RegisterOptionsTab()
        {
            _presenter.RegisterTab(new TownMenuTabRegistration(
                TownMenuTabIds.Options,
                "Options",
                _optionsContent,
                () => _optionsShown++,
                () => _optionsHidden++));
        }

        private void PressEscape() => InvokeReader("OnCloseInventoryPerformed");

        private void BindWithBothTabs()
        {
            _presenter.RegisterTab(CreateInventoryTab());
            _presenter.RegisterTab(new TownMenuTabRegistration(
                TownMenuTabIds.Attributes,
                "Attributes",
                _attributesContent,
                () => _attributesShown++,
                () => _attributesHidden++));
            _presenter.Bind(_reader, _uiParent.transform);
        }

        private TownMenuTabRegistration CreateInventoryTab() => new(
            TownMenuTabIds.Inventory,
            "Inventory",
            _inventoryContent,
            () => _inventoryShown++,
            () => _inventoryHidden++);

        private Button FindTabButton(string tabId)
        {
            foreach (Button button in _uiParent.GetComponentsInChildren<Button>(true))
            {
                if (button.name == $"Tab_{tabId}")
                {
                    return button;
                }
            }

            Assert.Fail($"No tab button named Tab_{tabId}.");
            return null;
        }

        private void PressInventory() => InvokeReader("OnToggleInventoryPerformed");

        private void PressAttributes() => InvokeReader("OnToggleAttributesPerformed");

        private void InvokeReader(string methodName)
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_reader, new object[] { default(InputAction.CallbackContext) });
        }
    }
}
#endif

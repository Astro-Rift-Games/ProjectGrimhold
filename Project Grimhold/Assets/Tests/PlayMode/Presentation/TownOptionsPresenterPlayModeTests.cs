#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownOptionsPresenterPlayModeTests
    {
        private const string MenuPrefabPath = "Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab";
        private const string OptionsPrefabPath = "Assets/Prefabs/UI/PlayerUI/TownOptions.prefab";

        private GameObject _host;
        private GameObject _readerObject;
        private GameObject _canvas;
        private TownPlayerMenuPresenter _menu;
        private TownOptionsPresenter _options;
        private PlayerInputReader _reader;
        private int _logoutCalls;
        private int _quitCalls;
        private TaskCompletionSource<bool> _logoutGate;

        [SetUp]
        public void SetUp()
        {
            var menuPrefab = AssetDatabase.LoadAssetAtPath<TownPlayerMenuView>(MenuPrefabPath);
            var optionsPrefab = AssetDatabase.LoadAssetAtPath<TownOptionsView>(OptionsPrefabPath);
            Assert.That(menuPrefab, Is.Not.Null);
            Assert.That(optionsPrefab, Is.Not.Null, "TownOptions prefab must exist with a TownOptionsView root.");

            _host = new GameObject("TownOptionsPresenterTests");
            _menu = _host.AddComponent<TownPlayerMenuPresenter>();
            typeof(TownPlayerMenuPresenter)
                .GetField("_viewPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_menu, menuPrefab);
            _options = _host.AddComponent<TownOptionsPresenter>();
            typeof(TownOptionsPresenter)
                .GetField("_viewPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_options, optionsPrefab);

            _logoutCalls = 0;
            _quitCalls = 0;
            _logoutGate = new TaskCompletionSource<bool>();
            _options.LogoutAction = () =>
            {
                _logoutCalls++;
                return _logoutGate.Task;
            };
            _options.QuitAction = () => _quitCalls++;

            _readerObject = new GameObject("TownOptionsInputReader");
            _reader = _readerObject.AddComponent<PlayerInputReader>();
            _canvas = new GameObject("TownOptionsCanvas", typeof(RectTransform), typeof(Canvas));
            _options.Register(_menu);
            _menu.Bind(_reader, _canvas.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _logoutGate.TrySetResult(true);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_readerObject);
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void Escape_OpensTheOptionsTabWithLogoutAndExitButtons()
        {
            PressEscape();

            Assert.That(_menu.IsOpen, Is.True);
            Assert.That(_menu.SelectedTabId, Is.EqualTo(TownMenuTabIds.Options));
            Assert.That(FindButton("LogoutButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindButton("ExitButton").gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void OptionsContent_IsHostedInsideTheMenuWindow()
        {
            PressEscape();

            Assert.That(
                FindButton("LogoutButton").GetComponentInParent<TownPlayerMenuView>(true),
                Is.Not.Null,
                "The Options content must live under the menu window.");
        }

        [Test]
        public void LogoutButton_RunsLogoutOnceAndDisablesBothButtonsWhileItRuns()
        {
            PressEscape();

            FindButton("LogoutButton").onClick.Invoke();
            FindButton("LogoutButton").onClick.Invoke();

            Assert.That(_logoutCalls, Is.EqualTo(1));
            Assert.That(FindButton("LogoutButton").interactable, Is.False);
            Assert.That(FindButton("ExitButton").interactable, Is.False);
        }

        [Test]
        public void ExitButton_QuitsTheApplication()
        {
            PressEscape();

            FindButton("ExitButton").onClick.Invoke();

            Assert.That(_quitCalls, Is.EqualTo(1));
        }

        [Test]
        public void Unregister_RemovesTheTabSoEscapeNoLongerOpensIt()
        {
            _options.Unregister();

            PressEscape();

            Assert.That(_menu.IsOpen, Is.False);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in _canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            Assert.Fail($"No button named {name}.");
            return null;
        }

        private void PressEscape()
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                "OnCloseInventoryPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_reader, new object[] { default(InputAction.CallbackContext) });
        }
    }
}
#endif

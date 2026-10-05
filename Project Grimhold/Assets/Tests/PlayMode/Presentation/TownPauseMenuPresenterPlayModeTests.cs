#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownPauseMenuPresenterPlayModeTests
    {
        private GameObject _contextObject;
        private GameObject _readerObject;
        private GameObject _pauseObject;
        private GameObject _panel;
        private LocalInputContext _context;
        private PlayerInputReader _reader;
        private TownPauseMenuPresenter _presenter;
        private TownPauseMenuView _view;

        [SetUp]
        public void SetUp()
        {
            _contextObject = new GameObject("TownPauseInputContext");
            _context = _contextObject.AddComponent<LocalInputContext>();
            _readerObject = new GameObject("TownPauseInputReader");
            _reader = _readerObject.AddComponent<PlayerInputReader>();
            Assert.That(_context.TryRegister(_reader), Is.True);

            _pauseObject = new GameObject("TownPauseMenu");
            _presenter = _pauseObject.AddComponent<TownPauseMenuPresenter>();
            _view = _pauseObject.GetComponent<TownPauseMenuView>();
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_pauseObject.transform, false);
            _panel.SetActive(false);
            typeof(TownPauseMenuView)
                .GetField("_rootPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, _panel);
            InvokePresenter("Update");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_pauseObject);
            Object.DestroyImmediate(_readerObject);
            Object.DestroyImmediate(_contextObject);
        }

        [Test]
        public void Escape_WhenNothingConsumesIt_OpensThePauseMenu()
        {
            PressEscape();

            Assert.That(_view.IsVisible, Is.True);
        }

        [Test]
        public void Escape_WhenPauseIsOpen_ClosesIt()
        {
            PressEscape();

            PressEscape();

            Assert.That(_view.IsVisible, Is.False);
        }

        [Test]
        public void Escape_ConsumedByAnOpenPanel_DoesNotOpenThePauseMenu()
        {
            bool panelOpen = true;
            _reader.InventoryCloseRequested += () =>
            {
                bool consumed = panelOpen;
                panelOpen = false;
                return consumed;
            };

            PressEscape();
            Assert.That(_view.IsVisible, Is.False, "The press that closes a panel must not also open the pause menu.");

            PressEscape();
            Assert.That(_view.IsVisible, Is.True, "The next press, with no panel open, opens the pause menu.");
        }

        [Test]
        public void ReaderReplacement_StopsListeningToTheOldReader()
        {
            var otherObject = new GameObject("OtherReader");
            try
            {
                var other = otherObject.AddComponent<PlayerInputReader>();
                _context.Clear();
                Assert.That(_context.TryRegister(other), Is.True);

                InvokeReader(_reader, "OnCloseInventoryPerformed");
                Assert.That(_view.IsVisible, Is.False, "The replaced reader must no longer drive the pause menu.");

                InvokeReader(other, "OnCloseInventoryPerformed");
                Assert.That(_view.IsVisible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        private void PressEscape() => InvokeReader(_reader, "OnCloseInventoryPerformed");

        private void InvokePresenter(string methodName)
        {
            MethodInfo method = typeof(TownPauseMenuPresenter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, null);
        }

        private static void InvokeReader(PlayerInputReader reader, string methodName)
        {
            MethodInfo method = typeof(PlayerInputReader).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(reader, new object[] { default(InputAction.CallbackContext) });
        }
    }
}
#endif

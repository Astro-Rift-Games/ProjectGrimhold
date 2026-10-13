#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class MainMenuKeyboardNavigationPlayModeTests
    {
        private object _inputTestFixture;
        private Type _inputTestFixtureType;
        private Keyboard _keyboard;
        private InputAction _cancel;
        private InputActionAsset _cancelAsset;
        private InputActionReference _cancelReference;
        private GameObject _eventSystemObject;
        private GameObject _panel;
        private GameObject _loginPanel;
        private Button _playButton;
        private Button _backButton;
        private int _backClicks;

        [SetUp]
        public void SetUp()
        {
            // The package test fixture is not auto-referenced by Unity's predefined
            // test assembly, so load it without changing the production assembly layout.
            _inputTestFixtureType = Type.GetType(
                "UnityEngine.InputSystem.InputTestFixture, Unity.InputSystem.TestFramework",
                true);
            _inputTestFixture = Activator.CreateInstance(_inputTestFixtureType);
            _inputTestFixtureType.GetMethod("Setup").Invoke(_inputTestFixture, null);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            // InputActionReference.Create requires the action to belong to an InputActionAsset.
            _cancelAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            _cancel = _cancelAsset.AddActionMap("UI").AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            _cancelReference = InputActionReference.Create(_cancel);
            _backClicks = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_loginPanel);
            Object.DestroyImmediate(_panel);
            Object.DestroyImmediate(_eventSystemObject);
            Object.DestroyImmediate(_cancelReference);
            _cancel.Disable();
            Object.DestroyImmediate(_cancelAsset);
            InputSystem.RemoveDevice(_keyboard);
            _inputTestFixtureType.GetMethod("TearDown").Invoke(_inputTestFixture, null);
        }

        [Test]
        public void EnablingPanel_SelectsConfiguredSelectable()
        {
            CreateEventSystem();
            CreateMenuPanel();

            _panel.SetActive(true);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(_playButton.gameObject));
        }

        [Test]
        public void ReenablingPanel_ReselectsConfiguredSelectable()
        {
            CreateEventSystem();
            CreateMenuPanel();
            _panel.SetActive(true);
            _panel.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);

            _panel.SetActive(true);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(_playButton.gameObject));
        }

        [UnityTest]
        public IEnumerator EventSystemCreatedAfterEnable_SelectsConfiguredSelectableNextFrame()
        {
            Assert.That(EventSystem.current, Is.Null);
            CreateMenuPanel();

            _panel.SetActive(true);
            CreateEventSystem();
            yield return null;
            yield return null;

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(_playButton.gameObject));
        }

        [Test]
        public void MissingSelectable_LogsErrorOnceWithoutThrowing()
        {
            CreateEventSystem();
            _panel = new GameObject("MenuPanel");
            _panel.SetActive(false);
            _panel.AddComponent<DefaultSelectableOnEnable>();

            LogAssert.Expect(LogType.Error, new Regex("No Selectable assigned"));
            _panel.SetActive(true);
            _panel.SetActive(false);
            _panel.SetActive(true);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
        }

        [Test]
        public void Cancel_WithInteractableBackButton_InvokesBackOnce()
        {
            CreateLoginPanel(backInteractable: true);

            PressEscape();

            Assert.That(_backClicks, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_WithNonInteractableBackButton_DoesNothing()
        {
            CreateLoginPanel(backInteractable: false);

            PressEscape();

            Assert.That(_backClicks, Is.Zero);
        }

        [Test]
        public void Cancel_AfterLoginPanelDisabled_DoesNothing()
        {
            CreateLoginPanel(backInteractable: true);
            _loginPanel.SetActive(false);

            PressEscape();

            Assert.That(_backClicks, Is.Zero);
            Assert.That(_cancel.enabled, Is.False);
        }

        [Test]
        public void MissingCancelAction_LogsErrorOnceWithoutThrowing()
        {
            _loginPanel = new GameObject("LoginPanel");
            _loginPanel.SetActive(false);
            _loginPanel.AddComponent<LoginPanelView>();

            LogAssert.Expect(LogType.Error, new Regex("No cancel action assigned"));
            _loginPanel.SetActive(true);
            _loginPanel.SetActive(false);
            _loginPanel.SetActive(true);

            Assert.That(_backClicks, Is.Zero);
        }

        private void CreateEventSystem()
        {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        }

        private void CreateMenuPanel()
        {
            _panel = new GameObject("MenuPanel", typeof(RectTransform));
            _panel.SetActive(false);
            var buttonObject = new GameObject("Play Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_panel.transform, false);
            _playButton = buttonObject.GetComponent<Button>();

            var selectable = _panel.AddComponent<DefaultSelectableOnEnable>();
            SetPrivateField(selectable, "_selectable", _playButton);
        }

        private void CreateLoginPanel(bool backInteractable)
        {
            _loginPanel = new GameObject("LoginPanel", typeof(RectTransform));
            _loginPanel.SetActive(false);
            var backObject = new GameObject("Back Button", typeof(RectTransform), typeof(Image), typeof(Button));
            backObject.transform.SetParent(_loginPanel.transform, false);
            _backButton = backObject.GetComponent<Button>();
            _backButton.interactable = backInteractable;
            _backButton.onClick.AddListener(() => _backClicks++);

            var view = _loginPanel.AddComponent<LoginPanelView>();
            SetPrivateField(view, "_backButton", _backButton);
            SetPrivateField(view, "_cancelAction", _cancelReference);
            _loginPanel.SetActive(true);
        }

        private void PressEscape()
        {
            SetControl(_keyboard[Key.Escape], 1f);
            SetControl(_keyboard[Key.Escape], 0f);
        }

        private static void SetControl<TValue>(InputControl<TValue> control, TValue value)
            where TValue : struct
        {
            using (DeltaStateEvent.From(control, out InputEventPtr eventPtr))
            {
                eventPtr.time = InputState.currentTime;
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif

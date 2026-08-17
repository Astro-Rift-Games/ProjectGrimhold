#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidMenuPresenterTests
    {
        private GameObject _holder;
        private RaidMenuView _view;
        private RaidMenuPresenter _presenter;
        private PlayerInputReader _inputReader;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("RaidMenuPresenterTestsHolder");
            _holder.SetActive(false);

            GameObject viewObject = new GameObject("RaidMenuView");
            viewObject.transform.SetParent(_holder.transform);

            _view = viewObject.AddComponent<RaidMenuView>();
            _presenter = viewObject.AddComponent<RaidMenuPresenter>();

            GameObject menuRoot = new GameObject("MenuRoot");
            menuRoot.transform.SetParent(viewObject.transform);
            menuRoot.SetActive(false);

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(menuRoot.transform);
            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();

            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(menuRoot.transform);
            var statusText = statusObj.AddComponent<TMPro.TextMeshProUGUI>();

            GameObject controlsObj = new GameObject("ControlsText");
            controlsObj.transform.SetParent(menuRoot.transform);
            var controlsText = controlsObj.AddComponent<TMPro.TextMeshProUGUI>();

            GameObject resumeObj = new GameObject("ResumeButton");
            resumeObj.transform.SetParent(menuRoot.transform);
            var resumeButton = resumeObj.AddComponent<Button>();
            GameObject resumeLabelObj = new GameObject("ResumeButtonText");
            resumeLabelObj.transform.SetParent(resumeObj.transform);
            var resumeButtonText = resumeLabelObj.AddComponent<TMPro.TextMeshProUGUI>();

            GameObject abandonObj = new GameObject("AbandonButton");
            abandonObj.transform.SetParent(menuRoot.transform);
            var abandonButton = abandonObj.AddComponent<Button>();
            GameObject abandonLabelObj = new GameObject("AbandonButtonText");
            abandonLabelObj.transform.SetParent(abandonObj.transform);
            var abandonButtonText = abandonLabelObj.AddComponent<TMPro.TextMeshProUGUI>();

            SetPrivateField(_view, "_menuRoot", menuRoot);
            SetPrivateField(_view, "_titleText", titleText);
            SetPrivateField(_view, "_statusText", statusText);
            SetPrivateField(_view, "_controlsText", controlsText);
            SetPrivateField(_view, "_resumeButton", resumeButton);
            SetPrivateField(_view, "_resumeButtonText", resumeButtonText);
            SetPrivateField(_view, "_abandonButton", abandonButton);
            SetPrivateField(_view, "_abandonButtonText", abandonButtonText);

            SetPrivateField(_presenter, "_view", _view);

            GameObject inputObj = new GameObject("InputReaderHolder");
            inputObj.transform.SetParent(_holder.transform);
            _inputReader = inputObj.AddComponent<PlayerInputReader>();
            InvokePrivateMethod(_inputReader, "Awake");

            _holder.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void View_PresentAliveState_SetsAliveTitleAndEnablesResumeButton()
        {
            _view.PresentAliveState();

            Assert.That(_view.TitleText.text, Is.EqualTo("Menú de Incursión"));
            Assert.That(_view.ResumeButton.gameObject.activeSelf, Is.True);
            Assert.That(_view.ResumeButton.interactable, Is.True);
            Assert.That(_view.AbandonButton.interactable, Is.True);
            Assert.That(_view.AbandonButtonText.text, Is.EqualTo("Abandonar Incursión"));
        }

        [Test]
        public void View_PresentAliveStateForOperationalHost_HidesAbandonAction()
        {
            _view.PresentAliveState(canAbandon: false);

            Assert.That(_view.AbandonButton.gameObject.activeSelf, Is.False);
            Assert.That(_view.AbandonButton.interactable, Is.False);
        }

        [Test]
        public void View_PresentDefeatedClientState_ShowsSpectateAndReturn()
        {
            _view.PresentDefeatedState(canReturn: true, isSpectating: false);

            Assert.That(_view.TitleText.text, Is.EqualTo("Has sido Derrotado"));
            Assert.That(_view.ResumeButton.gameObject.activeSelf, Is.True);
            Assert.That(_view.ResumeButtonText.text, Is.EqualTo("Observar"));
            Assert.That(_view.AbandonButton.gameObject.activeSelf, Is.True);
            Assert.That(_view.AbandonButton.interactable, Is.True);
            Assert.That(_view.AbandonButtonText.text, Is.EqualTo("Volver al pueblo"));
        }

        [Test]
        public void View_PresentDefeatedHostState_HidesReturn()
        {
            _view.PresentDefeatedState(canReturn: false, isSpectating: true);

            Assert.That(_view.ResumeButtonText.text, Is.EqualTo("Continuar observando"));
            Assert.That(_view.AbandonButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void View_PresentExtractedPending_ShowsSuccessAndDisablesReturn()
        {
            _view.PresentExtractedState(false);

            Assert.That(_view.TitleText.text, Is.EqualTo("Extracción completada"));
            Assert.That(_view.StatusText.text, Does.Contain("Guardado pendiente"));
            Assert.That(_view.ResumeButton.gameObject.activeSelf, Is.False);
            Assert.That(_view.AbandonButton.interactable, Is.False);
            Assert.That(_view.AbandonButtonText.text, Is.EqualTo("Volver al pueblo"));
        }

        [Test]
        public void View_PresentExtractedConfirmed_EnablesReturn()
        {
            _view.PresentExtractedState(true);

            Assert.That(_view.TitleText.text, Is.EqualTo("Extracción completada"));
            Assert.That(_view.StatusText.text, Does.Contain("botín fue asegurado"));
            Assert.That(_view.AbandonButton.interactable, Is.True);
            Assert.That(_view.AbandonButtonText.text, Is.EqualTo("Volver al pueblo"));
        }

        [Test]
        public void View_PresentExtractedPersistenceFailure_ExposesRetryAndDisablesReturn()
        {
            _view.PresentExtractedState(ExtractionLootSaveStatus.PersistenceFailed);

            Assert.That(_view.ResumeButton.gameObject.activeSelf, Is.True);
            Assert.That(_view.ResumeButton.interactable, Is.True);
            Assert.That(_view.AbandonButton.interactable, Is.False);
            Assert.That(_view.StatusText.text, Does.Contain("reintentar"));
        }

        [Test]
        public void Presenter_OpenMenu_AcquiresInputSuppressionAndMakesViewVisible()
        {
            SetPresenterBoundState();

            _presenter.OpenMenu();

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));
        }

        [Test]
        public void Presenter_CloseMenuWhenAlive_HidesViewAndReleasesSuppression()
        {
            SetPresenterBoundState();
            _presenter.OpenMenu();

            _presenter.CloseMenu();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
        }

        [Test]
        public void Presenter_CloseMenuWhenDefeated_DoesNotReleaseSuppression()
        {
            SetPresenterBoundState(isAlive: false);
            _presenter.OpenMenu();

            _presenter.CloseMenu();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.GreaterThan(0));
        }

        [Test]
        public void Presenter_Unbind_ClearsReferencesAndReleasesSuppression()
        {
            SetPresenterBoundState();
            _presenter.OpenMenu();

            _presenter.Unbind();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
            Assert.That(_presenter.IsOpen, Is.False);
        }

        [Test]
        public void Presenter_ObservePlayerDefeated_AutomaticallyOpensMenuAndPresentsDefeatedState()
        {
            SetPresenterBoundState();

            InvokePrivateMethod(_presenter, "ObserveCharacterState", new object[] { false });

            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.TitleText.text, Is.EqualTo("Has sido Derrotado"));
            Assert.That(ReadSuppressionCount(_inputReader), Is.GreaterThan(0));
        }

        [Test]
        public void InputReader_CloseInventory_WhenInventoryConsumed_DoesNotRaiseMenuToggleRequested()
        {
            bool menuToggleRaised = false;
            _inputReader.MenuToggleRequested += () => menuToggleRaised = true;
            _inputReader.InventoryCloseRequested += () => true; // Consumed by open inventory

            InvokePrivateMethod(_inputReader, "OnCloseInventoryPerformed", new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });

            Assert.That(menuToggleRaised, Is.False);
        }

        [Test]
        public void InputReader_CloseInventory_WhenNotConsumed_RaisesMenuToggleRequested()
        {
            bool menuToggleRaised = false;
            _inputReader.MenuToggleRequested += () => menuToggleRaised = true;
            _inputReader.InventoryCloseRequested += () => false; // Not consumed (inventory closed)

            InvokePrivateMethod(_inputReader, "OnCloseInventoryPerformed", new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });

            Assert.That(menuToggleRaised, Is.True);
        }

        private void SetPresenterBoundState(bool isAlive = true)
        {
            SetPrivateField(_presenter, "_inputReader", _inputReader);
            SetPrivateField(_presenter, "_isBound", true);
            SetPrivateField(_presenter, "_wasDefeatedObserved", !isAlive);
        }

        private static int ReadSuppressionCount(PlayerInputReader reader)
        {
            FieldInfo field = typeof(PlayerInputReader).GetField(
                "_gameplaySuppressionCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(reader);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType()}");
            field.SetValue(target, value);
        }

        private static object InvokePrivateMethod(object target, string methodName, object[] parameters = null)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on {target.GetType()}");
            return method.Invoke(target, parameters);
        }
    }
}
#endif

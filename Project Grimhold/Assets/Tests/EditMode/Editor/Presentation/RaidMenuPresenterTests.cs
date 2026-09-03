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
        private GameObject _progressionResultsRoot;
        private TMPro.TMP_Text _progressionActivityText;
        private TMPro.TMP_Text _progressionExperienceText;
        private TMPro.TMP_Text _progressionLevelText;
        private TMPro.TMP_Text _progressionLevelStatusText;
        private Image _progressionExperienceFill;

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

            _progressionResultsRoot = new GameObject("ProgressionResults");
            _progressionResultsRoot.transform.SetParent(menuRoot.transform);
            _progressionActivityText = CreateText(
                _progressionResultsRoot.transform,
                "ProgressionActivityText");
            _progressionExperienceText = CreateText(
                _progressionResultsRoot.transform,
                "ProgressionExperienceText");
            _progressionLevelText = CreateText(
                _progressionResultsRoot.transform,
                "ProgressionLevelText");
            _progressionLevelStatusText = CreateText(
                _progressionResultsRoot.transform,
                "ProgressionLevelStatusText");
            GameObject fillObject = new GameObject("ProgressionExperienceFill");
            fillObject.transform.SetParent(_progressionResultsRoot.transform);
            _progressionExperienceFill = fillObject.AddComponent<Image>();
            _progressionExperienceFill.type = Image.Type.Filled;

            SetPrivateField(_view, "_menuRoot", menuRoot);
            SetPrivateField(_view, "_titleText", titleText);
            SetPrivateField(_view, "_statusText", statusText);
            SetPrivateField(_view, "_controlsText", controlsText);
            SetPrivateField(_view, "_resumeButton", resumeButton);
            SetPrivateField(_view, "_resumeButtonText", resumeButtonText);
            SetPrivateField(_view, "_abandonButton", abandonButton);
            SetPrivateField(_view, "_abandonButtonText", abandonButtonText);
            SetPrivateField(_view, "_progressionResultsRoot", _progressionResultsRoot);
            SetPrivateField(_view, "_progressionActivityText", _progressionActivityText);
            SetPrivateField(_view, "_progressionExperienceText", _progressionExperienceText);
            SetPrivateField(_view, "_progressionLevelText", _progressionLevelText);
            SetPrivateField(
                _view,
                "_progressionLevelStatusText",
                _progressionLevelStatusText);
            SetPrivateField(
                _view,
                "_progressionExperienceFill",
                _progressionExperienceFill);

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
        public void View_PresentProgressionPending_ShowsProcessingWithoutSummary()
        {
            _view.PresentProgressionResultsPending(
                "Has sido Derrotado",
                "Persistencia pendiente.",
                canSpectate: true,
                isSpectating: false,
                canRetryPersistence: false);

            Assert.That(_view.ControlsText.text, Is.EqualTo("Procesando resultados"));
            Assert.That(_progressionResultsRoot.activeSelf, Is.False);
            Assert.That(_view.AbandonButton.interactable, Is.False);
        }

        [TestCase(10_000, "Conservación: 100%")]
        [TestCase(2_000, "Conservación: 20%")]
        [TestCase(0, "Conservación: 0%")]
        [TestCase(5_000, "Conservación: 50%")]
        public void View_PresentsRetentionDirectlyFromResult(
            int retentionBasisPoints,
            string expectedText)
        {
            ExpeditionProgressionResult result = CreateResult(
                ExpeditionExperienceResolutionOutcome.Defeated,
                retentionBasisPoints,
                resultingExperience: 25,
                nextLevelRequirement: 100,
                isMaxLevel: false);

            PresentResult(result, "Persistencia pendiente.", canReturn: false);

            Assert.That(_progressionExperienceText.text, Does.Contain(expectedText));
            Assert.That(_progressionResultsRoot.activeSelf, Is.True);
            Assert.That(_progressionExperienceFill.fillAmount, Is.EqualTo(0.25f));
        }

        [Test]
        public void View_MaxLevelWithZeroRequirement_DoesNotCalculateRatio()
        {
            ExpeditionProgressionResult result = CreateResult(
                ExpeditionExperienceResolutionOutcome.Extracted,
                retentionBasisPoints: 10_000,
                resultingExperience: 0,
                nextLevelRequirement: 0,
                isMaxLevel: true);

            PresentResult(result, "Persistencia confirmada.", canReturn: true);

            Assert.That(_progressionLevelStatusText.text, Does.Contain("Nivel máximo"));
            Assert.That(_progressionExperienceFill.fillAmount, Is.EqualTo(1f));
            Assert.That(_view.AbandonButton.interactable, Is.True);
        }

        [Test]
        public void View_PersistenceFailure_DoesNotHideOrModifyAvailableSummary()
        {
            ExpeditionProgressionResult result = CreateResult(
                ExpeditionExperienceResolutionOutcome.Defeated,
                retentionBasisPoints: 5_000,
                resultingExperience: 25,
                nextLevelRequirement: 100,
                isMaxLevel: false);
            PresentResult(result, "Persistencia pendiente.", canReturn: false);
            string capturedSummary = _progressionExperienceText.text;

            PresentResult(
                result,
                "No se pudo persistir. Pulsa Reintentar.",
                canReturn: false,
                canRetryPersistence: true);

            Assert.That(_progressionResultsRoot.activeSelf, Is.True);
            Assert.That(_progressionExperienceText.text, Is.EqualTo(capturedSummary));
            Assert.That(_view.StatusText.text, Does.Contain("Reintentar"));
            Assert.That(_view.ResumeButton.interactable, Is.True);
            Assert.That(_view.AbandonButton.interactable, Is.False);
        }

        [TestCase(false, RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed, true, false)]
        [TestCase(true, RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed, true, false)]
        [TestCase(true, RaidParticipantState.Extracted,
            ExpeditionProgressionFinalizationCause.ExtractionConfirmed, false, false)]
        [TestCase(true, RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.None, true, false)]
        [TestCase(true, RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed, true, true)]
        public void Presenter_ReturnGate_RejectsMissingObservableCondition(
            bool hasSnapshot,
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause,
            bool extractionComplete,
            bool isHost)
        {
            Assert.That(
                RaidMenuPresenter.CanIssueReturnRequest(
                    hasSnapshot,
                    state,
                    cause,
                    extractionComplete,
                    isHost,
                    isCompatibleClientPhase: !isHost,
                    isMatchFinished: false,
                    hasRaidingParticipants: isHost),
                Is.False);
        }

        [TestCase(RaidParticipantState.Extracted,
            ExpeditionProgressionFinalizationCause.ExtractionConfirmed)]
        [TestCase(RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed)]
        [TestCase(RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed)]
        public void Presenter_ReturnGate_AllowsSupportedTerminalResultAfterAck(
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause)
        {
            Assert.That(
                RaidMenuPresenter.CanIssueReturnRequest(
                    hasProgressionResultSnapshot: true,
                    state,
                    cause,
                    isExtractionProgressionComplete: true,
                    isHost: false,
                    isCompatibleClientPhase: true,
                    isMatchFinished: false,
                    hasRaidingParticipants: false),
                Is.True);
        }

        [Test]
        public void Presenter_PrematureReturnAttempt_DoesNotSetRequestLatch()
        {
            SetPresenterBoundState();

            InvokePrivateMethod(_presenter, "RequestTerminalReturnOnce");

            Assert.That(ReadPrivateField<bool>(_presenter, "_returnRequested"), Is.False);
        }

        [Test]
        public void View_AcceptedReturnRequest_DisablesButtonAndShowsPendingState()
        {
            ExpeditionProgressionResult result = CreateResult(
                ExpeditionExperienceResolutionOutcome.Defeated,
                retentionBasisPoints: 2_000,
                resultingExperience: 20,
                nextLevelRequirement: 100,
                isMaxLevel: false);

            _view.PresentProgressionResults(
                "Resultados",
                result,
                "Progreso guardado.",
                canReturn: false,
                returnRequested: true,
                canSpectate: false,
                isSpectating: false,
                canRetryPersistence: false);

            Assert.That(_view.AbandonButton.interactable, Is.False);
            Assert.That(_view.AbandonButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Is.EqualTo("Regreso solicitado"));
            Assert.That(_view.StatusText.text, Does.Contain("Regreso solicitado"));
        }

        [Test]
        public void Presenter_FinishedPhase_RefreshesRetainedResultsAndEnablesEligibleHostReturn()
        {
            Assert.That(
                RaidMenuPresenter.ShouldRefreshForMatchPhase(
                    NetworkMatchController.MatchPhase.Closing,
                    NetworkMatchController.MatchPhase.Finished,
                    hasPersistentResultScreen: true),
                Is.True);
            Assert.That(
                RaidMenuPresenter.CanIssueReturnRequest(
                    hasProgressionResultSnapshot: true,
                    RaidParticipantState.Extracted,
                    ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                    isExtractionProgressionComplete: true,
                    isHost: true,
                    isCompatibleClientPhase: true,
                    isMatchFinished: true,
                    hasRaidingParticipants: false),
                Is.True);
        }

        [Test]
        public void Presenter_AuthorizedClient_StartsTheIndividualParticipantReturn()
        {
            Assert.That(
                RaidMenuPresenter.ShouldStartParticipantReturn(
                    isReturnAuthorized: true,
                    returnStarted: false,
                    hasOperationalRole: true,
                    isOperationalHost: false),
                Is.True);
        }

        [Test]
        public void Presenter_OperationalHost_NeverStartsTheIndividualParticipantReturn()
        {
            // Cancel Raid authorizes every participant, including the Host's. The Host's
            // departure belongs to the coordinator's global raid closure.
            Assert.That(
                RaidMenuPresenter.ShouldStartParticipantReturn(
                    isReturnAuthorized: true,
                    returnStarted: false,
                    hasOperationalRole: true,
                    isOperationalHost: true),
                Is.False);
        }

        [Test]
        public void Presenter_UnresolvedOperationalRole_StartsNoParticipantReturn()
        {
            Assert.That(
                RaidMenuPresenter.ShouldStartParticipantReturn(
                    isReturnAuthorized: true,
                    returnStarted: false,
                    hasOperationalRole: false,
                    isOperationalHost: false),
                Is.False);
        }

        [Test]
        public void Presenter_ParticipantReturn_IsStartedOnlyOnce()
        {
            Assert.That(
                RaidMenuPresenter.ShouldStartParticipantReturn(
                    isReturnAuthorized: true,
                    returnStarted: true,
                    hasOperationalRole: true,
                    isOperationalHost: false),
                Is.False);
            Assert.That(
                RaidMenuPresenter.ShouldStartParticipantReturn(
                    isReturnAuthorized: false,
                    returnStarted: false,
                    hasOperationalRole: true,
                    isOperationalHost: false),
                Is.False);
        }

        [Test]
        public void Presenter_FailedParticipantReturn_ReleasesTheLatchAndRestoresTheMenu()
        {
            SetPresenterBoundState();
            SetPrivateField(_presenter, "_returnStarted", true);
            _presenter.CloseMenu();

            InvokePrivateMethod(_presenter, "RestorePresentationAfterFailedReturn");

            Assert.That(ReadPrivateField<bool>(_presenter, "_returnStarted"), Is.False);
            Assert.That(_presenter.IsOpen, Is.True);
        }

        [TestCase(RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed, true)]
        [TestCase(RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed, true)]
        [TestCase(RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed, false)]
        [TestCase(RaidParticipantState.Extracted,
            ExpeditionProgressionFinalizationCause.ExtractionConfirmed, false)]
        public void HudRetention_UsesExactTerminalSemanticCause(
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause,
            bool expected)
        {
            Assert.That(
                LocalPlayerHudBinder.ShouldRetainTerminalHud(state, cause),
                Is.EqualTo(expected));
        }

        [TestCase(0, "Sin ascenso de nivel")]
        [TestCase(1, "¡Subiste 1 nivel!")]
        [TestCase(3, "¡Subiste 3 niveles!")]
        public void View_PresentsLevelGainCountFromResult(
            int levelsGained,
            string expectedStatus)
        {
            ExpeditionProgressionResult result = CreateResult(
                ExpeditionExperienceResolutionOutcome.Extracted,
                retentionBasisPoints: 10_000,
                resultingExperience: 25,
                nextLevelRequirement: 100,
                isMaxLevel: false,
                levelsGained: levelsGained);

            PresentResult(result, "Persistencia pendiente.", canReturn: false);

            Assert.That(_progressionLevelStatusText.text, Is.EqualTo(expectedStatus));
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
        public void Presenter_OnDisable_PerformsIdempotentStructuralTeardown()
        {
            SetPresenterBoundState();
            SetPrivateField(_presenter, "_hasProgressionResultSnapshot", true);
            SetPrivateField(_presenter, "_returnRequested", true);
            _presenter.OpenMenu();

            InvokePrivateMethod(_presenter, "OnDisable");
            InvokePrivateMethod(_presenter, "OnDisable");
            _presenter.Unbind();

            Assert.That(ReadPrivateField<bool>(_presenter, "_isBound"), Is.False);
            Assert.That(
                ReadPrivateField<bool>(_presenter, "_hasProgressionResultSnapshot"),
                Is.False);
            Assert.That(ReadPrivateField<bool>(_presenter, "_returnRequested"), Is.False);
            Assert.That(ReadPrivateField<object>(_presenter, "_participant"), Is.Null);
            Assert.That(ReadPrivateField<object>(_presenter, "_progressionResolver"), Is.Null);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);
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

        private void PresentResult(
            in ExpeditionProgressionResult result,
            string persistenceFeedback,
            bool canReturn,
            bool canRetryPersistence = false)
        {
            _view.PresentProgressionResults(
                "Resultados",
                result,
                persistenceFeedback,
                canReturn,
                returnRequested: false,
                canSpectate: false,
                isSpectating: false,
                canRetryPersistence: canRetryPersistence);
        }

        private static ExpeditionProgressionResult CreateResult(
            ExpeditionExperienceResolutionOutcome outcome,
            int retentionBasisPoints,
            long resultingExperience,
            long nextLevelRequirement,
            bool isMaxLevel,
            int levelsGained = 1)
        {
            var snapshot = new ExpeditionExperienceSnapshot(10, 4, 5, 7);
            var resolution = new ExpeditionExperienceResolution(
                outcome,
                snapshot,
                retentionBasisPoints,
                consolidatedExperience: 13);
            var application = new ExperienceApplicationResult(
                previousLevel: 2,
                previousExperience: 12,
                resultingLevel: isMaxLevel ? 99 : 3,
                resultingExperience: resultingExperience,
                levelsGained: isMaxLevel ? 28 : levelsGained);
            return new ExpeditionProgressionResult(
                resolution,
                application,
                pveKillCount: 1,
                pvpKillCount: 2,
                pveAssistCount: 3,
                pvpAssistCount: 4,
                firstOpenChestCount: 5,
                eligibleExtractedLootValue: 70,
                nextLevelExperienceRequirement: nextLevelRequirement,
                isMaxLevel: isMaxLevel);
        }

        private static TMPro.TMP_Text CreateText(Transform parent, string name)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            return textObject.AddComponent<TMPro.TextMeshProUGUI>();
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

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType()}");
            return (T)field.GetValue(target);
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

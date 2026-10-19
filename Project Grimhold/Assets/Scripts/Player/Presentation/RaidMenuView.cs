using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the local raid menu and spectator controls from serialized uGUI references.
/// It owns presentation and button events only; session and gameplay decisions remain external.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMenuView : MonoBehaviour
{
    private const string DefaultControlsText =
        "W, A, S, D — Moverse\n" +
        "Click Izquierdo — Atacar\n" +
        "F — Interactuar\n" +
        "Tab — Inventario\n" +
        "Escape — Menú / Cerrar";

    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _controlsText;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private TMP_Text _resumeButtonText;
    [SerializeField] private Button _abandonButton;
    [SerializeField] private TMP_Text _abandonButtonText;
    [SerializeField] private Button _cancelRaidButton;
    [SerializeField] private TMP_Text _cancelRaidButtonText;

    [Header("Progression Results")]
    [SerializeField] private GameObject _progressionResultsRoot;
    [SerializeField] private TMP_Text _progressionActivityText;
    [SerializeField] private TMP_Text _progressionExperienceText;
    [SerializeField] private TMP_Text _progressionLevelText;
    [SerializeField] private TMP_Text _progressionLevelStatusText;
    [SerializeField] private Image _progressionExperienceFill;

    [Header("Results Headline")]
    [SerializeField] private string _extractedTitle = "EXTRACCIÓN EXITOSA";
    [SerializeField] private string _defeatedTitle = "DERROTA";
    [SerializeField] private string _abandonedTitle = "INCURSIÓN ABANDONADA";
    [SerializeField, Min(1f), Tooltip("Scales the title font while a terminal Result is shown.")]
    private float _resultTitleFontScale = 1.6f;

    [Header("Results Loot")]
    [SerializeField] private GameObject _resultLootRoot;
    [SerializeField] private TMP_Text _resultLootHeaderText;
    [SerializeField] private RectTransform _resultLootGridRoot;
    [SerializeField, Tooltip("Inactive template cloned for every loot entry.")]
    private RaidResultLootSlotView _resultLootSlotTemplate;
    [SerializeField] private TMP_Text _resultLootValueText;
    [SerializeField] private TMP_Text _resultLootEmptyText;
    [SerializeField] private string _extractedLootHeader = "BOTÍN OBTENIDO";
    [SerializeField] private string _lostLootHeader = "BOTÍN PERDIDO";
    [SerializeField] private string _extractedLootEmpty = "No obtuviste objetos en esta expedición.";
    [SerializeField] private string _lostLootEmpty = "No llevabas objetos.";
    [SerializeField, Tooltip("{0} is the total Value.")]
    private string _lootValueFormat = "Valor total: {0}";

    [Header("Level Up")]
    [SerializeField] private GameObject _levelUpBannerRoot;
    [SerializeField] private TMP_Text _levelUpBannerText;
    [SerializeField, Tooltip("{0} is the resulting level.")]
    private string _levelUpFormat = "¡NIVEL {0}!";

    [Header("Spectator")]
    [SerializeField] private GameObject _spectatorBarRoot;
    [SerializeField] private TMP_Text _spectatorTargetText;
    [SerializeField] private Button _previousTargetButton;
    [SerializeField] private Button _nextTargetButton;

    private readonly List<RaidResultLootSlotView> _resultLootSlots = new();
    private float _baseTitleFontSize;

    public event Action ResumeRequested;
    public event Action AbandonRequested;
    public event Action CancelRaidRequested;
    public event Action PreviousTargetRequested;
    public event Action NextTargetRequested;

    public GameObject MenuRoot => _menuRoot;
    public TMP_Text TitleText => _titleText;
    public TMP_Text StatusText => _statusText;
    public TMP_Text ControlsText => _controlsText;
    public Button ResumeButton => _resumeButton;
    public TMP_Text ResumeButtonText => _resumeButtonText;
    public Button AbandonButton => _abandonButton;
    public TMP_Text AbandonButtonText => _abandonButtonText;
    public Button CancelRaidButton => _cancelRaidButton;
    public GameObject ProgressionResultsRoot => _progressionResultsRoot;
    public TMP_Text ProgressionActivityText => _progressionActivityText;
    public TMP_Text ProgressionExperienceText => _progressionExperienceText;
    public TMP_Text ProgressionLevelText => _progressionLevelText;
    public TMP_Text ProgressionLevelStatusText => _progressionLevelStatusText;
    public Image ProgressionExperienceFill => _progressionExperienceFill;
    public GameObject SpectatorBarRoot => _spectatorBarRoot;
    public TMP_Text SpectatorTargetText => _spectatorTargetText;
    public Button PreviousTargetButton => _previousTargetButton;
    public Button NextTargetButton => _nextTargetButton;
    public bool IsOpen => UiPanelVisibility.IsVisible(_menuRoot);

    private void Awake()
    {
        _baseTitleFontSize = _titleText != null ? _titleText.fontSize : 0f;
        CacheButtonLabels();
        BindButtonListeners();
        SetProgressionResultsVisible(false);
        SetSpectatorBarVisible(false);
    }

    private void OnEnable()
    {
        BindButtonListeners();
    }

    private void OnDisable()
    {
        UnbindButtonListeners();
    }

    public void SetMenuVisible(bool visible)
    {
        if (visible)
        {
            BindButtonListeners();
        }

        UiPanelVisibility.SetVisible(_menuRoot, visible);
    }

    /// <summary>Returns the editable headline shown for a terminal Results outcome.</summary>
    public string GetOutcomeTitle(RaidResultsOutcome outcome) => outcome switch
    {
        RaidResultsOutcome.Extracted => _extractedTitle,
        RaidResultsOutcome.Defeated => _defeatedTitle,
        RaidResultsOutcome.Abandoned => _abandonedTitle,
        _ => "Resultados"
    };

    /// <summary>Shows the loot captured for this Results screen; it never reads the inventory.</summary>
    public void PresentResultsLoot(RaidResultsLootSummary summary, LootDefinitionCatalog catalog)
    {
        if (summary == null || summary.Kind == RaidResultsLootKind.None || catalog == null)
        {
            ClearResultsLoot();
            return;
        }

        bool isReward = summary.Kind == RaidResultsLootKind.Extracted;
        SetText(_resultLootHeaderText, isReward ? _extractedLootHeader : _lostLootHeader);
        SetText(_resultLootValueText, string.Format(_lootValueFormat, summary.TotalValue));
        if (_resultLootValueText != null)
        {
            // Design shows a total Value for extracted loot only; lost loot is just listed.
            _resultLootValueText.gameObject.SetActive(isReward);
        }

        SetText(_resultLootEmptyText, isReward ? _extractedLootEmpty : _lostLootEmpty);
        if (_resultLootEmptyText != null)
        {
            _resultLootEmptyText.gameObject.SetActive(summary.Entries.Count == 0);
        }

        PresentResultsLootSlots(summary, catalog, isReward);
        UiPanelVisibility.SetVisible(_resultLootRoot, true);
    }

    public void ClearResultsLoot()
    {
        for (int index = 0; index < _resultLootSlots.Count; index++)
        {
            _resultLootSlots[index].gameObject.SetActive(false);
        }

        UiPanelVisibility.SetVisible(_resultLootRoot, false);
    }

    /// <summary>Plays the level-up banner, distinct from the plain experience bar update.</summary>
    public void PresentLevelUp(int resultingLevel, bool animate)
    {
        SetText(_levelUpBannerText, string.Format(_levelUpFormat, resultingLevel));
        UiPanelVisibility.SetVisible(_levelUpBannerRoot, true);
        if (animate && _levelUpBannerRoot != null)
        {
            UiFeedbackPulse.Play(_levelUpBannerRoot, UiPulseKind.Success);
        }
    }

    public void HideLevelUp()
    {
        UiPanelVisibility.SetVisible(_levelUpBannerRoot, false);
    }

    public void PresentAliveState(bool canAbandon = true)
    {
        SetProgressionResultsVisible(false);
        SetTitleEmphasis(false);
        SetText(_titleText, "Menú de Incursión");
        SetText(_statusText, "La simulación continúa en tiempo real.");
        SetText(_controlsText, DefaultControlsText);
        SetButtonState(_resumeButton, true, true);
        SetText(_resumeButtonText, "Reanudar");
        SetButtonState(_abandonButton, canAbandon, canAbandon);
        SetText(_abandonButtonText, "Abandonar Incursión");
        SetCancelRaidVisible(false);
    }

    /// <summary>Presents role-aware actions for a defeated local participant.</summary>
    public void PresentDefeatedState(bool canReturn, bool isSpectating)
    {
        SetProgressionResultsVisible(false);
        SetTitleEmphasis(true);
        SetText(_titleText, GetOutcomeTitle(RaidResultsOutcome.Defeated));
        SetText(
            _statusText,
            canReturn
                ? "Puedes observar la incursión o volver al pueblo."
                : "Debes continuar observando mientras la incursión siga activa.");
        SetText(_controlsText, string.Empty);
        SetButtonState(_resumeButton, true, true);
        SetText(_resumeButtonText, isSpectating ? "Continuar observando" : "Observar");
        SetButtonState(_abandonButton, canReturn, canReturn);
        SetText(_abandonButtonText, "Volver al pueblo");
        SetCancelRaidVisible(false);
    }

    public void PresentExtractedState(bool isCommitConfirmed)
    {
        SetProgressionResultsVisible(false);
        SetTitleEmphasis(true);
        SetText(_titleText, GetOutcomeTitle(RaidResultsOutcome.Extracted));
        SetText(
            _statusText,
            isCommitConfirmed
                ? "El botín fue asegurado. Ya puedes volver al pueblo."
                : "Guardado pendiente. Esperando confirmación del botín extraído.");
        SetText(_controlsText, string.Empty);
        SetButtonState(_resumeButton, false, false);
        SetButtonState(_abandonButton, true, isCommitConfirmed);
        SetText(_abandonButtonText, "Volver al pueblo");
    }

    public void PresentExtractedState(ExtractionLootSaveStatus saveStatus)
    {
        SetProgressionResultsVisible(false);
        SetTitleEmphasis(true);
        SetText(_titleText, GetOutcomeTitle(RaidResultsOutcome.Extracted));
        string status = saveStatus switch
        {
            ExtractionLootSaveStatus.Committed => "El botín fue asegurado. Ya puedes volver al pueblo.",
            ExtractionLootSaveStatus.PersistenceFailed => "No se pudo guardar el botín. Pulsa Reanudar para reintentar.",
            _ => "Guardado pendiente. Esperando confirmación del botín extraído."
        };
        SetText(_statusText, status);
        SetText(_controlsText, string.Empty);

        bool retryVisible = saveStatus == ExtractionLootSaveStatus.PersistenceFailed;
        SetButtonState(_resumeButton, retryVisible, retryVisible);
        SetText(_resumeButtonText, "Reintentar");
        SetButtonState(
            _abandonButton,
            true,
            saveStatus == ExtractionLootSaveStatus.Committed);
        SetText(_abandonButtonText, "Volver al pueblo");
    }

    public void PresentAbandonConfirmation()
    {
        SetProgressionResultsVisible(false);
        SetTitleEmphasis(false);
        SetText(_titleText, "Abandonar incursión");
        SetText(
            _statusText,
            "Perderás el loot temporal. Pulsa Abandonar otra vez para confirmar o Reanudar para cancelar.");
        SetText(_controlsText, string.Empty);
        SetButtonState(_resumeButton, true, true);
        SetText(_resumeButtonText, "Cancelar");
        SetButtonState(_abandonButton, true, true);
        SetText(_abandonButtonText, "Confirmar abandono");
    }

    public void PresentProgressionResultsPending(
        string title,
        string persistenceFeedback,
        bool canSpectate,
        bool isSpectating,
        bool canRetryPersistence)
    {
        SetTitleEmphasis(true);
        SetText(_titleText, title);
        SetText(_statusText, persistenceFeedback);
        SetText(_controlsText, "Procesando resultados");
        SetProgressionResultsVisible(false);
        PresentTerminalActions(
            canReturn: false,
            returnRequested: false,
            canSpectate: canSpectate,
            isSpectating: isSpectating,
            canRetryPersistence: canRetryPersistence);
        SetCancelRaidVisible(false);
    }

    public void PresentProgressionResults(
        string title,
        in ExpeditionProgressionResult result,
        string persistenceFeedback,
        bool canReturn,
        bool returnRequested,
        bool canSpectate,
        bool isSpectating,
        bool canRetryPersistence)
    {
        SetTitleEmphasis(true);
        SetText(_titleText, title);
        SetText(
            _statusText,
            returnRequested
                ? $"{persistenceFeedback}\nRegreso solicitado."
                : persistenceFeedback);
        SetText(_controlsText, string.Empty);
        SetText(
            _progressionActivityText,
            $"Eliminaciones PvE: {result.PveKillCount}\n" +
            $"Eliminaciones PvP: {result.PvpKillCount}\n" +
            $"Asistencias PvE: {result.PveAssistCount}\n" +
            $"Asistencias PvP: {result.PvpAssistCount}\n" +
            $"Primeras aperturas: {result.FirstOpenChestCount}");
        SetText(
            _progressionExperienceText,
            $"Combate: {result.CombatExperience} XP\n" +
            $"Exploración: {result.ExplorationExperience} XP\n" +
            $"Loot: {result.LootExperience} XP\n" +
            $"Valor de Loot elegible: {result.EligibleExtractedLootValue}\n" +
            $"XP generada: {result.ProvisionalExperienceTotal}\n" +
            $"Conservación: {FormatRetentionPercentage(result.RetentionBasisPoints)}\n" +
            $"XP consolidada: {result.ConsolidatedExperience}");

        if (result.IsMaxLevel)
        {
            SetText(
                _progressionLevelText,
                $"Nivel {result.PreviousLevel} ({result.PreviousExperience} XP) → " +
                $"Nivel {result.ResultingLevel}");
            SetText(_progressionLevelStatusText, "Nivel máximo alcanzado");
            SetProgressionFill(1f);
        }
        else
        {
            SetText(
                _progressionLevelText,
                $"Nivel {result.PreviousLevel} ({result.PreviousExperience} XP) → " +
                $"Nivel {result.ResultingLevel}\n" +
                $"Progreso: {result.ResultingExperience} / " +
                $"{result.NextLevelExperienceRequirement} XP");
            SetText(
                _progressionLevelStatusText,
                result.LevelsGained > 0
                    ? result.LevelsGained == 1
                        ? "¡Subiste 1 nivel!"
                        : $"¡Subiste {result.LevelsGained} niveles!"
                    : "Sin ascenso de nivel");
            float fillAmount = result.NextLevelExperienceRequirement > 0
                ? Mathf.Clamp01(
                    (float)((double)result.ResultingExperience /
                    result.NextLevelExperienceRequirement))
                : 0f;
            SetProgressionFill(fillAmount);
        }

        SetProgressionResultsVisible(true);
        PresentTerminalActions(
            canReturn,
            returnRequested,
            canSpectate,
            isSpectating,
            canRetryPersistence);
        SetCancelRaidVisible(false);
    }

    public void SetCancelRaidVisible(bool visible)
    {
        SetButtonState(_cancelRaidButton, visible, visible);
        SetText(_cancelRaidButtonText, "Cancelar raid");
    }

    public void PresentSpectatorState(string profileId, bool hasTarget)
    {
        SetSpectatorBarVisible(true);
        SetText(
            _spectatorTargetText,
            hasTarget ? $"Observando: {profileId}" : "No hay jugadores para observar");
        if (_previousTargetButton != null)
        {
            _previousTargetButton.interactable = hasTarget;
        }
        if (_nextTargetButton != null)
        {
            _nextTargetButton.interactable = hasTarget;
        }
    }

    public void SetSpectatorBarVisible(bool visible)
    {
        if (_spectatorBarRoot != null && _spectatorBarRoot.activeSelf != visible)
        {
            _spectatorBarRoot.SetActive(visible);
        }
    }

    public void Clear()
    {
        SetText(_titleText, string.Empty);
        SetText(_statusText, string.Empty);
        SetText(_controlsText, string.Empty);
        SetMenuVisible(false);
        SetCancelRaidVisible(false);
        SetTitleEmphasis(false);
        SetProgressionResultsVisible(false);
        SetSpectatorBarVisible(false);
    }

    internal static string FormatRetentionPercentage(int basisPoints)
    {
        int wholePercentage = basisPoints / 100;
        int fractionalPercentage = basisPoints % 100;
        if (fractionalPercentage == 0)
        {
            return $"{wholePercentage}%";
        }

        if (fractionalPercentage % 10 == 0)
        {
            return $"{wholePercentage}.{fractionalPercentage / 10}%";
        }

        return $"{wholePercentage}.{fractionalPercentage:00}%";
    }

    private void PresentTerminalActions(
        bool canReturn,
        bool returnRequested,
        bool canSpectate,
        bool isSpectating,
        bool canRetryPersistence)
    {
        bool showPrimaryAction = canRetryPersistence || canSpectate;
        SetButtonState(_resumeButton, showPrimaryAction, showPrimaryAction);
        SetText(
            _resumeButtonText,
            canRetryPersistence
                ? "Reintentar"
                : isSpectating
                    ? "Continuar observando"
                    : "Observar");
        SetButtonState(_abandonButton, true, canReturn && !returnRequested);
        SetText(
            _abandonButtonText,
            returnRequested ? "Regreso solicitado" : "Volver al pueblo");
    }

    private void SetProgressionResultsVisible(bool visible)
    {
        UiPanelVisibility.SetVisible(_progressionResultsRoot, visible);
        if (!visible)
        {
            ClearResultsLoot();
            HideLevelUp();
        }
    }

    private void SetTitleEmphasis(bool emphasized)
    {
        if (_titleText == null || _baseTitleFontSize <= 0f)
        {
            return;
        }

        // Auto-size keeps long headlines such as "INCURSIÓN ABANDONADA" on one line inside the
        // title rect instead of wrapping over the controls below it.
        _titleText.enableAutoSizing = emphasized;
        _titleText.fontSizeMin = _baseTitleFontSize;
        _titleText.fontSizeMax = _baseTitleFontSize * _resultTitleFontScale;
        _titleText.fontSize = emphasized
            ? _titleText.fontSizeMax
            : _baseTitleFontSize;
    }

    private void PresentResultsLootSlots(
        RaidResultsLootSummary summary,
        LootDefinitionCatalog catalog,
        bool isReward)
    {
        if (_resultLootSlotTemplate == null || _resultLootGridRoot == null)
        {
            Debug.LogError(
                $"{nameof(RaidMenuView)} has no Results loot slot template or grid root.",
                this);
            return;
        }

        for (int index = 0; index < summary.Entries.Count; index++)
        {
            if (index == _resultLootSlots.Count)
            {
                _resultLootSlots.Add(
                    Instantiate(_resultLootSlotTemplate, _resultLootGridRoot));
            }

            RaidResultsLootEntry entry = summary.Entries[index];
            RaidResultLootSlotView slot = _resultLootSlots[index];
            slot.gameObject.SetActive(true);
            if (catalog.TryGetIndex(entry.LootId, out int catalogIndex) &&
                catalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
            {
                slot.Present(definition.Icon, definition.DisplayName, entry.Amount, isReward);
            }
            else
            {
                slot.Present(null, entry.LootId.Value, entry.Amount, isReward);
            }
        }

        for (int index = summary.Entries.Count; index < _resultLootSlots.Count; index++)
        {
            _resultLootSlots[index].gameObject.SetActive(false);
        }
    }

    private void SetProgressionFill(float fillAmount)
    {
        if (_progressionExperienceFill != null)
        {
            _progressionExperienceFill.fillAmount = fillAmount;
        }
    }

    private void BindButtonListeners()
    {
        BindButton(_resumeButton, OnResumeButtonClicked);
        BindButton(_abandonButton, OnAbandonButtonClicked);
        BindButton(_cancelRaidButton, OnCancelRaidButtonClicked);
        BindButton(_previousTargetButton, OnPreviousTargetButtonClicked);
        BindButton(_nextTargetButton, OnNextTargetButtonClicked);
    }

    private void UnbindButtonListeners()
    {
        _resumeButton?.onClick.RemoveListener(OnResumeButtonClicked);
        _abandonButton?.onClick.RemoveListener(OnAbandonButtonClicked);
        _cancelRaidButton?.onClick.RemoveListener(OnCancelRaidButtonClicked);
        _previousTargetButton?.onClick.RemoveListener(OnPreviousTargetButtonClicked);
        _nextTargetButton?.onClick.RemoveListener(OnNextTargetButtonClicked);
    }

    private void CacheButtonLabels()
    {
        _resumeButtonText ??= _resumeButton != null
            ? _resumeButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        _abandonButtonText ??= _abandonButton != null
            ? _abandonButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        _cancelRaidButtonText ??= _cancelRaidButton != null
            ? _cancelRaidButton.GetComponentInChildren<TMP_Text>(true)
            : null;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetButtonState(Button button, bool visible, bool interactable)
    {
        if (button == null)
        {
            return;
        }
        button.gameObject.SetActive(visible);
        button.interactable = visible && interactable;
    }

    private void OnResumeButtonClicked() => ResumeRequested?.Invoke();
    private void OnAbandonButtonClicked() => AbandonRequested?.Invoke();
    private void OnCancelRaidButtonClicked() => CancelRaidRequested?.Invoke();
    private void OnPreviousTargetButtonClicked() => PreviousTargetRequested?.Invoke();
    private void OnNextTargetButtonClicked() => NextTargetRequested?.Invoke();

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }
}

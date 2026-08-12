using System;
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
        "E — Interactuar\n" +
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

    [Header("Spectator")]
    [SerializeField] private GameObject _spectatorBarRoot;
    [SerializeField] private TMP_Text _spectatorTargetText;
    [SerializeField] private Button _previousTargetButton;
    [SerializeField] private Button _nextTargetButton;

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
    public GameObject SpectatorBarRoot => _spectatorBarRoot;
    public TMP_Text SpectatorTargetText => _spectatorTargetText;
    public Button PreviousTargetButton => _previousTargetButton;
    public Button NextTargetButton => _nextTargetButton;
    public bool IsOpen => _menuRoot != null && _menuRoot.activeSelf;

    private void Awake()
    {
        CacheButtonLabels();
        BindButtonListeners();
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

        if (_menuRoot != null && _menuRoot.activeSelf != visible)
        {
            _menuRoot.SetActive(visible);
        }
    }

    public void PresentAliveState()
    {
        SetText(_titleText, "Menú de Incursión");
        SetText(_statusText, "La simulación continúa en tiempo real.");
        SetText(_controlsText, DefaultControlsText);
        SetButtonState(_resumeButton, true, true);
        SetText(_resumeButtonText, "Reanudar");
        SetButtonState(_abandonButton, true, true);
        SetText(_abandonButtonText, "Abandonar Incursión");
        SetCancelRaidVisible(false);
    }

    /// <summary>Presents role-aware actions for a defeated local participant.</summary>
    public void PresentDefeatedState(bool canReturn, bool isSpectating)
    {
        SetText(_titleText, "Has sido Derrotado");
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
        SetText(_titleText, "Extracción completada");
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
        SetText(_titleText, "Extracción completada");
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
        SetSpectatorBarVisible(false);
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

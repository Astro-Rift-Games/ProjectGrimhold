using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the local in-raid pause, controls and defeat menu overlay using uGUI/TMP.
/// It owns UI visualization and button events, consuming serialized references without runtime allocations.
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

    [SerializeField]
    private GameObject _menuRoot;

    [SerializeField]
    private TMP_Text _titleText;

    [SerializeField]
    private TMP_Text _statusText;

    [SerializeField]
    private TMP_Text _controlsText;

    [SerializeField]
    private Button _resumeButton;

    [SerializeField]
    private Button _abandonButton;

    [SerializeField]
    private TMP_Text _abandonButtonText;

    [SerializeField]
    private Button _cancelRaidButton;

    [SerializeField]
    private TMP_Text _cancelRaidButtonText;

    /// <summary>Raised when the user clicks the resume button.</summary>
    public event Action ResumeRequested;

    /// <summary>Raised when the user clicks the abandon button.</summary>
    public event Action AbandonRequested;

    /// <summary>Raised when the Host requests cancellation of the entire raid.</summary>
    public event Action CancelRaidRequested;

    /// <summary>Gets the visual root controlled by this view.</summary>
    public GameObject MenuRoot => _menuRoot;

    /// <summary>Gets the title text component for verification.</summary>
    public TMP_Text TitleText => _titleText;

    /// <summary>Gets the status description text component for verification.</summary>
    public TMP_Text StatusText => _statusText;

    /// <summary>Gets the controls guide text component for verification.</summary>
    public TMP_Text ControlsText => _controlsText;

    /// <summary>Gets the resume button for verification.</summary>
    public Button ResumeButton => _resumeButton;

    /// <summary>Gets the abandon button for verification.</summary>
    public Button AbandonButton => _abandonButton;

    /// <summary>Gets the label used by the context-sensitive abandon/return action.</summary>
    public TMP_Text AbandonButtonText => _abandonButtonText;

    /// <summary>Gets the optional Host-only raid cancellation button.</summary>
    public Button CancelRaidButton => _cancelRaidButton;

    /// <summary>Indicates whether the menu root is currently active in hierarchy.</summary>
    public bool IsOpen => _menuRoot != null && _menuRoot.activeSelf;

    private void Awake()
    {
        CreateFallbackCancelButton();
        CacheButtonLabel();
        BindButtonListeners();
    }

    private void OnEnable()
    {
        BindButtonListeners();
    }

    private void OnDisable()
    {
        UnbindButtonListeners();
    }

    /// <summary>Shows or hides the menu root object.</summary>
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

    /// <summary>Presents the menu state for an active, living player.</summary>
    public void PresentAliveState()
    {
        SetText(_titleText, "Menú de Incursión");
        SetText(_statusText, "La simulación continúa en tiempo real.");
        SetText(_controlsText, DefaultControlsText);

        if (_resumeButton != null)
        {
            _resumeButton.gameObject.SetActive(true);
            _resumeButton.interactable = true;
        }

        if (_abandonButton != null)
        {
            _abandonButton.interactable = true;
        }
        SetText(_abandonButtonText, "Abandonar Incursión");
        SetCancelRaidVisible(false);
    }

    /// <summary>Presents the menu state for a defeated player.</summary>
    public void PresentDefeatedState()
    {
        SetText(_titleText, "Has sido Derrotado");
        SetText(_statusText, "Tu personaje ha caído en la incursión.");
        SetText(_controlsText, DefaultControlsText);

        if (_resumeButton != null)
        {
            _resumeButton.gameObject.SetActive(false);
        }

        if (_abandonButton != null)
        {
            _abandonButton.interactable = true;
        }
        SetText(_abandonButtonText, "Volver al pueblo");
    }

    /// <summary>
    /// Presents a successful extraction while TASK-80 confirms the matching stash commit.
    /// </summary>
    public void PresentExtractedState(bool isCommitConfirmed)
    {
        SetText(_titleText, "Extracción completada");
        SetText(
            _statusText,
            isCommitConfirmed
                ? "El botín fue asegurado. Ya puedes volver al pueblo."
                : "Guardado pendiente. Esperando confirmación del botín extraído.");
        SetText(_controlsText, string.Empty);

        if (_resumeButton != null)
        {
            _resumeButton.gameObject.SetActive(false);
        }

        if (_abandonButton != null)
        {
            _abandonButton.interactable = isCommitConfirmed;
        }

        SetText(_abandonButtonText, "Volver al pueblo");
    }

    /// <summary>
    /// Presents extraction progress and exposes the resume button as a local
    /// persistence retry only after a durable save failure.
    /// </summary>
    public void PresentExtractedState(ExtractionLootSaveStatus saveStatus)
    {
        SetText(_titleText, "Extraccion completada");
        string status = saveStatus switch
        {
            ExtractionLootSaveStatus.Committed => "El botin fue asegurado. Ya puedes volver al pueblo.",
            ExtractionLootSaveStatus.PersistenceFailed => "No se pudo guardar el botin. Pulsa Reanudar para reintentar.",
            _ => "Guardado pendiente. Esperando confirmacion del botin extraido."
        };
        SetText(_statusText, status);
        SetText(_controlsText, string.Empty);

        bool retryVisible = saveStatus == ExtractionLootSaveStatus.PersistenceFailed;
        if (_resumeButton != null)
        {
            _resumeButton.gameObject.SetActive(retryVisible);
            _resumeButton.interactable = retryVisible;
        }

        if (_abandonButton != null)
        {
            _abandonButton.interactable = saveStatus == ExtractionLootSaveStatus.Committed;
        }

        SetText(_abandonButtonText, "Volver al pueblo");
    }

    /// <summary>
    /// Presents a local confirmation before an abandonment request is sent to State Authority.
    /// </summary>
    public void PresentAbandonConfirmation()
    {
        SetText(_titleText, "Abandonar incursión");
        SetText(_statusText, "Perderás el loot temporal. Pulsa Abandonar otra vez para confirmar o Reanudar para cancelar.");
        SetText(_controlsText, string.Empty);

        if (_resumeButton != null)
        {
            _resumeButton.gameObject.SetActive(true);
            _resumeButton.interactable = true;
        }

        if (_abandonButton != null)
        {
            _abandonButton.interactable = true;
        }
        SetText(_abandonButtonText, "Confirmar abandono");
    }

    /// <summary>Presents the optional Host-only global cancellation action.</summary>
    public void SetCancelRaidVisible(bool visible)
    {
        if (_cancelRaidButton == null)
        {
            return;
        }

        _cancelRaidButton.gameObject.SetActive(visible);
        _cancelRaidButton.interactable = visible;
        SetText(_cancelRaidButtonText, "Cancelar raid");
    }

    /// <summary>Clears text fields and hides the menu root.</summary>
    public void Clear()
    {
        SetText(_titleText, string.Empty);
        SetText(_statusText, string.Empty);
        SetText(_controlsText, string.Empty);
        SetMenuVisible(false);
        SetCancelRaidVisible(false);
    }

    private void BindButtonListeners()
    {
        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveListener(OnResumeButtonClicked);
            _resumeButton.onClick.AddListener(OnResumeButtonClicked);
        }

        if (_abandonButton != null)
        {
            _abandonButton.onClick.RemoveListener(OnAbandonButtonClicked);
            _abandonButton.onClick.AddListener(OnAbandonButtonClicked);
        }

        if (_cancelRaidButton != null)
        {
            _cancelRaidButton.onClick.RemoveListener(OnCancelRaidButtonClicked);
            _cancelRaidButton.onClick.AddListener(OnCancelRaidButtonClicked);
        }
    }

    private void CacheButtonLabel()
    {
        if (_abandonButtonText == null && _abandonButton != null)
        {
            _abandonButtonText = _abandonButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_cancelRaidButtonText == null && _cancelRaidButton != null)
        {
            _cancelRaidButtonText = _cancelRaidButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void CreateFallbackCancelButton()
    {
        if (_cancelRaidButton != null || _abandonButton == null || _abandonButton.transform.parent == null)
        {
            return;
        }

        _cancelRaidButton = Instantiate(_abandonButton, _abandonButton.transform.parent);
        _cancelRaidButton.name = "CancelRaidButton";
        RectTransform sourceRect = _abandonButton.transform as RectTransform;
        RectTransform cancelRect = _cancelRaidButton.transform as RectTransform;
        if (sourceRect != null && cancelRect != null)
        {
            cancelRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -40f);
        }

        _cancelRaidButtonText = _cancelRaidButton.GetComponentInChildren<TMP_Text>(true);
        SetCancelRaidVisible(false);
    }

    private void UnbindButtonListeners()
    {
        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveListener(OnResumeButtonClicked);
        }

        if (_abandonButton != null)
        {
            _abandonButton.onClick.RemoveListener(OnAbandonButtonClicked);
        }

        if (_cancelRaidButton != null)
        {
            _cancelRaidButton.onClick.RemoveListener(OnCancelRaidButtonClicked);
        }
    }

    private void OnResumeButtonClicked()
    {
        ResumeRequested?.Invoke();
    }

    private void OnAbandonButtonClicked()
    {
        AbandonRequested?.Invoke();
    }

    private void OnCancelRaidButtonClicked()
    {
        CancelRaidRequested?.Invoke();
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }
}

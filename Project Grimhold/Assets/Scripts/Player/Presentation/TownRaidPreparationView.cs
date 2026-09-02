using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-only Town view for creating or joining a raid through a six-digit session code.
/// It owns no connection state and emits explicit UI intentions to its presenter.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownRaidPreparationView : MonoBehaviour
{
    public const string ResourcesPrefabName = "TownRaidPreparationView";
    private GameObject _promptRoot;
    private TMP_Text _promptText;
    private GameObject _panelRoot;
    private TMP_Text _statusText;
    private TMP_InputField _codeInput;
    private Button _createButton;
    private Button _joinButton;
    private Button _copyButton;
    private Button _readyButton;
    private Button _startButton;
    private Button _leaveButton;
    private bool _localReady;
    private Button _closeButton;
    private string _rejectionNotice;

    public event Action<string> CreateRequested;
    public event Action<string> JoinRequested;
    public event Action<bool> ReadyRequested;
    public event Action StartRequested;
    public event Action LeaveRequested;
    public event Action CloseRequested;

    public bool IsPanelOpen => _panelRoot != null && _panelRoot.activeSelf;

    public static TownRaidPreparationView Create(Transform owner)
    {
        TownRaidPreparationView prefab = Resources.Load<TownRaidPreparationView>(ResourcesPrefabName);
        if (prefab == null)
        {
            Debug.LogError(
                $"[{nameof(TownRaidPreparationView)}] Missing required Resources prefab " +
                $"'{ResourcesPrefabName}.prefab'.");
            return null;
        }

        TownRaidPreparationView instance = UnityEngine.Object.Instantiate(prefab, owner, false);
        instance.name = prefab.name;
        instance.CacheSerializedReferences();
        return instance;
    }

#if UNITY_EDITOR
    public static TownRaidPreparationView CreateEditorSource(Transform owner)
    {
        var root = new GameObject(
            "TownRaidHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(TownRaidPreparationView));
        root.transform.SetParent(owner, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TownRaidPreparationView view = root.GetComponent<TownRaidPreparationView>();
        view.Build();
        return view;
    }
#endif

    private void Awake()
    {
        CacheSerializedReferences();
    }

    private void CacheSerializedReferences()
    {
        _promptRoot = FindChild("InteractionPrompt");
        _promptText = FindText("InteractionPrompt/PromptText");
        _panelRoot = FindChild("RaidCodePanel");
        _statusText = FindText("RaidCodePanel/Status");
        _codeInput = FindComponent<TMP_InputField>("RaidCodePanel/RaidCodeInput");
        _createButton = FindComponent<Button>("RaidCodePanel/Crear raid");
        _joinButton = FindComponent<Button>("RaidCodePanel/Unirse con este código");
        _copyButton = FindComponent<Button>("RaidCodePanel/Copiar código");
        _readyButton = FindComponent<Button>("RaidCodePanel/Ready");
        _startButton = FindComponent<Button>("RaidCodePanel/Iniciar raid");
        _leaveButton = FindComponent<Button>("RaidCodePanel/Abandonar preparacion");
        _closeButton = FindComponent<Button>("RaidCodePanel/Cerrar");

        if (_panelRoot == null || _statusText == null || _codeInput == null)
        {
            return;
        }

        WireButton(_createButton, () => CreateRequested?.Invoke(null));
        WireButton(_joinButton, () => Submit(JoinRequested));
        WireButton(_copyButton, CopyCode);
        WireButton(_readyButton, () => ReadyRequested?.Invoke(!_localReady));
        WireButton(_startButton, () => StartRequested?.Invoke());
        WireButton(_leaveButton, () => LeaveRequested?.Invoke());
        WireButton(_closeButton, () => CloseRequested?.Invoke());
    }

    private GameObject FindChild(string path) => transform.Find(path)?.gameObject;

    private T FindComponent<T>(string path) where T : Component => transform.Find(path)?.GetComponent<T>();

    private TMP_Text FindText(string path) => FindComponent<TMP_Text>(path);

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void SetPrompt(bool visible, string action)
    {
        if (_promptRoot == null || _promptText == null)
        {
            return;
        }

        _promptRoot.SetActive(visible && !IsPanelOpen);
        if (visible)
        {
            _promptText.text = $"E — {(string.IsNullOrWhiteSpace(action) ? "Interactuar" : action)}";
        }
    }

    public void Open()
    {
        _promptRoot?.SetActive(false);
        _panelRoot?.SetActive(true);
        _rejectionNotice = null;
        ShowStatus("Creá una raid o ingresá el código de una sesión existente.");
        SetBusy(false);
    }

    public void PresentNoPreparation()
    {
        _localReady = false;
        if (_codeInput != null)
        {
            _codeInput.text = string.Empty;
            _codeInput.interactable = true;
        }

        ShowStatus("Creá una raid o ingresá el código de una sesión existente.");
        if (_createButton != null) _createButton.gameObject.SetActive(true);
        if (_joinButton != null) _joinButton.gameObject.SetActive(true);
        if (_copyButton != null) _copyButton.gameObject.SetActive(false);
        if (_readyButton != null) _readyButton.gameObject.SetActive(false);
        if (_startButton != null) _startButton.gameObject.SetActive(false);
        if (_leaveButton != null) _leaveButton.gameObject.SetActive(false);
    }

    public void Close()
    {
        _panelRoot?.SetActive(false);
    }

    public void SetBusy(bool busy, string status = null)
    {
        if (_codeInput != null)
        {
            _codeInput.interactable = !busy;
        }

        if (_createButton != null) _createButton.interactable = !busy;
        if (_joinButton != null) _joinButton.interactable = !busy;
        if (_copyButton != null) _copyButton.interactable = !busy;
        if (_readyButton != null) _readyButton.interactable = !busy;
        if (_startButton != null) _startButton.interactable = !busy;
        if (_leaveButton != null) _leaveButton.interactable = !busy;
        if (_closeButton != null) _closeButton.interactable = !busy;

        if (!string.IsNullOrWhiteSpace(status))
        {
            ShowStatus(status);
        }
    }

    public void PresentPreparation(in TownRaidPreparationPresentation presentation)
    {
        TownRaidPreparationSnapshot snapshot = presentation.Snapshot;
        _localReady = presentation.LocalReady;
        if (_codeInput != null)
        {
            _codeInput.text = snapshot.RaidCode.Value;
            _codeInput.interactable = false;
        }

        var status = new System.Text.StringBuilder();
        status.Append("Código: ").Append(snapshot.RaidCode.Value)
            .Append("  Jugadores: ").Append(snapshot.Members.Count)
            .Append(" / ").Append(RaidSessionRules.MaxParticipants).AppendLine();
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            TownRaidPreparationMember member = snapshot.Members[index];
            status.Append(member.ProfileId.Value)
                .Append(member.IsReady ? " — Ready" : " — No Ready")
                .AppendLine();
        }

        status.Append(presentation.IsHost ? "Preparación del Host." : "Esperando al Host...");
        ShowStatus(status.ToString());
        if (_createButton != null) _createButton.gameObject.SetActive(false);
        if (_joinButton != null) _joinButton.gameObject.SetActive(false);
        if (_copyButton != null) _copyButton.gameObject.SetActive(true);
        if (_readyButton != null)
        {
            _readyButton.gameObject.SetActive(true);
            _readyButton.GetComponentInChildren<TMP_Text>().text = presentation.LocalReady ? "Cancelar Ready" : "Ready";
        }
        if (_startButton != null)
        {
            _startButton.gameObject.SetActive(presentation.IsHost);
            _startButton.interactable = presentation.CanStart;
        }
        if (_leaveButton != null) _leaveButton.gameObject.SetActive(true);
    }

    public void ShowInvalidCode()
    {
        ShowStatus("El código debe tener exactamente 6 números.");
    }

    public void ShowTransitionFailure(SessionTransitionResult result)
    {
        SetBusy(false);
        ShowStatus($"No se pudo conectar a la raid: {result}.");
    }

    /// <summary>
    /// Reports why the local expedition preparation was rejected. The cohort dissolves a moment
    /// later and repaints the panel, so the notice sticks until the player reopens it.
    /// </summary>
    public void ShowPreparationRejected(ExpeditionPreparationResult reason)
    {
        SetBusy(false);
        _rejectionNotice = Describe(reason);
        ShowStatus(_rejectionNotice);
    }

    private static string Describe(ExpeditionPreparationResult reason) => reason switch
    {
        ExpeditionPreparationResult.InvalidPreparedWeapon =>
            "La preparación de arma no es válida. Revisá tus Weapon Slots en el Stash.",
        ExpeditionPreparationResult.AttributeRequirementsNotMet =>
            "Tus atributos actuales no cumplen los requisitos del arma preparada.",
        ExpeditionPreparationResult.RecoveryWeaponUnavailable =>
            "No tenés ningún arma preparada y el arma base de recuperación no está configurada.",
        ExpeditionPreparationResult.LoadoutFull =>
            "No hay espacio en el Loadout para preparar un arma.",
        ExpeditionPreparationResult.ReservationFailed =>
            "No se pudo reservar el loadout de la expedición.",
        ExpeditionPreparationResult.ProfileUnavailable =>
            "El perfil local no está disponible.",
        _ => "No se pudo preparar la expedición."
    };

    private void Build()
    {
        _promptRoot = CreatePanel("InteractionPrompt", transform, new Vector2(0f, 110f), new Vector2(430f, 64f));
        _promptText = CreateText("PromptText", _promptRoot.transform, 28f, TextAlignmentOptions.Center);

        _panelRoot = CreatePanel("RaidCodePanel", transform, Vector2.zero, new Vector2(620f, 440f));
        var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = CreateText("Title", _panelRoot.transform, 34f, TextAlignmentOptions.Center);
        title.text = "Expedición";
        SetPreferredHeight(title.gameObject, 50f);

        _statusText = CreateText("Status", _panelRoot.transform, 21f, TextAlignmentOptions.Center);
        SetPreferredHeight(_statusText.gameObject, 62f);

        _codeInput = CreateCodeInput(_panelRoot.transform);
        _createButton = CreateButton(
            "Crear raid",
            _panelRoot.transform,
            () => CreateRequested?.Invoke(null),
            out _);
        _joinButton = CreateButton(
            "Unirse con este código",
            _panelRoot.transform,
            () => Submit(JoinRequested),
            out _);
        _copyButton = CreateButton("Copiar código", _panelRoot.transform, CopyCode, out _);
        _readyButton = CreateButton("Ready", _panelRoot.transform, () => ReadyRequested?.Invoke(!_localReady), out _);
        _startButton = CreateButton("Iniciar raid", _panelRoot.transform, () => StartRequested?.Invoke(), out _);
        _leaveButton = CreateButton("Abandonar preparacion", _panelRoot.transform, () => LeaveRequested?.Invoke(), out _);
        _closeButton = CreateButton("Cerrar", _panelRoot.transform, () => CloseRequested?.Invoke(), out _);

        _panelRoot.SetActive(false);
        _promptRoot.SetActive(false);
        _readyButton.gameObject.SetActive(false);
        _startButton.gameObject.SetActive(false);
        _leaveButton.gameObject.SetActive(false);
    }

    private void Submit(Action<string> action)
    {
        if (!RaidCode.TryParse(_codeInput?.text, out RaidCode raidCode))
        {
            ShowInvalidCode();
            return;
        }

        action?.Invoke(raidCode.Value);
    }

    private void CopyCode()
    {
        if (!RaidCode.TryParse(_codeInput?.text, out RaidCode raidCode))
        {
            ShowInvalidCode();
            return;
        }

        GUIUtility.systemCopyBuffer = raidCode.Value;
        ShowStatus($"Código {raidCode.Value} copiado.");
    }

    private void ShowStatus(string status)
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.text = string.IsNullOrEmpty(_rejectionNotice) || status == _rejectionNotice
            ? status
            : $"{_rejectionNotice}\n{status}";
    }

    private static TMP_InputField CreateCodeInput(Transform parent)
    {
        var inputObject = new GameObject(
            "RaidCodeInput",
            typeof(RectTransform),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.18f, 1f);
        SetPreferredHeight(inputObject, 58f);

        TMP_Text text = CreateText("Text", inputObject.transform, 28f, TextAlignmentOptions.Center);
        TMP_Text placeholder = CreateText("Placeholder", inputObject.transform, 22f, TextAlignmentOptions.Center);
        placeholder.text = "Código de 6 números";
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = RaidCode.Length;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.07f, 0.94f);
        return panel;
    }

    private static TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(14f, 8f);
        rect.offsetMax = new Vector2(-14f, -8f);
        return text;
    }

    private static Button CreateButton(string label, Transform parent, Action callback, out TMP_Text labelText)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => callback());
        SetPreferredHeight(buttonObject, 48f);
        labelText = CreateText("Label", buttonObject.transform, 21f, TextAlignmentOptions.Center);
        labelText.text = label;
        return button;
    }

    private static void SetPreferredHeight(GameObject target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = target.AddComponent<LayoutElement>();
        }

        element.preferredHeight = height;
    }
}

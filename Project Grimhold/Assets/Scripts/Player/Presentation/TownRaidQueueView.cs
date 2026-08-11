using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-only Town view for creating or joining a raid through a six-digit session code.
/// It owns no connection state and emits explicit UI intentions to its presenter.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownRaidQueueView : MonoBehaviour
{
    private GameObject _promptRoot;
    private TMP_Text _promptText;
    private GameObject _panelRoot;
    private TMP_Text _statusText;
    private TMP_InputField _codeInput;
    private Button _createButton;
    private Button _joinButton;
    private Button _copyButton;
    private Button _closeButton;

    public event Action<string> CreateRequested;
    public event Action<string> JoinRequested;
    public event Action CloseRequested;

    public bool IsPanelOpen => _panelRoot != null && _panelRoot.activeSelf;

    public static TownRaidQueueView Create(Transform owner)
    {
        var root = new GameObject(
            "TownRaidHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(TownRaidQueueView));
        root.transform.SetParent(owner, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TownRaidQueueView view = root.GetComponent<TownRaidQueueView>();
        view.Build();
        return view;
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
        if (_codeInput != null && string.IsNullOrWhiteSpace(_codeInput.text))
        {
            _codeInput.text = RaidLaunchManifest.Code.Generate();
        }

        ShowStatus("Creá una raid o ingresá el código de una sesión existente.");
        SetBusy(false);
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
        if (_closeButton != null) _closeButton.interactable = !busy;

        if (!string.IsNullOrWhiteSpace(status))
        {
            ShowStatus(status);
        }
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
            "Crear raid con este código",
            _panelRoot.transform,
            () => Submit(CreateRequested),
            out _);
        _joinButton = CreateButton(
            "Unirse con este código",
            _panelRoot.transform,
            () => Submit(JoinRequested),
            out _);
        _copyButton = CreateButton("Copiar código", _panelRoot.transform, CopyCode, out _);
        _closeButton = CreateButton("Cerrar", _panelRoot.transform, () => CloseRequested?.Invoke(), out _);

        _panelRoot.SetActive(false);
        _promptRoot.SetActive(false);
    }

    private void Submit(Action<string> action)
    {
        if (!RaidLaunchManifest.Code.TryNormalize(_codeInput?.text, out string code))
        {
            ShowInvalidCode();
            return;
        }

        action?.Invoke(code);
    }

    private void CopyCode()
    {
        if (!RaidLaunchManifest.Code.TryNormalize(_codeInput?.text, out string code))
        {
            ShowInvalidCode();
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        ShowStatus($"Código {code} copiado.");
    }

    private void ShowStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.text = status;
        }
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
        input.characterLimit = RaidLaunchManifest.Code.Length;
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

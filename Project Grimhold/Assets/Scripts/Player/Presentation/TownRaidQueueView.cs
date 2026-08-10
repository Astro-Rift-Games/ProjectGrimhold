using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-only runtime view for the Town interaction prompt and raid queue.
/// It owns no gameplay or network state and only emits explicit UI intentions.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownRaidQueueView : MonoBehaviour
{
    private GameObject _promptRoot;
    private TMP_Text _promptText;
    private GameObject _panelRoot;
    private TMP_Text _statusText;
    private TMP_Text _membersText;
    private Button _createButton;
    private Button _joinButton;
    private Button _leaveButton;
    private Button _readyButton;
    private Button _launchButton;
    private TMP_Text _readyButtonText;
    private bool _localReady;

    public event Action CreateRequested;
    public event Action JoinRequested;
    public event Action LeaveRequested;
    public event Action<bool> ReadyRequested;
    public event Action LaunchRequested;
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
    }

    public void Close()
    {
        _panelRoot?.SetActive(false);
    }

    public void ShowTransportFailure()
    {
        if (_statusText != null)
        {
            _statusText.text = "No se pudo enviar la solicitud.";
        }
    }

    public void Refresh(in TownRaidQueueSnapshot snapshot, ProfileId localProfile)
    {
        bool isMember = false;
        bool isReady = false;
        bool allReady = snapshot.Members.Count > 0;
        var membersBuilder = new StringBuilder(128);
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            TownRaidQueueMember member = snapshot.Members[index];
            bool isLocal = member.ProfileId == localProfile;
            isMember |= isLocal;
            if (isLocal)
            {
                isReady = member.IsReady;
            }

            allReady &= member.IsReady;
            membersBuilder.Append(member.ProfileId == snapshot.HostProfileId ? "Host · " : "Miembro · ");
            membersBuilder.Append(member.ProfileId.Value);
            membersBuilder.Append(member.IsReady ? " · Ready" : " · No listo");
            membersBuilder.AppendLine();
        }

        if (_membersText != null)
        {
            _membersText.text = membersBuilder.Length > 0 ? membersBuilder.ToString() : "No hay una expedición formada.";
        }

        bool isHost = isMember && snapshot.HostProfileId == localProfile;
        _localReady = isReady;
        if (_statusText != null)
        {
            _statusText.text = snapshot.State switch
            {
                TownRaidQueueState.Empty => "Creá una expedición o esperá a que otro jugador la forme.",
                TownRaidQueueState.Forming => "La expedición está esperando a que todos estén Ready.",
                TownRaidQueueState.Launching => "Preparando la raid…",
                _ => "Estado de cola no disponible."
            };
        }

        SetButton(_createButton, snapshot.State == TownRaidQueueState.Empty);
        SetButton(_joinButton, snapshot.State == TownRaidQueueState.Forming && !isMember);
        SetButton(_leaveButton, snapshot.State == TownRaidQueueState.Forming && isMember);
        SetButton(_readyButton, snapshot.State == TownRaidQueueState.Forming && isMember);
        SetButton(_launchButton, snapshot.State == TownRaidQueueState.Forming && isHost && allReady);
        if (_readyButtonText != null)
        {
            _readyButtonText.text = isReady ? "No estoy Ready" : "Estoy Ready";
        }
    }

    private void Build()
    {
        _promptRoot = CreatePanel("InteractionPrompt", transform, new Vector2(0f, 110f), new Vector2(430f, 64f));
        _promptText = CreateText("PromptText", _promptRoot.transform, 28f, TextAlignmentOptions.Center);

        _panelRoot = CreatePanel("RaidQueuePanel", transform, Vector2.zero, new Vector2(620f, 620f));
        var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = CreateText("Title", _panelRoot.transform, 34f, TextAlignmentOptions.Center);
        title.text = "Expedición";
        SetPreferredHeight(title.gameObject, 50f);
        _statusText = CreateText("Status", _panelRoot.transform, 22f, TextAlignmentOptions.Center);
        SetPreferredHeight(_statusText.gameObject, 70f);
        _membersText = CreateText("Members", _panelRoot.transform, 19f, TextAlignmentOptions.TopLeft);
        SetPreferredHeight(_membersText.gameObject, 180f);

        _createButton = CreateButton("Crear expedición", _panelRoot.transform, () => CreateRequested?.Invoke(), out _);
        _joinButton = CreateButton("Unirse", _panelRoot.transform, () => JoinRequested?.Invoke(), out _);
        _leaveButton = CreateButton("Salir de la cola", _panelRoot.transform, () => LeaveRequested?.Invoke(), out _);
        _readyButton = CreateButton("Estoy Ready", _panelRoot.transform, () => ReadyRequested?.Invoke(!_localReady), out _readyButtonText);
        _launchButton = CreateButton("Lanzar raid", _panelRoot.transform, () => LaunchRequested?.Invoke(), out _);
        CreateButton("Cerrar", _panelRoot.transform, () => CloseRequested?.Invoke(), out _);
        _panelRoot.SetActive(false);
        _promptRoot.SetActive(false);
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

    private static void SetButton(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }
}

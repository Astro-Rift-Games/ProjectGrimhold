using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display data for a single selectable training weapon in the trainer UI.
/// Decouples view rendering from asset catalogs.
/// </summary>
public readonly struct TrainingWeaponOption
{
    public readonly LootId LootId;
    public readonly string DisplayName;
    public readonly string Archetype;
    public readonly string Details;
    public readonly Sprite Icon;

    public TrainingWeaponOption(LootId lootId, string displayName, string archetype, string details, Sprite icon = null)
    {
        LootId = lootId;
        DisplayName = displayName;
        Archetype = archetype;
        Details = details;
        Icon = icon;
    }

    public static TrainingWeaponOption FromDefinition(LootDefinition def)
    {
        if (def == null) return default;

        string archetype = string.Empty;
        string details = def.Description;

        if (def.WeaponDefinition != null)
        {
            WeaponDefinition weapon = def.WeaponDefinition;
            string hand = weapon.Handedness == WeaponHandedness.TwoHanded ? "2 Manos" : "1 Mano";
            string dmgType = weapon.DamageType == DamageType.Magical ? "Magia" : "Físico";
            archetype = $"{hand} • {dmgType}";

            var requirements = weapon.AttributeRequirements;
            var minimums = new List<string>();
            if (requirements.MinimumStrength > 0) minimums.Add($"FUE {requirements.MinimumStrength}");
            if (requirements.MinimumDexterity > 0) minimums.Add($"DES {requirements.MinimumDexterity}");
            if (requirements.MinimumIntelligence > 0) minimums.Add($"INT {requirements.MinimumIntelligence}");
            string requirementText = minimums.Count == 0 ? "Ninguno" : string.Join(" · ", minimums);
            details = $"Daño: {weapon.BaseDamage}\n{hand}\nRequisitos: {requirementText}";
            if (!string.IsNullOrWhiteSpace(def.Description)) details += $"\n{def.Description}";
        }

        string displayName = string.IsNullOrWhiteSpace(def.DisplayName) ? def.Id : def.DisplayName;
        return new TrainingWeaponOption(def.LootId, displayName, archetype, details, def.Icon);
    }
}

/// <summary>
/// Passive presentation layer for the Town Trainer weapon selection screen.
/// Data-driven: dynamically renders cards for each training weapon supplied by the presenter.
/// Allows the player to choose one of the training weapons to satisfy contingency requirements (Doc 09 §15).
/// </summary>
[DisallowMultipleComponent]
public sealed class TownTrainerView : MonoBehaviour
{
    private const int OverlaySortingOrder = 110;

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private GameObject _scrim;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _subtitleText;
    [SerializeField] private TMP_Text _feedbackText;
    [SerializeField] private Transform _cardsContainer;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _scrimCloseButton;

    private readonly List<GameObject> _spawnedCards = new();

    public static readonly LootId ShortSwordId = new("training_short_sword");
    public static readonly LootId DaggerId = new("training_dagger");
    public static readonly LootId WandId = new("training_wand");

    public bool IsOpen => UiPanelVisibility.IsVisible(_panelRoot != null ? _panelRoot : gameObject);

    public event Action<LootId> WeaponSelected;
    public event Action CloseRequested;

    public void SetCanSelect(bool canSelect)
    {
        foreach (var card in _spawnedCards)
            if (card != null)
                foreach (var button in card.GetComponentsInChildren<Button>(true)) button.interactable = canSelect;
    }

    /// <summary>
    /// Creates and returns a fully initialized TownTrainerView under the given owner transform.
    /// </summary>
    public static TownTrainerView Create(Transform owner)
    {
        var root = new GameObject(
            "TownTrainerHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(TownTrainerView));

        if (owner != null)
        {
            root.transform.SetParent(owner, false);
        }

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TownTrainerView view = root.GetComponent<TownTrainerView>();
        view.BuildUI();
        view.Close();
        return view;
    }

    private void Awake()
    {
        if (_panelRoot == null)
        {
            BuildUI();
        }
        else
        {
            EnsureScrimReference();
            WireButtons();
        }
    }

    public void Open()
    {
        EnsureScrimReference();
        if (_scrim != null)
        {
            UiPanelVisibility.SetVisible(_scrim, true);
        }

        UiPanelVisibility.SetVisible(_panelRoot != null ? _panelRoot : gameObject, true);
    }

    public void Close()
    {
        EnsureScrimReference();
        if (_scrim != null)
        {
            UiPanelVisibility.SetVisible(_scrim, false);
        }

        UiPanelVisibility.SetVisible(_panelRoot != null ? _panelRoot : gameObject, false);
    }

    private void EnsureScrimReference()
    {
        if (_scrim == null)
        {
            if (_scrimCloseButton != null)
            {
                _scrim = _scrimCloseButton.gameObject;
            }
            else
            {
                _scrim = transform.Find("DarkScrim")?.gameObject;
            }
        }
    }

    public void SetFeedback(string text, Color color)
    {
        if (_feedbackText != null)
        {
            _feedbackText.text = text ?? string.Empty;
            _feedbackText.color = color;
        }
    }

    /// <summary>
    /// Dynamically creates weapon cards for each supplied option.
    /// </summary>
    public void Present(IReadOnlyList<TrainingWeaponOption> options)
    {
        ClearCards();

        if (options == null || options.Count == 0)
        {
            SetFeedback("No hay armas de entrenamiento disponibles.", new Color(1f, 0.4f, 0.4f, 1f));
            return;
        }

        for (int i = 0; i < options.Count; i++)
        {
            CreateWeaponCard(options[i]);
        }
    }

    public void ClearCards()
    {
        for (int i = _spawnedCards.Count - 1; i >= 0; i--)
        {
            if (_spawnedCards[i] != null)
            {
                _spawnedCards[i].SetActive(false);
                _spawnedCards[i].transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(_spawnedCards[i]);
                else DestroyImmediate(_spawnedCards[i]);
            }
        }
        _spawnedCards.Clear();
    }

    private void WireButtons()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }

        if (_scrimCloseButton != null)
        {
            _scrimCloseButton.onClick.RemoveAllListeners();
            _scrimCloseButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }
    }

    public void BuildUI()
    {
        if (_panelRoot != null) return;

        // 1. Dark scrim background
        var scrimObj = new GameObject("DarkScrim", typeof(RectTransform), typeof(Image), typeof(Button));
        scrimObj.transform.SetParent(transform, false);
        RectTransform scrimRect = scrimObj.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.offsetMin = Vector2.zero;
        scrimRect.offsetMax = Vector2.zero;
        scrimObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        _scrimCloseButton = scrimObj.GetComponent<Button>();
        _scrim = scrimObj;

        // 2. Main modal panel
        _panelRoot = new GameObject("TrainerPanel", typeof(RectTransform), typeof(Image));
        _panelRoot.transform.SetParent(transform, false);
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(860f, 520f);
        _panelRoot.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.98f);

        // 3. Header title & subtitle
        _titleText = CreateText("Title", _panelRoot.transform, 28f, TextAlignmentOptions.Center);
        RectTransform titleRect = _titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(0f, 40f);
        _titleText.text = "Armas de Entrenamiento";
        _titleText.fontStyle = FontStyles.Bold;

        _subtitleText = CreateText("Subtitle", _panelRoot.transform, 16f, TextAlignmentOptions.Center);
        RectTransform subRect = _subtitleText.rectTransform;
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -64f);
        subRect.sizeDelta = new Vector2(0f, 30f);
        _subtitleText.text = "Selecciona un arma básica para volver a la batalla. Solo puedes portar 1 a la vez.";
        _subtitleText.color = new Color(0.7f, 0.75f, 0.8f, 1f);

        // 4. Cards container with HorizontalLayoutGroup
        var cardsContainer = new GameObject("CardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        cardsContainer.transform.SetParent(_panelRoot.transform, false);
        _cardsContainer = cardsContainer.transform;

        RectTransform cardsRect = cardsContainer.GetComponent<RectTransform>();
        cardsRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRect.pivot = new Vector2(0.5f, 0.5f);
        cardsRect.anchoredPosition = new Vector2(0f, 10f);
        cardsRect.sizeDelta = new Vector2(800f, 250f);

        var layout = cardsContainer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 5. Feedback line
        _feedbackText = CreateText("Feedback", _panelRoot.transform, 15f, TextAlignmentOptions.Center);
        RectTransform feedRect = _feedbackText.rectTransform;
        feedRect.anchorMin = new Vector2(0f, 0f);
        feedRect.anchorMax = new Vector2(1f, 0f);
        feedRect.pivot = new Vector2(0.5f, 0f);
        feedRect.anchoredPosition = new Vector2(0f, 75f);
        feedRect.sizeDelta = new Vector2(0f, 30f);
        _feedbackText.text = "Al elegir un arma, sustituirá automáticamente cualquier arma de entrenamiento previa.";
        _feedbackText.color = new Color(0.6f, 0.7f, 0.8f, 1f);

        // 6. Close button
        _closeButton = CreateButton(
            "Cerrar",
            _panelRoot.transform,
            new Vector2(0f, -215f),
            new Vector2(160f, 44f),
            new Color(0.22f, 0.28f, 0.36f, 1f));

        WireButtons();

        // The presenter supplies catalog options; no equipment definitions are invented here.
    }

    private void CreateWeaponCard(TrainingWeaponOption option)
    {
        if (_cardsContainer == null) return;

        var card = new GameObject("Card_" + option.DisplayName, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(_cardsContainer, false);
        _spawnedCards.Add(card);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(240f, 250f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f, 1f);

        // Name
        TMP_Text nameText = CreateText("Name", card.transform, 19f, TextAlignmentOptions.Center);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -14f);
        nameRect.sizeDelta = new Vector2(0f, 28f);
        nameText.text = option.DisplayName;
        nameText.fontStyle = FontStyles.Bold;

        // Archetype
        TMP_Text archText = CreateText("Archetype", card.transform, 13f, TextAlignmentOptions.Center);
        RectTransform archRect = archText.rectTransform;
        archRect.anchorMin = new Vector2(0f, 1f);
        archRect.anchorMax = new Vector2(1f, 1f);
        archRect.pivot = new Vector2(0.5f, 1f);
        archRect.anchoredPosition = new Vector2(0f, -42f);
        archRect.sizeDelta = new Vector2(0f, 22f);
        archText.text = option.Archetype ?? string.Empty;
        archText.color = new Color(0.4f, 0.8f, 1f, 1f);

        // Optional Icon
        float detailsTop = -68f;
        if (option.Icon != null)
        {
            var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(card.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -68f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            var iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = option.Icon;
            iconImg.preserveAspect = true;
            detailsTop = -112f;
        }

        // Details / Stats
        TMP_Text statsText = CreateText("Stats", card.transform, 12f, TextAlignmentOptions.Center);
        RectTransform statsRect = statsText.rectTransform;
        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.offsetMin = new Vector2(10f, 54f);
        statsRect.offsetMax = new Vector2(-10f, detailsTop);
        statsText.text = option.Details ?? string.Empty;
        statsText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        statsText.textWrappingMode = TextWrappingModes.Normal;

        // Select Button
        Button selectButton = CreateButton(
            "Elegir " + option.DisplayName,
            card.transform,
            new Vector2(0f, -98f),
            new Vector2(190f, 38f),
            new Color(0.18f, 0.40f, 0.28f, 1f));

        LootId chosenId = option.LootId;
        selectButton.onClick.AddListener(() => WeaponSelected?.Invoke(chosenId));
    }

    private static TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static Button CreateButton(
        string label,
        Transform parent,
        Vector2 position,
        Vector2 size,
        Color buttonColor)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = buttonColor;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text labelText = CreateText("Label", buttonObject.transform, 16f, TextAlignmentOptions.Center);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelText.text = label;

        return buttonObject.GetComponent<Button>();
    }
}

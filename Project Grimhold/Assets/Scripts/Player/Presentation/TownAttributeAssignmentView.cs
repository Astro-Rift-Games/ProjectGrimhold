using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local-only rendering surface for confirmed Town character attributes.</summary>
[DisallowMultipleComponent]
public sealed class TownAttributeAssignmentView : MonoBehaviour
{
    public const string ResourcesPrefabName = "TownAttributeAssignmentView";

    [SerializeField] private TMP_Text _availablePointsText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TownAttributeAssignmentRowView[] _rows;

    public TMP_Text AvailablePointsText => _availablePointsText;
    public Button CloseButton => _closeButton;
    public IReadOnlyList<TownAttributeAssignmentRowView> Rows => _rows;
    public bool IsOpen => gameObject.activeSelf;

    public event Action<CharacterAttribute> AssignmentRequested;
    public event Action CloseRequested;

    public static TownAttributeAssignmentView Create(Transform owner)
    {
        TownAttributeAssignmentView prefab = Resources.Load<TownAttributeAssignmentView>(ResourcesPrefabName);
        if (prefab == null)
        {
            return null;
        }

        TownAttributeAssignmentView instance = Instantiate(prefab, owner, false);
        instance.name = prefab.name;
        return instance;
    }

    private void Awake()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(RequestClose);
        }

        if (_rows == null)
        {
            return;
        }

        foreach (TownAttributeAssignmentRowView row in _rows)
        {
            if (row != null)
            {
                row.AssignmentRequested += OnAssignmentRequested;
            }
        }
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(RequestClose);
        }

        if (_rows == null)
        {
            return;
        }

        foreach (TownAttributeAssignmentRowView row in _rows)
        {
            if (row != null)
            {
                row.AssignmentRequested -= OnAssignmentRequested;
            }
        }
    }

    public void Present(in TownAttributeAssignmentPresentation presentation)
    {
        _availablePointsText.text = $"Puntos disponibles: {presentation.AvailablePoints}";
        foreach (TownAttributeAssignmentRowView row in _rows)
        {
            if (row != null && presentation.TryGet(row.Attribute, out int value, out bool canAssign))
            {
                row.Present(value, canAssign);
            }
        }
    }

    public void PresentUnavailable()
    {
        _availablePointsText.text = "Puntos disponibles: —";
        foreach (TownAttributeAssignmentRowView row in _rows)
        {
            row?.PresentUnavailable();
        }
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void OnAssignmentRequested(CharacterAttribute attribute) =>
        AssignmentRequested?.Invoke(attribute);

    private void RequestClose() => CloseRequested?.Invoke();
}

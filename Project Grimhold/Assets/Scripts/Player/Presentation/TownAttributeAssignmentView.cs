using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local-only rendering surface for confirmed Town character attributes.</summary>
[DisallowMultipleComponent]
public sealed class TownAttributeAssignmentView : MonoBehaviour
{
    [SerializeField] private TMP_Text _availablePointsText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TownAttributeAssignmentRowView[] _rows;
    [SerializeField] private TownEquipmentSlotView[] _equipmentSlots;
    [SerializeField] private RectTransform _statLinesRoot;
    [SerializeField] private TownStatLineView _headerLineTemplate;
    [SerializeField] private TownStatLineView _statLineTemplate;
    [SerializeField] private TownStatLineView _noteLineTemplate;

    private readonly List<GameObject> _spawnedLines = new();

    public TMP_Text AvailablePointsText => _availablePointsText;
    public Button CloseButton => _closeButton;
    public IReadOnlyList<TownAttributeAssignmentRowView> Rows => _rows;
    public bool IsOpen => gameObject.activeSelf;

    public event Action<CharacterAttribute> AssignmentRequested;
    public event Action CloseRequested;

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
        _availablePointsText.text = $"Available Points: {presentation.AvailablePoints}";
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
        _availablePointsText.text = "Available Points: —";
        foreach (TownAttributeAssignmentRowView row in _rows)
        {
            row?.PresentUnavailable();
        }
    }

    /// <summary>Shows the character sheet: derived statistics and what is prepared in each slot.</summary>
    public void PresentStatistics(in TownCharacterStatisticsPresentation statistics)
    {
        BuildLines(TownCharacterStatisticsLines.Build(statistics));
        if (_equipmentSlots == null)
        {
            return;
        }

        IReadOnlyList<TownEquipmentSlotEntry> entries = statistics.Slots;
        foreach (TownEquipmentSlotView slotView in _equipmentSlots)
        {
            if (slotView == null)
            {
                continue;
            }

            bool found = false;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Slot == slotView.Slot)
                {
                    slotView.Present(entries[index]);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                slotView.PresentEmpty();
            }
        }
    }

    /// <summary>Replaces the character sheet with a single note and empties the equipment slots.</summary>
    public void PresentStatisticsUnavailable()
    {
        BuildLines(new[] { new TownStatLine(TownStatLineKind.Note, "Statistics unavailable") });
        if (_equipmentSlots == null)
        {
            return;
        }

        foreach (TownEquipmentSlotView slotView in _equipmentSlots)
        {
            slotView?.PresentEmpty();
        }
    }

    private void BuildLines(IReadOnlyList<TownStatLine> lines)
    {
        foreach (GameObject spawned in _spawnedLines)
        {
            if (spawned == null)
            {
                continue;
            }

            // Deactivate first: Destroy is deferred in Play Mode and the row must stop counting immediately.
            spawned.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(spawned);
            }
            else
            {
                DestroyImmediate(spawned);
            }
        }

        _spawnedLines.Clear();
        if (_statLinesRoot == null)
        {
            return;
        }

        foreach (TownStatLine line in lines)
        {
            TownStatLineView template = line.Kind switch
            {
                TownStatLineKind.Header => _headerLineTemplate,
                TownStatLineKind.Note => _noteLineTemplate,
                _ => _statLineTemplate
            };
            if (template == null)
            {
                continue;
            }

            TownStatLineView row = Instantiate(template, _statLinesRoot, false);
            row.gameObject.SetActive(true);
            row.Present(line);
            _spawnedLines.Add(row.gameObject);
        }
    }

    /// <summary>Hides the panel's own close button when a host window provides one.</summary>
    public void SetCloseButtonVisible(bool visible)
    {
        if (_closeButton != null)
        {
            _closeButton.gameObject.SetActive(visible);
        }
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void OnAssignmentRequested(CharacterAttribute attribute) =>
        AssignmentRequested?.Invoke(attribute);

    private void RequestClose() => CloseRequested?.Invoke();
}

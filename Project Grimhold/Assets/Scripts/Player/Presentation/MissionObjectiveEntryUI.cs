using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single objective entry in the Mission Details panel.
/// </summary>
public sealed class MissionObjectiveEntryUI : MonoBehaviour
{
    [SerializeField] private Toggle _statusToggle;
    [SerializeField] private TMP_Text _descriptionText;

    /// <summary>
    /// Initializes the objective entry.
    /// </summary>
    /// <param name="description">The text describing the objective.</param>
    /// <param name="isCompleted">True if the objective is completed (checks the toggle).</param>
    public void Initialize(string description, bool isCompleted)
    {
        if (_descriptionText != null)
        {
            _descriptionText.text = description;
        }

        if (_statusToggle != null)
        {
            // We disable interactable so the user can't manually check/uncheck it.
            _statusToggle.interactable = false;
            _statusToggle.isOn = isCompleted;
        }
    }
}

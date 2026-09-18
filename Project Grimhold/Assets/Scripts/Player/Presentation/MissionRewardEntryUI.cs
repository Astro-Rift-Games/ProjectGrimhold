using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single reward entry in the Mission Details panel.
/// </summary>
public sealed class MissionRewardEntryUI : MonoBehaviour
{
    [SerializeField] private Image _rewardIcon;
    [SerializeField] private TMP_Text _rewardText;

    /// <summary>
    /// Initializes the reward entry.
    /// </summary>
    /// <param name="description">The text describing the reward.</param>
    /// <param name="icon">An optional icon sprite for the reward.</param>
    public void Initialize(string description, Sprite icon = null)
    {
        if (_rewardText != null)
        {
            _rewardText.text = description;
        }

        if (_rewardIcon != null)
        {
            if (icon != null)
            {
                _rewardIcon.sprite = icon;
                _rewardIcon.enabled = true;
            }
            else
            {
                _rewardIcon.enabled = false;
            }
        }
    }
}

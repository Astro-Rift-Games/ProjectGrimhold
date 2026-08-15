using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for individual stat prefabs inside the merchant shop or inventory.
/// </summary>
public sealed class StatInfoUI : MonoBehaviour
{
    [SerializeField] private Image _statIcon;
    [SerializeField] private TMP_Text _statNameText;
    [SerializeField] private TMP_Text _statValueText;

    /// <summary>
    /// Configures the UI for a specific stat.
    /// </summary>
    public void Setup(Sprite icon, string statName, string statValue)
    {
        if (_statIcon != null)
        {
            _statIcon.sprite = icon;
            _statIcon.gameObject.SetActive(icon != null);
        }
        
        if (_statNameText != null)
        {
            _statNameText.text = statName;
        }

        if (_statValueText != null)
        {
            _statValueText.text = statValue;
        }
    }
}

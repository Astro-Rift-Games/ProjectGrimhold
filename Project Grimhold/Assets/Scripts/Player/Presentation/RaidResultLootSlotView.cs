using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only slot of the Results loot grid. It has no drag, drop or click behavior, so it
/// can never move or modify the items it shows.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidResultLootSlotView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField, Tooltip("Shown only for loot recovered as an expedition reward.")]
    private GameObject _rewardHighlight;

    public void Present(Sprite icon, string displayName, int amount, bool isReward)
    {
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_nameText != null)
        {
            _nameText.text = displayName;
        }

        if (_amountText != null)
        {
            _amountText.text = amount > 1 ? $"x{amount}" : string.Empty;
        }

        if (_rewardHighlight != null)
        {
            _rewardHighlight.SetActive(isReward);
        }
    }
}

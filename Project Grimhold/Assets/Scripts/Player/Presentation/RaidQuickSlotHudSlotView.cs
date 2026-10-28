using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one Quick Slot of the Raid HUD. It owns only uGUI/TMP presentation and never reads or
/// modifies gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidQuickSlotHudSlotView : MonoBehaviour
{
    [SerializeField]
    private Image _icon;

    [SerializeField]
    private GameObject _emptyRoot;

    [SerializeField]
    private TMP_Text _keyLabel;

    [SerializeField]
    private TMP_Text _amountText;

    [SerializeField]
    private GameObject _unavailableOverlay;

    public void SetKeyLabel(string label)
    {
        if (_keyLabel != null)
        {
            _keyLabel.text = label;
        }
    }

    /// <summary>Presents an unassigned slot.</summary>
    public void PresentEmpty()
    {
        SetIcon(null);
        if (_icon != null)
        {
            _icon.color = Color.white;
        }
        SetActive(_emptyRoot, true);
        SetActive(_unavailableOverlay, false);
        if (_amountText != null)
        {
            _amountText.text = string.Empty;
        }
    }

    /// <summary>
    /// Presents a bound definition with the units available in the Inventory. An unavailable
    /// slot (no units, defeated or not in progress) keeps its icon, tints grayish, and shows the overlay.
    /// </summary>
    public void Present(Sprite icon, int amount, bool unavailable)
    {
        SetIcon(icon);
        if (_icon != null)
        {
            _icon.color = unavailable ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.white;
        }
        SetActive(_emptyRoot, false);
        SetActive(_unavailableOverlay, unavailable);
        if (_amountText != null)
        {
            _amountText.text = amount.ToString();
        }
    }

    private void SetIcon(Sprite sprite)
    {
        if (_icon == null)
        {
            return;
        }

        _icon.sprite = sprite;
        _icon.enabled = sprite != null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}

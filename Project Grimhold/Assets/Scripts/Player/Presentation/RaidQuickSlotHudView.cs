using TMPro;
using UnityEngine;

/// <summary>
/// The four Quick Slots of the Raid HUD plus a short-lived rejection message. The slot views are
/// authored in the prefab; this component only forwards presentation data to them.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidQuickSlotHudView : MonoBehaviour
{
    [SerializeField]
    private RaidQuickSlotHudSlotView[] _slots = new RaidQuickSlotHudSlotView[QuickSlotBindings.SlotCount];

    [SerializeField]
    private TMP_Text _messageText;

    [SerializeField, Min(0.1f)]
    private float _messageDuration = 1.5f;

    private float _messageUntil;

    public bool HasAllSlots
    {
        get
        {
            if (_slots == null || _slots.Length != QuickSlotBindings.SlotCount)
            {
                return false;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private void Awake()
    {
        if (!HasAllSlots)
        {
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].SetKeyLabel((i + 1).ToString());
        }

        HideMessage();
    }

    private void Update()
    {
        if (_messageUntil > 0f && Time.unscaledTime >= _messageUntil)
        {
            HideMessage();
        }
    }

    public RaidQuickSlotHudSlotView GetSlot(int slotIndex) => _slots[slotIndex];

    public void ShowMessage(string message)
    {
        if (_messageText == null)
        {
            return;
        }

        _messageText.text = message;
        _messageText.gameObject.SetActive(true);
        _messageUntil = Time.unscaledTime + _messageDuration;
    }

    public void HideMessage()
    {
        _messageUntil = 0f;
        if (_messageText != null)
        {
            _messageText.gameObject.SetActive(false);
        }
    }
}

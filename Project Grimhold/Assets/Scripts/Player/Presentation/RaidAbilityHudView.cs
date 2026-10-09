using UnityEngine;

/// <summary>
/// Hosts the two ability slot views of the Raid HUD. It owns only presentation and never reads or
/// modifies gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidAbilityHudView : MonoBehaviour
{
    [SerializeField]
    private RaidAbilityHudSlotView _slot1;

    [SerializeField]
    private RaidAbilityHudSlotView _slot2;

    public RaidAbilityHudSlotView Slot1 => _slot1;
    public RaidAbilityHudSlotView Slot2 => _slot2;

    public RaidAbilityHudSlotView For(UniversalAbilitySlot slot) =>
        slot == UniversalAbilitySlot.Slot2 ? _slot2 : _slot1;

    /// <summary>Restores both slots to the empty state and drops transient feedback.</summary>
    public void Clear()
    {
        if (_slot1 != null)
        {
            _slot1.Clear();
        }

        if (_slot2 != null)
        {
            _slot2.Clear();
        }
    }
}

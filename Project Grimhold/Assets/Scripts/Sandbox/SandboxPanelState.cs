#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using UnityEngine;

/// <summary>Tabs of the sandbox panel.</summary>
public enum SandboxPanelTab : byte
{
    Player = 0,
    Abilities = 1,
    Enemies = 2,
    Dummy = 3
}

/// <summary>
/// Pure UI state of the sandbox panel: selections, clamping and text parsing.
/// Holds no Unity scene or Fusion references so it can be tested in EditMode.
/// </summary>
public sealed class SandboxPanelState
{
    public const int NoAbility = -1;
    public const int MaxAttributeStep = 10;
    public const float MaxSpacing = 20f;

    public SandboxPanelTab Tab { get; private set; } = SandboxPanelTab.Player;
    public SandboxEnemyKind Kind { get; private set; } = SandboxEnemyKind.Melee;
    public SandboxSpawnPattern Pattern { get; private set; } = SandboxSpawnPattern.Ring;
    public int Count { get; private set; } = 1;
    public float Spacing { get; private set; } = 1.5f;
    public int AttributeStep { get; private set; } = 1;
    public WeaponSetSlot WeaponSet { get; set; } = WeaponSetSlot.SetA;
    public bool PreferOffHand { get; set; }
    public int Slot1Index { get; private set; } = NoAbility;
    public int Slot2Index { get; private set; } = NoAbility;

    public void SetTab(SandboxPanelTab tab)
    {
        if (Enum.IsDefined(typeof(SandboxPanelTab), tab))
        {
            Tab = tab;
        }
    }

    public void AdjustCount(int delta, int maxCount)
    {
        Count = SandboxSpawnPlanner.ClampCount(Count + delta, maxCount);
    }

    public void AdjustSpacing(float delta)
    {
        if (!float.IsFinite(delta))
        {
            return;
        }

        Spacing = Mathf.Clamp(Spacing + delta, SandboxSpawnPlanner.MinimumSpacing, MaxSpacing);
    }

    public void AdjustAttributeStep(int delta)
    {
        AttributeStep = Math.Clamp(AttributeStep + delta, 1, MaxAttributeStep);
    }

    public void CycleKind(int direction)
    {
        Kind = (SandboxEnemyKind)WrapIndex((int)Kind, direction, Enum.GetValues(typeof(SandboxEnemyKind)).Length);
    }

    public void CyclePattern(int direction)
    {
        Pattern = (SandboxSpawnPattern)WrapIndex(
            (int)Pattern, direction, Enum.GetValues(typeof(SandboxSpawnPattern)).Length);
    }

    /// <summary>
    /// Selects a catalog ability for Slot1 or Slot2. Entries without an execution behaviour and
    /// the same ability in both slots are rejected. <paramref name="slot"/> is 1 or 2.
    /// </summary>
    public bool TrySelectAbility(int slot, int catalogIndex, bool hasBehaviour, out string error)
    {
        error = null;
        if (slot != 1 && slot != 2)
        {
            error = $"Unknown ability slot {slot}.";
            return false;
        }

        if (catalogIndex < 0)
        {
            error = "Invalid ability index.";
            return false;
        }

        if (!hasBehaviour)
        {
            error = "This ability has no behaviour and cannot be equipped.";
            return false;
        }

        int other = slot == 1 ? Slot2Index : Slot1Index;
        if (other == catalogIndex)
        {
            error = "The same ability cannot occupy both slots.";
            return false;
        }

        if (slot == 1)
        {
            Slot1Index = catalogIndex;
        }
        else
        {
            Slot2Index = catalogIndex;
        }

        return true;
    }

    /// <summary>Replaces the selection with the indices currently applied to the player (negative = empty).</summary>
    public void SyncSlots(int slot1Index, int slot2Index)
    {
        Slot1Index = slot1Index < 0 ? NoAbility : slot1Index;
        Slot2Index = slot2Index < 0 ? NoAbility : slot2Index;
    }

    public void ClearSlot(int slot)
    {
        if (slot == 1)
        {
            Slot1Index = NoAbility;
        }
        else if (slot == 2)
        {
            Slot2Index = NoAbility;
        }
    }

    /// <summary>Wraps <paramref name="current"/> + <paramref name="delta"/> into [0, count); 0 for empty lists.</summary>
    public static int WrapIndex(int current, int delta, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = (current + delta) % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    /// <summary>Parses a finite number using the invariant culture.</summary>
    public static bool TryParseNumber(string text, out float value)
    {
        value = 0f;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
            !float.IsFinite(parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryParseVector(string x, string y, out Vector2 result)
    {
        result = default;
        if (!TryParseNumber(x, out float parsedX) || !TryParseNumber(y, out float parsedY))
        {
            return false;
        }

        result = new Vector2(parsedX, parsedY);
        return true;
    }
}
#endif

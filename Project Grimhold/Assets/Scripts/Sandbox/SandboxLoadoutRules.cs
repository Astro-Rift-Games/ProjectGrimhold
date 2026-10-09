#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

/// <summary>
/// Pure validation rules for the ability sandbox's live player customization.
/// Equipment compatibility is delegated to <see cref="EquipmentSlotRules"/>; this class
/// only adds the sandbox-specific selection and clamping decisions.
/// </summary>
public static class SandboxLoadoutRules
{
    /// <summary>Health may be set low for testing, but never to zero (that would be a defeat).</summary>
    public const float MinimumSetHealth = 1f;

    /// <summary>
    /// Validates a Slot1/Slot2 pair. Empty (invalid) ids clear a slot; a non-empty id must be
    /// equippable (known to the catalog and backed by an execution behaviour) and may not
    /// appear in both slots.
    /// </summary>
    public static bool TryValidateAbilityPair(
        AbilityId slot1,
        AbilityId slot2,
        Predicate<AbilityId> isEquippable,
        out string error)
    {
        error = null;
        if (isEquippable == null)
        {
            error = "An equippable-ability predicate is required.";
            return false;
        }

        if (slot1.IsValid && slot1 == slot2)
        {
            error = "The same ability cannot occupy both slots.";
            return false;
        }

        if (slot1.IsValid && !isEquippable(slot1))
        {
            error = $"Ability '{slot1.Value}' has no execution behaviour.";
            return false;
        }

        if (slot2.IsValid && !isEquippable(slot2))
        {
            error = $"Ability '{slot2.Value}' has no execution behaviour.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Picks the Equipment slot an item of <paramref name="category"/> goes to. Armor pieces use
    /// their fixed slot; shields always use the set's Off Hand; weapons use the set's Main Hand
    /// unless <paramref name="preferOffHand"/> is set. Hand items require a real Weapon Set.
    /// </summary>
    public static bool TryResolveEquipmentSlot(
        LootCategory category,
        WeaponSetSlot set,
        bool preferOffHand,
        out EquipmentSlot slot)
    {
        slot = EquipmentSlot.None;
        EquipmentSlot candidate;
        switch (category)
        {
            case LootCategory.Weapon:
            case LootCategory.Shield:
                if (set != WeaponSetSlot.SetA && set != WeaponSetSlot.SetB)
                {
                    return false;
                }

                candidate = category == LootCategory.Shield || preferOffHand
                    ? EquipmentSlotRules.GetOffHandSlot(set)
                    : EquipmentSlotRules.GetMainHandSlot(set);
                break;
            default:
                candidate = EquipmentSlotRules.ResolveFixedSlot(category);
                break;
        }

        if (candidate == EquipmentSlot.None || !EquipmentSlotRules.IsCompatible(category, candidate))
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    /// <summary>A two-handed Main Hand cannot coexist with an Off Hand item in the same set.</summary>
    public static bool TryValidateWeaponSetPair(bool mainHandTwoHanded, bool offHandPresent, out string error)
    {
        if (mainHandTwoHanded && offHandPresent)
        {
            error = "A two-handed weapon cannot coexist with an Off Hand item in its Weapon Set.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Clamps a requested value into [minimum, maximum]. Fails (clamped = 0) for non-finite
    /// input or an inverted/non-finite range.
    /// </summary>
    public static bool TryClampValue(float requested, float minimum, float maximum, out float clamped)
    {
        clamped = 0f;
        if (!float.IsFinite(requested) || !float.IsFinite(minimum) || !float.IsFinite(maximum) ||
            minimum < 0f || maximum < minimum)
        {
            return false;
        }

        clamped = requested < minimum ? minimum : requested > maximum ? maximum : requested;
        return true;
    }
}
#endif

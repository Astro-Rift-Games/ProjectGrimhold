using UnityEngine;

/// <summary>Deterministic presentation decisions shared by Town member cards.</summary>
public static class TownRaidPreparationMemberCardRules
{
    public static string StateLabel(bool isReady) => isReady ? "Listo" : "Preparando";

    public static LootDefinition ResolveOffHand(LootDefinition mainHand, LootDefinition offHand, out bool reserved)
    {
        reserved = mainHand != null && mainHand.WeaponDefinition != null &&
            mainHand.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded;
        return reserved ? mainHand : offHand;
    }

    public static LootDefinition ResolveSummarySlot(in TownRaidPreparationPresentation.Member member,
        EquipmentSlot slot, out bool reserved)
    {
        reserved = false;
        if (slot != EquipmentSlot.WeaponSetAMainHand && slot != EquipmentSlot.WeaponSetAOffHand &&
            slot != EquipmentSlot.Armor) return null;

        member.TryGetEquipment(slot, out LootDefinition definition);
        if (slot != EquipmentSlot.WeaponSetAOffHand) return definition;
        member.TryGetEquipment(EquipmentSlot.WeaponSetAMainHand, out LootDefinition main);
        return ResolveOffHand(main, definition, out reserved);
    }

    public static bool ResolveOverlay(Sprite baseSprite, LootDefinition definition, out Sprite sprite, out Color color)
    {
        sprite = null;
        color = Color.white;
        EquipmentVisualDefinition visual = definition != null ? definition.EquipmentVisualDefinition : null;
        if (visual == null) return false;
        sprite = visual.ResolveSprite(baseSprite);
        color = visual.UsesBaseSpritesAsPlaceholder ? visual.Tint : Color.white;
        return sprite != null;
    }
}

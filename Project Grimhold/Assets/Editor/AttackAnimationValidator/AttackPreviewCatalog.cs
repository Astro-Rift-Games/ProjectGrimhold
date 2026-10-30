#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;


    /// <summary>
    /// Enumerates asset-backed cells in stable GUID order. Reuse EquipmentSlotRules.IsCompatible,
    /// WeaponDefinition.TryValidate and SandboxLoadoutRules.TryValidateWeaponSetPair, not sandbox
    /// slot preference (shields are legal in either hand). Null denotes an empty slot.
    /// Missing generated Off Hand presentation is diagnostic, never a legality filter.
    /// Shield cells defend only when a valid offensive weapon keeps the set eligible and the
    /// shield has defense presentation; shields never produce attack cells.
    /// Character attribute/admission eligibility is not evaluated: this is an asset preview,
    /// not a particular participant loadout.
    /// </summary>
    public static class AttackPreviewCatalog
    {
        public static IReadOnlyList<AttackPreviewCombination> Enumerate() => Enumerate(
            AssetDatabase.FindAssets("t:LootDefinition").OrderBy(guid => guid, StringComparer.Ordinal)
                .Select(guid => AssetDatabase.LoadAssetAtPath<LootDefinition>(AssetDatabase.GUIDToAssetPath(guid))));

        public static IReadOnlyList<AttackPreviewCombination> Enumerate(IEnumerable<LootDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var items = new List<LootDefinition> { null };
            items.AddRange(definitions.Where(IsValidHandItem).Distinct()
                .OrderBy(AttackPreviewCombination.AssetKey, StringComparer.Ordinal));
            var cells = new List<AttackPreviewCombination>();
            foreach (var main in items)
            foreach (var off in items)
            {
                if (!IsLegalPair(main, off)) continue;
                for (int facing = 0; facing < 6; facing++)
                {
                    if (IsWeapon(main)) cells.Add(new AttackPreviewCombination(main, off, AttackPreviewAction.MainAttack, facing));
                    if (IsWeapon(off)) cells.Add(new AttackPreviewCombination(main, off, AttackPreviewAction.OffAttack, facing));
                    if (AttackPreviewCombination.HasDefensePresentation(main) || AttackPreviewCombination.HasDefensePresentation(off))
                        cells.Add(new AttackPreviewCombination(main, off, AttackPreviewAction.Defend, facing));
                    if (AttackPreviewCombination.HasKnockbackPresentation(facing))
                        cells.Add(new AttackPreviewCombination(main, off, AttackPreviewAction.Knockback, facing));
                }
            }
            return cells.AsReadOnly();
        }

        private static bool IsWeapon(LootDefinition item) => item != null && item.Category == LootCategory.Weapon;

        private static bool IsValidHandItem(LootDefinition item) => item != null &&
            (IsWeapon(item) ? item.WeaponDefinition != null && item.WeaponDefinition.TryValidate(out _)
                : item.Category == LootCategory.Shield && item.ShieldDefinition != null && item.ShieldDefinition.TryValidate(out _));

        /// <summary>
        /// Mirrors the runner-bound set eligibility decision in
        /// PlayerWeaponEquipmentNetworkController.TryGetEligibleSetWeapon: at least one valid
        /// weapon in either compatible hand, with a two-handed Main blocking Off Hand.
        /// Reuses static validation; does not emulate participant attributes or ownership.
        /// </summary>
        public static bool IsLegalPair(LootDefinition mainHand, LootDefinition offHand)
        {
            if (mainHand != null && (!IsValidHandItem(mainHand) || !EquipmentSlotRules.IsCompatible(mainHand, EquipmentSlot.WeaponSetAMainHand))) return false;
            if (offHand != null && (!IsValidHandItem(offHand) || !EquipmentSlotRules.IsCompatible(offHand, EquipmentSlot.WeaponSetAOffHand))) return false;
            bool twoHanded = IsWeapon(mainHand) && mainHand.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded;
            return SandboxLoadoutRules.TryValidateWeaponSetPair(twoHanded, offHand != null, out _) &&
                (IsWeapon(mainHand) || IsWeapon(offHand));
        }

        /// <summary>
        /// False: PlayerCombatNetworkController accepts at most one attack per tick, Main first,
        /// even with zero execution window. Dual equipment does not imply simultaneous starts.
        /// </summary>
        public static bool SupportsSimultaneousAttacks => false;
    }
#endif

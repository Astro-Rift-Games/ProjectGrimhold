#if UNITY_EDITOR
using System;
using UnityEditor;


    public enum AttackPreviewAction { MainAttack, OffAttack, Defend, Knockback }

    /// <summary>Immutable catalog cell. Facing indices follow PlayerAnimatorView: N, NE, NW, S, SE, SW.</summary>
    public sealed class AttackPreviewCombination
    {
        private static readonly string[] FacingNames = { "N", "NE", "NW", "S", "SE", "SW" };

        public AttackPreviewCombination(LootDefinition mainHand, LootDefinition offHand, AttackPreviewAction action, int facingIndex)
        {
            if (facingIndex < 0 || facingIndex > 5) throw new ArgumentOutOfRangeException(nameof(facingIndex));
            if (!Enum.IsDefined(typeof(AttackPreviewAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            MainHand = mainHand;
            OffHand = offHand;
            Action = action;
            FacingIndex = facingIndex;
            string facing = FacingNames[facingIndex];
            Id = $"{AssetKey(mainHand)}/{AssetKey(offHand)}/{action}/{facingIndex}";
            DisplayName = $"{(mainHand != null ? mainHand.name : "None")} + {(offHand != null ? offHand.name : "None")} / {action} / {facing}";

        }

        internal static string AssetKey(LootDefinition definition)
        {
            if (definition == null) return "none";
            string path = AssetDatabase.GetAssetPath(definition);
            // In-memory fixtures have no GUID; names provide a deterministic test identity.
            return string.IsNullOrEmpty(path) ? definition.name : AssetDatabase.AssetPathToGUID(path);
        }

        internal static bool HasDefensePresentation(LootDefinition definition) => definition != null &&
            definition.Category == LootCategory.Shield && definition.DefenseSprites != null && definition.DefenseSprites.IsComplete;
        internal static bool HasKnockbackPresentation(int facingIndex)
        {
            if (facingIndex < 0 || facingIndex >= FacingNames.Length) return false;
            string facing = FacingNames[facingIndex];
            return AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>($"Assets/Animations/Player/Knockback/Knockback_{facing}.anim") != null &&
                AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>($"Assets/Animations/Player/Knockback/RightHand/RightHand_Knockback_{facing}.anim") != null &&
                AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>($"Assets/Animations/Player/Knockback/LeftHand/LeftHand_Knockback_{facing}.anim") != null;
        }
        public LootDefinition MainHand { get; }
        public LootDefinition OffHand { get; }
        public AttackPreviewAction Action { get; }
        public int FacingIndex { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public bool MissingPresentation => PresentationError != null;
        public AttackPreviewHand ConfigurationHand => OffHand?.WeaponDefinition != null && InvalidOffSet(OffHand.WeaponDefinition)
            ? AttackPreviewHand.Off : Action == AttackPreviewAction.OffAttack ? AttackPreviewHand.Off : AttackPreviewHand.Main;
        private static bool InvalidOffSet(WeaponDefinition weapon) => weapon.Presentation.OffHandAttackAnimationSet == null ||
            !weapon.Presentation.OffHandAttackAnimationSet.IsComplete || !weapon.Presentation.OffHandAttackAnimationSet.HasGripOriginOffsets;
        public string PresentationError
        {
            get
            {
                var off = OffHand != null ? OffHand.WeaponDefinition : null;
                if (off != null && (off.Presentation.HasGenericAttack || Action == AttackPreviewAction.OffAttack) && InvalidOffSet(off))
                    return "Off Hand attack set is missing/incomplete or lacks grip origin offsets. Regenerate Off Hand attacks.";
                if (Action == AttackPreviewAction.Knockback)
                    return HasKnockbackPresentation(FacingIndex) ? null : "Missing Knockback presentation clips.";
                if (Action == AttackPreviewAction.Defend)
                    return HasDefensePresentation(MainHand) || HasDefensePresentation(OffHand) ? null : "Missing defense presentation.";
                var attacker = Action == AttackPreviewAction.MainAttack ? MainHand : OffHand;
                var weapon = attacker != null ? attacker.WeaponDefinition : null;
                if (weapon == null) return "Attacking hand has no weapon presentation.";
                if (Action == AttackPreviewAction.MainAttack &&
                    (!weapon.Presentation.HasGenericAttack || weapon.Presentation.GetAttackClip(FacingIndex) == null))
                    return "Missing Main Hand attack clip.";
                if (Action == AttackPreviewAction.OffAttack && weapon.Presentation.GetOffHandAttackClip(FacingIndex) == null)
                    return "Missing Off Hand attack clip.";
                return null;
            }
        }
    }
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;


    /// <summary>Borrowed renderers for one already-evaluated frame; never owns or mutates them.
    /// GripPoint is pivot-relative sprite units, NOT rect pixels (presenter uses -R*S*GripPoint).
    /// ExpectedHandSprites comes from the owning hand's idle clip object-reference curves;
    /// name fallback is allowed only when that source set is unavailable, and must be disclosed in a diagnostic.
    /// Depth checks compare the hand and weapon against the body; occlusion independently measures
    /// the visual fist coverage rather than trusting the presenter's hand/weapon ordering.
    /// Neutral values must be captured from directional idle clips, never the attack's first frame.</summary>
    public sealed class AttackPreviewFrame
    {
        public float TimeSeconds { get; set; }
        public AttackPreviewHand Hand { get; set; }
        public SpriteRenderer HandRenderer { get; set; }
        public SpriteRenderer WeaponRenderer { get; set; }
        public SpriteRenderer BodyRenderer { get; set; }
        public Transform Grip { get; set; }
        public Transform VisualRoot { get; set; }
        public Vector2 WeaponGripPoint { get; set; }
        public IReadOnlyList<Sprite> ExpectedHandSprites { get; set; }
        /// <summary>Legacy input retained for callers; ignored. Occlusion is measured from sprite pixels.</summary>
        public bool HandOverWeapon { get; set; }
        /// <summary>False for an empty anatomical hand, even if a stale renderer is still visible.</summary>
        public bool HasHeldItem { get; set; } = true;
        /// <summary>Shield coverage is intentional; only Weapon hands participate in occlusion.</summary>
        public LootCategory HeldItemCategory { get; set; } = LootCategory.Weapon;
        public bool CheckWeaponGrip { get; set; } = true;
        public bool CheckGripTransform { get; set; } = true;
        public bool CheckHeldItemDepth { get; set; } = true;
        public Vector3? ExpectedVfxWorldOrigin { get; set; }
        public bool HasPinnedSupportIdle { get; set; }
        public SpriteRenderer VfxRenderer { get; set; }
        public float BladeReach { get; set; }
        public bool IsAfterDuration { get; set; }
        public bool IsInIdleState { get; set; } = true;
        public Vector3 NeutralHandLocalPosition { get; set; }
        public Quaternion NeutralHandLocalRotation { get; set; } = Quaternion.identity;
        public Vector3 NeutralHandLocalScale { get; set; } = Vector3.one;
        public Vector3 NeutralGripLocalPosition { get; set; }
        public Quaternion NeutralGripLocalRotation { get; set; } = Quaternion.identity;
        public Vector3 NeutralGripLocalScale { get; set; } = Vector3.one;
        public Sprite NeutralHandSprite { get; set; }
        public bool HasTransientOffHandOverlay { get; set; }
        public bool HasNeutralPivot { get; set; }
        public Vector3 NeutralPivotLocalPosition { get; set; }
        public Quaternion NeutralPivotLocalRotation { get; set; } = Quaternion.identity;
        public Vector3 NeutralPivotLocalScale { get; set; } = Vector3.one;
    }
#endif

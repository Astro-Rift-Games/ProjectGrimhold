#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;


    /// <summary>Editor-only bridge from the real rig to deterministic frame inputs. Neutral state is
    /// sampled on an independent prefab, never inferred from a completed (possibly broken) attack.</summary>
    internal sealed class AttackPreviewFrameSource : IDisposable
    {
        private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
        private static readonly FieldInfo SamplerField = typeof(PlayerWeaponPresenter).GetField("_offHandSampler", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AppliedField = typeof(OneHandPoseSampler).GetField("_applied", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly AttackPreviewRig _neutral;
        private readonly AttackPreviewRig _rig;
        private readonly AttackPreviewCombination _combination;
        private readonly SpriteRenderer _body;
        private readonly PlayerWeaponPresenter _presenter;
        private readonly IReadOnlyList<Sprite>[] _identity = new IReadOnlyList<Sprite>[2];

        public AttackPreviewFrameSource(AttackPreviewRig rig, AttackPreviewCombination combination)
        {
            _rig = rig;
            _combination = combination;
            _body = rig.VisualRoot.Find("Body")?.GetComponent<SpriteRenderer>();
            if (_body == null) throw new InvalidOperationException("Preview prefab is missing VisualRoot/Body.");
            _presenter = rig.Root.GetComponentInChildren<PlayerWeaponPresenter>(true);
            if (SamplerField == null || AppliedField == null)
                throw new InvalidOperationException("Off Hand overlay inspection no longer matches presenter fields.");
            _neutral = new AttackPreviewRig();
            try
            {
                _neutral.Animator.Rebind();
                SampleIdle("Idle/Idle_" + Directions[combination.FacingIndex]);
                for (int hand = 0; hand < 2; hand++)
                {
                    string side = hand == 0 ? "RightHand" : "LeftHand";
                    SampleIdle($"Idle/{side}/{side}_Idle_{Directions[combination.FacingIndex]}");
                    var sprites = new HashSet<Sprite>();
                    foreach (string direction in Directions)
                    {
                        AddClipSprites(sprites, LoadClip($"Idle/{side}/{side}_Idle_{direction}"), side);
                        if (combination.Action == AttackPreviewAction.Knockback)
                            AddClipSprites(sprites, LoadClip($"Knockback/{side}/{side}_Knockback_{direction}"), side);
                    }
                    _identity[hand] = new List<Sprite>(sprites).AsReadOnly();
                }
            }
            catch { _neutral.Dispose(); throw; }
        }

        private static void AddClipSprites(HashSet<Sprite> sprites, AnimationClip clip, string side)
        {
            if (clip == null) return;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite" ||
                    binding.path != side + "Pivot/" + side) continue;
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    if (key.value is Sprite sprite) sprites.Add(sprite);
            }
        }

        private static AnimationClip LoadClip(string relative) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/" + relative + ".anim");
        private void SampleIdle(string relative)
        {
            var clip = LoadClip(relative);
            if (clip == null) throw new InvalidOperationException("Missing neutral preview clip: " + relative);
            clip.SampleAnimation(_neutral.VisualRoot.gameObject, 0f);
        }

        public AttackPreviewFrame Capture(int hand, float seconds, bool afterDuration)
        {
            bool off = hand == 1;
            var loot = off ? _combination.OffHand : _combination.MainHand;
            var weapon = loot != null ? loot.WeaponDefinition : null;
            var mainWeapon = _combination.MainHand != null ? _combination.MainHand.WeaponDefinition : null;
            bool support = off && mainWeapon != null && mainWeapon.Handedness == WeaponHandedness.TwoHanded &&
                (mainWeapon.Presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip ||
                    mainWeapon.Presentation.Rig == WeaponRig.WeaponDriven);
            bool weaponDriven = mainWeapon != null && mainWeapon.Presentation.Rig == WeaponRig.WeaponDriven;
            bool stringHand = !off && weaponDriven;
            var renderer = _rig.WeaponRenderers[hand];
            Vector2 gripPoint = weapon != null ? weapon.Presentation.GripPoint : Vector2.zero;
            // Shields have no WeaponDefinition, but their pivot is their authored holding grip.
            // Sustained Defend must verify that grip without opting shields into weapon occlusion.
            bool defendingShield = _combination.Action == AttackPreviewAction.Defend &&
                loot != null && loot.Category == LootCategory.Shield;
            bool checkWeaponGrip = (weapon != null || defendingShield) && !stringHand;
            if (support)
            {
                renderer = _rig.WeaponRenderers[0];
                gripPoint = weaponDriven ? mainWeapon.Presentation.GripPoint : mainWeapon.Presentation.SecondaryGripPoint;
                checkWeaponGrip = weaponDriven || mainWeapon.Presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip;
            }
            bool idleState = !afterDuration || SampleCurrentIdle(hand);
            Transform neutralHand = _neutral.HandRenderers[hand].transform;
            Transform neutralGrip = off ? _neutral.OffHandGrip : _neutral.MainHandGrip;
            var frame = new AttackPreviewFrame
            {
                TimeSeconds = seconds, Hand = off ? AttackPreviewHand.Off : AttackPreviewHand.Main,
                VisualRoot = _rig.VisualRoot, HandRenderer = _rig.HandRenderers[hand], WeaponRenderer = renderer,
                BodyRenderer = _body, Grip = off ? _rig.OffHandGrip : _rig.MainHandGrip,
                WeaponGripPoint = gripPoint, ExpectedHandSprites = _identity[hand],
                CheckWeaponGrip = checkWeaponGrip, CheckGripTransform = !stringHand && (weapon != null || defendingShield || (support && checkWeaponGrip)),
                CheckHeldItemDepth = !stringHand,
                HasPinnedSupportIdle = support && !weaponDriven && afterDuration,
                // A free gesturing hand is not holding an item. A true two-hand support uses
                // the Main weapon renderer for depth AND opaque-pixel occlusion measurements.
                HasHeldItem = loot != null || support,
                HeldItemCategory = support ? LootCategory.Weapon : loot != null ? loot.Category : LootCategory.Weapon,
                BladeReach = weapon != null ? weapon.Presentation.BladeReach : 0,
                IsAfterDuration = afterDuration, IsInIdleState = idleState,
                NeutralHandLocalPosition = neutralHand.localPosition, NeutralHandLocalRotation = neutralHand.localRotation,
                NeutralHandLocalScale = neutralHand.localScale, NeutralHandSprite = _neutral.HandRenderers[hand].sprite,
                NeutralGripLocalPosition = neutralGrip.localPosition, NeutralGripLocalRotation = neutralGrip.localRotation,
                NeutralGripLocalScale = neutralGrip.localScale,
                HasNeutralPivot = true, NeutralPivotLocalPosition = neutralHand.parent.localPosition,
                NeutralPivotLocalRotation = neutralHand.parent.localRotation, NeutralPivotLocalScale = neutralHand.parent.localScale,
                HasTransientOffHandOverlay = off && HasOverlay()
            };
            bool attacking = off ? _combination.Action == AttackPreviewAction.OffAttack : _combination.Action == AttackPreviewAction.MainAttack;
            if (attacking)
            {
                var vfx = _rig.AttackVfxRenderers;
                frame.VfxRenderer = off ? (vfx.Count > 1 ? vfx[1] : null) : vfx[0];
                frame.ExpectedVfxWorldOrigin = ResolveReleaseOrigin(weapon, off);
            }
            return frame;
        }

        private Vector3? ResolveReleaseOrigin(WeaponDefinition weapon, bool off)
        {
            var presentation = weapon != null ? weapon.Presentation : default;
            var vfx = weapon != null ? presentation.AttackVfx : null;
            // The canonical slash pivot is the body origin, independently of the authored VFX pose.
            if (vfx != null && presentation.AttackAnimationSet != null && presentation.AttackAnimationSet.CanonicalSlash)
                return _rig.VisualRoot.TransformPoint(Vector3.zero);
            if (vfx == null || !(vfx.Visual is BowShotVfxVisualDefinition || vfx.Visual is CastFlashVfxVisualDefinition)) return null;
            int index = _combination.FacingIndex;
            if (!vfx.TryResolvePose(index, presentation.BladeReach, out var pose)) return null;
            Vector3 position = pose.Position;
            var view = _rig.Root.GetComponentInChildren<PlayerAnimatorView>(true);
            bool live = FreeAimResidualPolicy.Applies(presentation.AimMode, view.HasPresentedAim, view.IsAimDriven);
            bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(view.VisualFacingDirection);
            if (off && presentation.AimMode == WeaponAimMode.FreeAim)
            {
                var source = FreeAimAttackVfxPose.ResolveAtGrip(_rig.VisualRoot, _rig.OffHandGrip,
                    view.PresentedAimDirection, mirrored, vfx.GetPose(index), pose.SortingOrder);
                if (vfx.TryResolvePose(source, presentation.BladeReach, out pose)) position = pose.Position;
            }
            else if (!off && live)
            {
                Transform pivot = _rig.WeaponRenderers[0].transform.parent;
                Vector3 anchor = _rig.VisualRoot.InverseTransformPoint(pivot.position);
                Vector3 axis = _rig.VisualRoot.InverseTransformDirection(pivot.right);
                bool front = CharacterVisualDirectionResolver.IsFrontFacing(CharacterVisualDirectionResolver.Resolve(view.VisualFacingDirection));
                var source = FreeAimAttackVfxPose.Resolve(anchor, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg,
                    mirrored, vfx.GetPose(index), vfx.GetPose(front ? 3 : 0).SortingOrder);
                if (vfx.TryResolvePose(source, presentation.BladeReach, out pose)) position = pose.Position;
            }
            else if (off)
            {
                var set = presentation.OffHandAttackAnimationSet;
                position += (Vector3)set.GetGripOriginOffset(index);
                if (set.OffHandStroke == OffHandStroke.Mirrored)
                {
                    Vector3 neutral = set.GetMainGripNeutralPosition(index);
                    Vector3 delta = pose.Position - neutral;
                    delta = PlayerWeaponPresentationMath.ReflectAcrossFacing(delta, index);
                    position = neutral + (Vector3)set.GetGripOriginOffset(index) + delta;
                }
            }
            return _rig.VisualRoot.TransformPoint(position);
        }

        // Sample independent authored idle data, never the observed hand (which may contain residue).
        // Each hand's layer owns its own phase; the non-attacking idle need not restart with the attack.
        private bool SampleCurrentIdle(int hand)
        {
            string side = hand == 0 ? "RightHand" : "LeftHand";
            int layer = _rig.Animator.GetLayerIndex(side);
            _neutral.Animator.Rebind();
            var bodyIdle = LoadClip("Idle/Idle_" + Directions[_combination.FacingIndex]);
            if (bodyIdle != null) SampleAtPhase(bodyIdle, _rig.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            var fallback = LoadClip($"Idle/{side}/{side}_Idle_{Directions[_combination.FacingIndex]}");
            if (layer < 0)
            {
                if (fallback != null) SampleAtPhase(fallback, 0);
                return false;
            }
            var state = _rig.Animator.GetCurrentAnimatorStateInfo(layer);
            bool idle = state.shortNameHash == Animator.StringToHash(side + "-Idle") && !_rig.Animator.IsInTransition(layer);
            AnimationClip clip = null;
            float weight = -1;
            if (idle)
            {
                foreach (var info in _rig.Animator.GetCurrentAnimatorClipInfo(layer))
                {
                    if (info.clip == null || info.weight <= weight) continue;
                    clip = info.clip;
                    weight = info.weight;
                }
            }
            if (clip == null) clip = fallback;
            if (clip != null) SampleAtPhase(clip, idle ? state.normalizedTime : 0);
            return idle && clip != null;
        }

        private void SampleAtPhase(AnimationClip clip, float normalizedTime)
        {
            float phase = clip.isLooping ? Mathf.Repeat(normalizedTime, 1f) : Mathf.Clamp01(normalizedTime);
            clip.SampleAnimation(_neutral.VisualRoot.gameObject, phase * clip.length);
        }

        private bool HasOverlay()
        {
            var sampler = SamplerField.GetValue(_presenter);
            return sampler != null && (bool)AppliedField.GetValue(sampler);
        }
        public void Dispose() => _neutral.Dispose();
    }
#endif

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Samples a configured sprite-only VFX on the existing character Animator root, sized from the
/// attacking weapon's blade reach.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAttackVfxPresenter : MonoBehaviour
{
    private const float BoundaryToleranceSeconds = 0.0001f;
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private CharacterBase _character;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _vfxTransform;
    [SerializeField] private SpriteRenderer _vfxRenderer;
    [SerializeField, Tooltip("Maps the confirmed weapon's damage type to the color that tints the neutral VFX sprite.")]
    private AttackVfxTintPalette _tintPalette;

    private PlayerCombatNetworkController _subscribedCombat;
    private AttackVfxDefinition _vfx;
    private AnimationClip _attackClip;
    private int _layer = -1;
    private bool _pending;
    private bool _observed;
    private float _pendingSince;
    private AttackPerformedEvent _attack;
    private float _lastSampleSeconds;
    private readonly List<AnimatorClipInfo> _clipBuffer = new List<AnimatorClipInfo>(2);
    // Live weapon pivot pushed by the weapon presenter while its weapon uses free aim.
    private bool _hasFreeAimPivot;
    private Vector2 _freeAimAnchor;
    private float _freeAimAngle;
    private bool _freeAimMirrored;
    private bool _freeAimFront;
    private bool _followsFreeAim;
    private int _attackIndex;
    private float _bladeReach;

    private void OnEnable()
    {
        _layer = _animator != null ? _animator.GetLayerIndex("RightHand") : -1;
        if (_combatController == null || _equipmentSource == null || _character == null || _layer < 0 ||
            _vfxTransform == null || _vfxRenderer == null || _vfxRenderer.transform != _vfxTransform ||
            _vfxTransform.parent != _animator.transform || _vfxTransform.name != "AttackVfx")
        {
            Debug.LogError("Attack VFX presenter requires combat, equipment, RightHand Animator layer and VisualRoot/AttackVfx renderer.", this);
            enabled = false;
            return;
        }
        string paletteError = null;
        if (_tintPalette == null || !_tintPalette.TryValidate(out paletteError))
        {
            Debug.LogError($"Attack VFX presenter requires a complete tint palette: {paletteError ?? "none assigned."}", this);
            enabled = false;
            return;
        }
        _subscribedCombat = _combatController;
        _subscribedCombat.AttackPerformed += OnAttackPerformed;
        _subscribedCombat.AttackPresentationResumed += OnAttackPerformed;
        Clear();
    }

    private void OnDisable()
    {
        if (_subscribedCombat != null)
        {
            _subscribedCombat.AttackPerformed -= OnAttackPerformed;
            _subscribedCombat.AttackPresentationResumed -= OnAttackPerformed;
            _subscribedCombat = null;
        }
        Clear();
        _hasFreeAimPivot = false;
    }

    /// <summary>
    /// Receives the free-aim weapon pivot, in the Animator root space, every frame it is posed. A playing free-aim
    /// VFX is re-anchored to it because the weapon keeps moving during wind-up.
    /// </summary>
    internal void SetFreeAimPivot(Vector2 anchor, float angleDegrees, bool mirrored, bool frontFacing)
    {
        _hasFreeAimPivot = true;
        _freeAimAnchor = anchor;
        _freeAimAngle = angleDegrees;
        _freeAimMirrored = mirrored;
        _freeAimFront = frontFacing;
        if (_pending && _followsFreeAim &&
            TryResolveAttackPose(_vfx, _attackIndex, _bladeReach, true, out AttackVfxDefinition.ResolvedPose pose))
        {
            ApplyPose(pose);
        }
    }

    internal void ClearFreeAimPivot()
    {
        bool wasPushed = _hasFreeAimPivot;
        _hasFreeAimPivot = false;
        // Leaving the aim-driven state while an attack plays goes back to the baked pose.
        if (wasPushed && _pending && _followsFreeAim &&
            TryResolveAttackPose(_vfx, _attackIndex, _bladeReach, false, out AttackVfxDefinition.ResolvedPose pose))
        {
            ApplyPose(pose);
        }
    }

    private bool TryResolveAttackPose(AttackVfxDefinition vfx, int index, float bladeReach, bool followsFreeAim,
        out AttackVfxDefinition.ResolvedPose pose)
    {
        if (!followsFreeAim)
        {
            return vfx.TryResolvePose(index, bladeReach, out pose);
        }
        // The anchor and axis come from the live pivot; the authored lead and the front/back depth are kept.
        AttackVfxDefinition.DirectionalPose freePose = FreeAimAttackVfxPose.Resolve(
            _freeAimAnchor,
            _freeAimAngle,
            _freeAimMirrored,
            vfx.GetPose(index),
            vfx.GetPose(_freeAimFront ? 3 : 0).SortingOrder);
        return vfx.TryResolvePose(freePose, bladeReach, out pose);
    }

    private void ApplyPose(AttackVfxDefinition.ResolvedPose pose)
    {
        _vfxTransform.localPosition = pose.Position;
        _vfxTransform.localRotation = pose.Rotation;
        _vfxTransform.localScale = pose.Scale;
        _vfxRenderer.sortingOrder = pose.SortingOrder;
    }

    private void OnAttackPerformed(AttackPerformedEvent attack)
    {
        Clear();
        if (!_character.IsAlive || PlayerDownedGate.IsDowned(_character) || !_equipmentSource.TryGetWeaponByCatalogIndexPlusOne(
            attack.WeaponCatalogIndexPlusOne, out LootDefinition loot))
        {
            return;
        }
        WeaponDefinition.PresentationConfig presentation = loot.WeaponDefinition.Presentation;
        AttackVfxDefinition vfx = presentation.AttackVfx;
        if (vfx == null || !presentation.HasGenericAttack || !vfx.TryValidate(out _))
        {
            return;
        }
        CharacterVisualDirection direction = CharacterVisualDirectionResolver.Resolve(attack.Direction);
        int index = direction switch
        {
            CharacterVisualDirection.North => 0,
            CharacterVisualDirection.NorthEast => 1,
            CharacterVisualDirection.NorthWest => 2,
            CharacterVisualDirection.South => 3,
            CharacterVisualDirection.SouthEast => 4,
            _ => 5
        };
        _attackClip = presentation.GetAttackClip(index);
        float start = attack.HasReleaseTimeline
            ? loot.WeaponDefinition.AttackReleaseSeconds - vfx.ReleaseLeadSeconds : vfx.StartSeconds;
        // An aim-driven weapon (free arc, or aim stance while aiming or attacking) anchors to the live pivot once
        // the weapon presenter pushes it, which happens every LateUpdate while the residual applies.
        bool usesAim = presentation.AimMode != WeaponAimMode.BakedFacing;
        if (_attackClip == null || start < 0f || _attackClip.length < start + vfx.Clip.length ||
            !TryResolveAttackPose(vfx, index, presentation.BladeReach, usesAim && _hasFreeAimPivot,
                out AttackVfxDefinition.ResolvedPose pose) ||
            !_tintPalette.TryGetTint(loot.WeaponDefinition.DamageType, out Color tint))
        {
            Clear();
            return;
        }
        ApplyPose(pose);
        _vfxRenderer.color = tint;
        _followsFreeAim = usesAim;
        _attackIndex = index;
        _bladeReach = presentation.BladeReach;
        _vfx = vfx;
        _attack = attack;
        _lastSampleSeconds = -1f;
        _pending = true;
        _pendingSince = Time.time;
    }

    private void LateUpdate()
    {
        if (!_character.IsAlive || PlayerDownedGate.IsDowned(_character))
        {
            Clear();
            return;
        }
        if (!_pending) return;
        if (_attack.HasReleaseTimeline)
        {
            if (!_combatController.TryGetAttackPresentationSeconds(_attack, out float elapsed))
            {
                Clear();
                return;
            }
            // Same deadline as the projectile, with an explicit art lead; never an Animator receipt timer.
            SampleVfx(AttackTiming.ReleaseVfxSeconds(elapsed, _attack.ScheduledWindupSeconds, _vfx.ReleaseLeadSeconds));
            return;
        }
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(_layer);
        if (!state.IsTag("Attack"))
        {
            if (_observed || Time.time - _pendingSince > _attackClip.length) Clear();
            return;
        }
        _animator.GetCurrentAnimatorClipInfo(_layer, _clipBuffer);
        bool matches = false;
        for (int i = 0; i < _clipBuffer.Count; i++)
        {
            if (_clipBuffer[i].clip == _attackClip)
            {
                matches = true;
                break;
            }
        }
        if (!matches)
        {
            Clear();
            return;
        }
        _observed = true;
        float seconds = state.normalizedTime * _attackClip.length - _vfx.StartSeconds;
        SampleVfx(seconds);
    }

    private void SampleVfx(float seconds)
    {
        if (seconds < -BoundaryToleranceSeconds) return;
        // Never rewind an already observed ranged impulse during clock corrections.
        // Melee keeps its existing clip-relative sampling behavior.
        if (_attack.HasReleaseTimeline)
        {
            seconds = Mathf.Max(seconds, _lastSampleSeconds);
            _lastSampleSeconds = seconds;
        }
        if (seconds + BoundaryToleranceSeconds >= _vfx.Clip.length)
        {
            Clear();
            return;
        }
        _vfx.Clip.SampleAnimation(_animator.gameObject, Mathf.Max(0f, seconds + BoundaryToleranceSeconds));
        _vfxRenderer.enabled = true;
    }

    /// <summary>Cancels any pending or playing attack VFX, e.g. when the player becomes Downed.</summary>
    public void CancelAndRestore() => Clear();

    private void Clear()
    {
        _pending = false;
        _observed = false;
        _followsFreeAim = false;
        _vfx = null;
        _attackClip = null;
        if (_vfxRenderer != null)
        {
            _vfxRenderer.sprite = null;
            _vfxRenderer.color = Color.white;
            _vfxRenderer.enabled = false;
        }
    }
}

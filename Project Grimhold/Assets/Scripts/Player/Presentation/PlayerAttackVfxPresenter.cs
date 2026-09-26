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

    private PlayerCombatNetworkController _subscribedCombat;
    private AttackVfxDefinition _vfx;
    private AnimationClip _attackClip;
    private int _layer = -1;
    private bool _pending;
    private bool _observed;
    private float _pendingSince;
    private readonly List<AnimatorClipInfo> _clipBuffer = new List<AnimatorClipInfo>(2);

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
        _subscribedCombat = _combatController;
        _subscribedCombat.AttackPerformed += OnAttackPerformed;
        Clear();
    }

    private void OnDisable()
    {
        if (_subscribedCombat != null)
        {
            _subscribedCombat.AttackPerformed -= OnAttackPerformed;
            _subscribedCombat = null;
        }
        Clear();
    }

    private void OnAttackPerformed(AttackPerformedEvent attack)
    {
        Clear();
        if (!_character.IsAlive || !_equipmentSource.TryGetWeaponByCatalogIndexPlusOne(
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
        if (_attackClip == null || _attackClip.length < vfx.StartSeconds + vfx.Clip.length ||
            !vfx.TryResolvePose(index, presentation.BladeReach, out AttackVfxDefinition.ResolvedPose pose))
        {
            Clear();
            return;
        }
        _vfxTransform.localPosition = pose.Position;
        _vfxTransform.localRotation = pose.Rotation;
        _vfxTransform.localScale = pose.Scale;
        _vfxRenderer.sortingOrder = pose.SortingOrder;
        _vfx = vfx;
        _pending = true;
        _pendingSince = Time.time;
    }

    private void LateUpdate()
    {
        if (!_character.IsAlive)
        {
            Clear();
            return;
        }
        if (!_pending) return;
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
        if (seconds < -BoundaryToleranceSeconds) return;
        if (seconds + BoundaryToleranceSeconds >= _vfx.Clip.length)
        {
            Clear();
            return;
        }
        _vfx.Clip.SampleAnimation(_animator.gameObject, Mathf.Max(0f, seconds + BoundaryToleranceSeconds));
        _vfxRenderer.enabled = true;
    }

    private void Clear()
    {
        _pending = false;
        _observed = false;
        _vfx = null;
        _attackClip = null;
        if (_vfxRenderer != null)
        {
            _vfxRenderer.sprite = null;
            _vfxRenderer.enabled = false;
        }
    }
}

using UnityEngine;
using Grimhold.Combat.Presentation;

[DisallowMultipleComponent]
public sealed class PlayerAttackVfxPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private PlayerWeaponPresenter _weaponPresenter;
    [SerializeField] private CombatVfxPool _vfxPool;

    private PlayerCombatNetworkController _subscribedCombatController;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        SubscribeToCombat();
    }

    private void OnDisable()
    {
        UnsubscribeFromCombat();
    }

    private void CacheDependencies()
    {
        _combatController ??= GetComponentInParent<PlayerCombatNetworkController>();
        _equipmentSource ??= GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
        _weaponPresenter ??= GetComponentInChildren<PlayerWeaponPresenter>(true);
        _vfxPool ??= GetComponentInChildren<CombatVfxPool>(true);
    }

    private void SubscribeToCombat()
    {
        if (_subscribedCombatController == _combatController) return;

        UnsubscribeFromCombat();
        _subscribedCombatController = _combatController;
        if (_subscribedCombatController != null)
        {
            _subscribedCombatController.AttackPerformed += OnAttackPerformed;
        }
    }

    private void UnsubscribeFromCombat()
    {
        if (_subscribedCombatController != null)
        {
            _subscribedCombatController.AttackPerformed -= OnAttackPerformed;
            _subscribedCombatController = null;
        }
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (_vfxPool == null || _equipmentSource == null || !_equipmentSource.Object.IsValid)
            return;

        if (!_equipmentSource.TryGetEquippedDefinition(out LootDefinition mainHandLoot) || mainHandLoot == null)
            return;

        WeaponDefinition weaponDef = mainHandLoot.WeaponDefinition;
        if (weaponDef == null) return;

        WeaponDefinition.PresentationConfig presentation = weaponDef.Presentation;
        CombatVfxDefinition vfxDef = presentation.CombatVfx;

        if (vfxDef == null) 
        {
            // Debug.LogWarning($"[PlayerAttackVfxPresenter] No VFX assigned for {weaponDef.name}");
            return;
        }

        Transform parent = null;
        if (_weaponPresenter != null)
        {
            parent = _weaponPresenter.GetMainHandWeaponTransform();
        }

        Vector3 localPosition;
        if (vfxDef.AnchorMode == CombatVfxAnchor.WeaponPoint)
        {
            localPosition = presentation.VfxLocalPoint;
        }
        else
        {
            localPosition = vfxDef.LocalOffset;
        }

        // If parented to the weapon, the VFX inherits the weapon's animation rotation.
        // We only need to apply the rotation offset to align the sprite with the sword blade.
        float rotation = vfxDef.RotationOffset;
        
        CharacterVisualDirection visualDirection = CharacterVisualDirectionResolver.Resolve(attackEvent.Direction);
        bool mirror = CombatVfxPresentationMath.ShouldMirror(visualDirection);

        Color tint = presentation.VfxTint != default && presentation.VfxTint.a > 0f ? presentation.VfxTint : vfxDef.DefaultTint;
        if (presentation.VfxTint == Color.clear)
        {
             tint = vfxDef.DefaultTint;
        }

        _vfxPool.Prewarm(vfxDef);
        _vfxPool.Play(
            definition: vfxDef,
            position: localPosition,
            rotation: Quaternion.Euler(0f, 0f, rotation),
            mirror: mirror,
            tint: tint,
            parent: parent
        );
    }
}

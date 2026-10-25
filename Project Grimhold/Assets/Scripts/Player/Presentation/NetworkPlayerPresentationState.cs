using System;
using UnityEngine;

/// <summary>Runner-backed read-only presentation state. Owns only its event subscriptions, not the controllers.</summary>
public sealed class NetworkPlayerPresentationState : IPlayerPresentationState, IDisposable
{
    private readonly PlayerCombatNetworkController _combat;
    private readonly PlayerWeaponEquipmentNetworkController _equipment;
    private readonly PlayerMovementNetworkController _movement;
    private readonly PlayerShieldDefenseNetworkController _shield;
    private readonly PlayerDownedStateNetworkController _downed;
    private readonly bool _useCharacterDownedGate;
    private readonly CharacterBase _character;
    private readonly PlayerConsumableNetworkController _consumables;
    private bool _disposed;

    public event Action<AttackPerformedEvent> AttackPerformed;
    public event Action<AttackPerformedEvent> AttackPresentationResumed;

    public NetworkPlayerPresentationState(
        PlayerCombatNetworkController combat,
        PlayerWeaponEquipmentNetworkController equipment,
        PlayerMovementNetworkController movement,
        PlayerShieldDefenseNetworkController shield,
        PlayerDownedStateNetworkController downed,
        CharacterBase character,
        PlayerConsumableNetworkController consumables,
        bool useCharacterDownedGate = false)
    {
        _combat = combat;
        _equipment = equipment;
        _movement = movement;
        _shield = shield;
        _downed = downed;
        _useCharacterDownedGate = useCharacterDownedGate;
        _character = character;
        _consumables = consumables;
        if (_combat != null)
        {
            _combat.AttackPerformed += OnAttackPerformed;
            _combat.AttackPresentationResumed += OnAttackPresentationResumed;
        }
    }

    public bool IsAlive => _character != null && _character.IsAlive;
    // Consumers explicitly select their original predicate; neither source falls back to the other.
    public bool IsDowned => _useCharacterDownedGate
        ? PlayerDownedGate.IsDowned(_character)
        : _downed != null && _downed.Object != null && _downed.Object.IsValid && _downed.IsDowned;
    public bool IsConsuming => _consumables != null && _consumables.IsConsuming;
    public bool IsEquipmentReadable => _equipment != null && _equipment.Object != null && _equipment.Object.IsValid;
    public bool IsAimReadable => _movement != null && _movement.Object != null && _movement.Object.IsValid;
    public Vector2 AimDirection => IsAimReadable ? _movement.AimDirection : Vector2.zero;
    public Vector2 FacingDirection => IsAimReadable ? _movement.FacingDirection : Vector2.down;
    public bool IsAimStance => IsAimReadable && _movement.IsAimStance;
    public bool HasInputAuthority => IsAimReadable && _movement.Object.HasInputAuthority;
    public bool IsDefending => _shield != null && _shield.Object != null && _shield.Object.IsValid && _shield.IsDefending;
    public WeaponSetSlot ActiveWeaponSetSlot => _equipment != null ? _equipment.ActiveWeaponSetSlot : WeaponSetSlot.None;

    public bool TryGetAttackPresentationSeconds(in AttackPerformedEvent attack, out float seconds)
    {
        seconds = 0f;
        return _combat != null && _combat.TryGetAttackPresentationSeconds(attack, out seconds);
    }

    public bool TryGetWeaponByCatalogIndexPlusOne(int indexPlusOne, out LootDefinition definition)
    {
        definition = null;
        return _equipment != null && _equipment.TryGetWeaponByCatalogIndexPlusOne(indexPlusOne, out definition);
    }

    public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
    {
        definition = null;
        return _equipment != null && _equipment.TryGetSlotDefinition(slot, out definition);
    }

    public bool TryGetEquippedDefinition(out LootDefinition definition)
    {
        definition = null;
        return _equipment != null && _equipment.TryGetEquippedDefinition(out definition);
    }

    public EquipmentSlot GetActiveHandSlot(bool offHand) => _equipment != null
        ? _equipment.GetActiveHandSlot(offHand) : EquipmentSlot.None;

    public bool TryGetAimStanceElapsedSeconds(out float seconds)
    {
        seconds = 0f;
        return _movement != null && _movement.TryGetAimStanceElapsedSeconds(out seconds);
    }

    public bool TryGetConsumingDefinition(out LootDefinition definition)
    {
        definition = null;
        return _consumables != null && _consumables.TryGetConsumingDefinition(out definition);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_combat != null)
        {
            _combat.AttackPerformed -= OnAttackPerformed;
            _combat.AttackPresentationResumed -= OnAttackPresentationResumed;
        }
        AttackPerformed = null;
        AttackPresentationResumed = null;
    }

    private void OnAttackPerformed(AttackPerformedEvent attack) => AttackPerformed?.Invoke(attack);
    private void OnAttackPresentationResumed(AttackPerformedEvent attack) => AttackPresentationResumed?.Invoke(attack);
}

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Tests.EditMode.Player.Presentation
{
    internal sealed class FakePlayerPresentationState : IPlayerPresentationState, IMovementState, IEquipmentVisualSource
    {
        public event Action<AttackPerformedEvent> AttackPerformed;
        public event Action<AttackPerformedEvent> AttackPresentationResumed;
        public readonly Dictionary<EquipmentSlot, LootDefinition> Equipment = new();
        public LootDefinition AttackWeapon { get; set; }
        public float AttackSeconds { get; set; }
        public bool HasAttackClock { get; set; } = true;
        public bool IsAlive { get; set; } = true;
        public bool IsDowned { get; set; }
        public bool IsConsuming { get; set; }
        public bool IsEquipmentReadable { get; set; } = true;
        public bool IsAimReadable { get; set; } = true;
        public Vector2 AimDirection { get; set; } = Vector2.down;
        public Vector2 FacingDirection { get; set; } = Vector2.down;
        public bool IsAimStance { get; set; }
        public bool HasInputAuthority { get; set; } = true;
        public bool IsDefending { get; set; }
        public WeaponSetSlot ActiveWeaponSetSlot { get; set; } = WeaponSetSlot.SetA;
        public NetworkBool IsMoving { get; set; }
        public NetworkBool IsControlEnabled { get; set; } = true;
        public int ObservedEquipmentRevision { get; set; }
        public float AimStanceSeconds { get; set; }
        public LootDefinition ConsumingDefinition { get; set; }

        public void RaiseAttack(AttackPerformedEvent attack) => AttackPerformed?.Invoke(attack);
        public void ResumeAttack(AttackPerformedEvent attack) => AttackPresentationResumed?.Invoke(attack);
        public bool TryGetAttackPresentationSeconds(in AttackPerformedEvent attack, out float seconds)
        {
            seconds = HasAttackClock ? AttackSeconds : 0f;
            return HasAttackClock && IsAlive && !IsDowned && !IsConsuming;
        }
        public bool TryGetWeaponByCatalogIndexPlusOne(int indexPlusOne, out LootDefinition definition)
        {
            definition = indexPlusOne == 1 ? AttackWeapon : null;
            return definition != null;
        }
        public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition) => Equipment.TryGetValue(slot, out definition);
        public bool TryGetEquippedDefinition(out LootDefinition definition) => TryGetSlotDefinition(GetActiveHandSlot(false), out definition);
        public EquipmentSlot GetActiveHandSlot(bool offHand) => offHand
            ? EquipmentSlotRules.GetOffHandSlot(ActiveWeaponSetSlot)
            : EquipmentSlotRules.GetMainHandSlot(ActiveWeaponSetSlot);
        public bool TryGetAimStanceElapsedSeconds(out float seconds)
        {
            seconds = IsAimStance ? AimStanceSeconds : 0f;
            return IsAimStance;
        }
        public bool TryGetConsumingDefinition(out LootDefinition definition)
        {
            definition = IsConsuming ? ConsumingDefinition : null;
            return definition != null;
        }
    }
}
#endif

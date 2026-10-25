#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;


    /// <summary>Runner-free specimen of a live, stationary, non-consuming, non-aim-stance player.
    /// Equipment queries mirror the runtime controller: equipped means Main Hand (including shields),
    /// slot queries expose raw equipment, and confirmed catalog identities survive equipment changes.
    /// The isolated catalog uses preview-local indices, never network/persistence identities.
    /// Configure schedules one accepted action at zero; Evaluate deliberately republishes that same
    /// confirmed event for absolute/backward seeks, unlike the runtime's once-per-observation Render.</summary>
    public sealed class PreviewPlayerPresentationState : IPlayerPresentationState, IMovementState, IEquipmentVisualSource
    {
        private sealed class HandTimeline
        {
            public bool Enabled;
            public int Sequence;
            public AttackPerformedEvent Attack;
        }

        private static readonly Vector2[] Directions =
        {
            Vector2.up, new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
            Vector2.down, new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
        };
        private readonly HandTimeline _main = new HandTimeline();
        private readonly HandTimeline _off = new HandTimeline();
        private readonly List<LootDefinition> _catalog = new List<LootDefinition>();
        private AttackPreviewCombination _combination;
        private float _seconds;

        public void Configure(AttackPreviewCombination combination)
        {
            if (combination == null) throw new ArgumentNullException(nameof(combination));
            bool equipmentChanged = _combination == null || _combination.MainHand != combination.MainHand ||
                _combination.OffHand != combination.OffHand;
            _combination = combination;
            _seconds = 0f;
            if (equipmentChanged) ObservedEquipmentRevision++;
            Register(combination.MainHand);
            Register(combination.OffHand);
            ConfigureHand(_main, combination.MainHand, false, combination.Action == AttackPreviewAction.MainAttack);
            ConfigureHand(_off, combination.OffHand, true, combination.Action == AttackPreviewAction.OffAttack);
        }

        private int Register(LootDefinition definition)
        {
            if (definition == null) return 0;
            int index = _catalog.IndexOf(definition);
            if (index < 0) { index = _catalog.Count; _catalog.Add(definition); }
            return index + 1;
        }

        private void ConfigureHand(HandTimeline hand, LootDefinition loot, bool offHand, bool enabled)
        {
            hand.Enabled = enabled && loot != null && loot.Category == LootCategory.Weapon &&
                loot.WeaponDefinition != null && !(offHand && IsOffHandBlocked) &&
                EquipmentSlotRules.IsCompatible(loot, GetActiveHandSlot(offHand));
            hand.Attack = default;
            if (!hand.Enabled) return;
            var weapon = loot.WeaponDefinition;
            hand.Sequence++;
            hand.Attack = new AttackPerformedEvent(default,
                weapon.PrimaryAttack is RangedAttackConfig ? AttackType.Ranged : AttackType.Melee,
                Vector2.zero, FacingDirection, 0, Register(loot), hand.Sequence,
                0, weapon.AttackReleaseSeconds, sourceSlot: GetActiveHandSlot(offHand));
        }

        public void Evaluate(float seconds, bool publishAttack = true)
        {
            if (_combination == null) throw new InvalidOperationException("Configure preview state first.");
            if (!float.IsFinite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
            if (publishAttack)
            {
                Publish(_main);
                Publish(_off);
            }
        }

        private void Publish(HandTimeline hand)
        {
            if (hand.Enabled) AttackPerformed?.Invoke(hand.Attack);
        }

        private bool IsOffHandBlocked => _combination?.MainHand?.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded;
        private bool HasEligibleShield(bool offHand)
        {
            EquipmentSlot slot = GetActiveHandSlot(offHand);
            return !IsOffHandBlocked && TryGetSlotDefinition(slot, out var definition) &&
                definition.Category == LootCategory.Shield && EquipmentSlotRules.IsCompatible(definition, slot);
        }

        public event Action<AttackPerformedEvent> AttackPerformed;
        // Migration is not simulated. Retain the subscription contract without fabricating resume events.
        public event Action<AttackPerformedEvent> AttackPresentationResumed { add { } remove { } }
        public bool IsAlive => true;
        public bool IsDowned => false;
        public bool IsConsuming => false;
        public bool IsEquipmentReadable => _combination != null;
        public bool IsAimReadable => _combination != null;
        public bool IsAimStance => false;
        public bool HasInputAuthority => IsAimReadable;
        public bool IsDefending => _combination?.Action == AttackPreviewAction.Defend &&
            (HasEligibleShield(false) || HasEligibleShield(true));
        public Vector2 AimDirection => IsAimReadable ? FacingDirection : Vector2.zero;
        public Vector2 FacingDirection => _combination == null ? Vector2.down : Directions[_combination.FacingIndex];
        public NetworkBool IsMoving => false;
        public NetworkBool IsControlEnabled => IsEquipmentReadable;
        public int ObservedEquipmentRevision { get; private set; }
        public WeaponSetSlot ActiveWeaponSetSlot => IsEquipmentReadable ? WeaponSetSlot.SetA : WeaponSetSlot.None;
        public EquipmentSlot GetActiveHandSlot(bool offHand) => offHand
            ? EquipmentSlotRules.GetOffHandSlot(ActiveWeaponSetSlot) : EquipmentSlotRules.GetMainHandSlot(ActiveWeaponSetSlot);

        public bool TryGetAttackPresentationSeconds(in AttackPerformedEvent attack, out float seconds)
        {
            seconds = 0f;
            if (!IsEquipmentReadable || !EquipmentSlotRules.IsHandSlot(attack.SourceSlot) ||
                EquipmentSlotRules.GetWeaponSet(attack.SourceSlot) != WeaponSetSlot.SetA) return false;
            var hand = attack.IsOffHand ? _off : _main;
            if (!hand.Enabled || attack.Sequence != hand.Attack.Sequence) return false;
            // SimulationTick is the confirmed start, not event receipt time. This preview's start tick is zero.
            seconds = _seconds;
            return seconds >= 0f;
        }
        public bool TryGetAimStanceElapsedSeconds(out float seconds) { seconds = 0f; return false; }
        public bool TryGetWeaponByCatalogIndexPlusOne(int indexPlusOne, out LootDefinition definition)
        {
            definition = null;
            if (indexPlusOne <= 0 || indexPlusOne > _catalog.Count) return false;
            definition = _catalog[indexPlusOne - 1];
            return definition != null && definition.WeaponDefinition != null;
        }
        public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
        {
            definition = null;
            if (_combination == null || slot == EquipmentSlot.None) return false;
            definition = slot == GetActiveHandSlot(false) ? _combination.MainHand
                : slot == GetActiveHandSlot(true) ? _combination.OffHand : null;
            return definition != null;
        }
        public bool TryGetEquippedDefinition(out LootDefinition definition) =>
            TryGetSlotDefinition(EquipmentSlotRules.GetMainHandSlot(ActiveWeaponSetSlot), out definition);
        public bool TryGetConsumingDefinition(out LootDefinition definition) { definition = null; return false; }
    }
#endif

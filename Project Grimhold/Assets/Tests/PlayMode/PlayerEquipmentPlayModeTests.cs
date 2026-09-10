#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Equipment
{
    /// <summary>
    /// Exercises the authoritative Equipment invariants over the eight MVP slots through the real
    /// request path: Input Authority expresses intent, State Authority validates and commits.
    /// Armor definitions are built in memory because no production armor content exists yet.
    /// </summary>
    public sealed class PlayerEquipmentPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string MeleeWeaponPath =
            "Assets/Scriptable Objects/Loot/Definitions/TrainingSword.asset";
        private const string RangedWeaponPath =
            "Assets/Scriptable Objects/Loot/Definitions/Wand.asset";
        private const string GreatswordPath =
            "Assets/Scriptable Objects/Loot/Definitions/Greatsword.asset";

        private NetworkRunner _runner;
        private PlayerEquipmentSimulationDriver _driver;
        private PrimaryAttackStatusSimulationDriver _cooldownDriver;
        private PlayerWeaponEquipmentNetworkController _equipment;
        private PlayerLootReceiver _receiver;
        private PlayerCombatNetworkController _combat;
        private PlayerCharacter _character;
        private PlayerStaminaNetworkController _stamina;
        private NetworkRaidParticipant _participant;
        private RuntimeAttributeOverrideNetworkController _attributeOverride;

        private LootDefinition _meleeWeapon;
        private LootDefinition _rangedWeapon;
        private LootDefinition _greatsword;
        private LootDefinition _helmet;
        private LootDefinition _armor;
        private LootDefinition _gloves;
        private LootDefinition _boots;
        private LootDefinition _trinket;
        private LootDefinitionCatalog _catalog;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner != null && _runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
            }

            _runner = null;
            EquipmentTestContent.Cleanup();
        }

        [UnityTest]
        public IEnumerator EquipAndUnequip_MovesExactlyOneUnitForEverySlot()
        {
            yield return StartRaidPlayer();

            var expectations = new (LootDefinition Definition, EquipmentSlot Slot)[]
            {
                (_meleeWeapon, EquipmentSlot.WeaponSetAMainHand),
                (_helmet, EquipmentSlot.Helmet),
                (_armor, EquipmentSlot.Armor),
                (_gloves, EquipmentSlot.Gloves),
                (_boots, EquipmentSlot.Boots)
            };

            for (int index = 0; index < expectations.Length; index++)
            {
                (LootDefinition definition, EquipmentSlot slot) = expectations[index];
                int inventoryBefore = _receiver.GetLootAmount(definition.LootId);

                yield return Equip(definition, EquipmentOperationResult.Succeeded);

                Assert.That(_equipment.IsSlotOccupied(slot), Is.True, slot.ToString());
                Assert.That(_equipment.TryGetSlotLoot(slot, out LootEntry entry), Is.True);
                Assert.That(entry.LootId, Is.EqualTo(definition.LootId));
                Assert.That(entry.Amount, Is.EqualTo(1), "Equipment always owns a single unit.");
                Assert.That(
                    _receiver.GetLootAmount(definition.LootId),
                    Is.EqualTo(inventoryBefore - 1),
                    $"Equipping {slot} must remove exactly one unit.");
            }

            for (int index = 0; index < expectations.Length; index++)
            {
                (LootDefinition definition, EquipmentSlot slot) = expectations[index];
                int inventoryBefore = _receiver.GetLootAmount(definition.LootId);

                yield return Unequip(slot, EquipmentOperationResult.Succeeded);

                Assert.That(_equipment.IsSlotOccupied(slot), Is.False, slot.ToString());
                Assert.That(_equipment.TryGetSlotLoot(slot, out _), Is.False);
                Assert.That(
                    _receiver.GetLootAmount(definition.LootId),
                    Is.EqualTo(inventoryBefore + 1),
                    $"Unequipping {slot} must return exactly one unit.");
            }

            Assert.That(_equipment.HasAnyEquipment, Is.False);
        }

        [UnityTest]
        public IEnumerator CopyStateFrom_PreservesRaidParticipantIdsAndEquipmentOrigins()
        {
            yield return StartRaidPlayer();
            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);

            PlayerRaidLootOriginState source =
                _receiver.GetComponent<PlayerRaidLootOriginState>();
            NetworkObject restoredObject = Spawn(PlayerPrefabGuid, PlayerRef.None);
            PlayerRaidLootOriginState restored =
                restoredObject.GetComponent<PlayerRaidLootOriginState>();

            Assert.That(source, Is.Not.Null);
            Assert.That(restored, Is.Not.Null);
            restored.CopyStateFrom(source);

            Assert.That(
                source.TryGetInventoryEntries(_catalog, out IReadOnlyList<RaidLootOriginEntry> expected),
                Is.True);
            Assert.That(
                restored.TryGetInventoryEntries(_catalog, out IReadOnlyList<RaidLootOriginEntry> actual),
                Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(
                source.TryGetEquipmentOrigin(EquipmentSlot.WeaponSetAMainHand, out RaidLootOrigin expectedOrigin),
                Is.True);
            Assert.That(
                restored.TryGetEquipmentOrigin(EquipmentSlot.WeaponSetAMainHand, out RaidLootOrigin actualOrigin),
                Is.True);
            Assert.That(actualOrigin, Is.EqualTo(expectedOrigin));
        }

        [UnityTest]
        public IEnumerator ArmorPiece_OnlyReachesItsOwnSlotAndLeavesTheOthersEmpty()
        {
            yield return StartRaidPlayer();

            var pieces = new (LootDefinition Definition, EquipmentSlot Slot)[]
            {
                (_helmet, EquipmentSlot.Helmet),
                (_armor, EquipmentSlot.Armor),
                (_gloves, EquipmentSlot.Gloves),
                (_boots, EquipmentSlot.Boots)
            };

            for (int index = 0; index < pieces.Length; index++)
            {
                yield return Equip(pieces[index].Definition, EquipmentOperationResult.Succeeded);
            }

            for (int index = 0; index < pieces.Length; index++)
            {
                (LootDefinition definition, EquipmentSlot expectedSlot) = pieces[index];
                Assert.That(_equipment.TryGetSlotDefinition(expectedSlot, out LootDefinition resolved), Is.True);
                Assert.That(resolved.LootId, Is.EqualTo(definition.LootId));

                EquipmentSlot[] slots = PlayerWeaponEquipmentNetworkController.AllSlots;
                for (int other = 0; other < slots.Length; other++)
                {
                    if (slots[other] == expectedSlot ||
                        !_equipment.TryGetSlotLoot(slots[other], out LootEntry occupant))
                    {
                        continue;
                    }

                    Assert.That(
                        occupant.LootId,
                        Is.Not.EqualTo(definition.LootId),
                        $"{definition.Id} leaked into {slots[other]}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator OccupiedArmorSlot_RejectsASecondPieceWithoutMutatingAnyState()
        {
            yield return StartRaidPlayer();
            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);

            int inventoryBefore = _receiver.GetLootAmount(_helmet.LootId);
            int revisionBefore = _equipment.ObservedEquipmentRevision;
            Assert.That(inventoryBefore, Is.GreaterThan(0), "The fixture must own a spare helmet.");
            Assert.That(_equipment.CanEquip(_helmet.LootId, EquipmentSlot.Helmet), Is.False);

            // The client-side guard already refuses, so drive the authority path directly to
            // prove the rejection is authoritative and not merely a UI convenience.
            yield return EquipThroughAuthority(_helmet, EquipmentOperationResult.SlotOccupied);

            Assert.That(_receiver.GetLootAmount(_helmet.LootId), Is.EqualTo(inventoryBefore));
            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(revisionBefore));
            Assert.That(_equipment.TryGetSlotLoot(EquipmentSlot.Helmet, out LootEntry entry), Is.True);
            Assert.That(entry.LootId, Is.EqualTo(_helmet.LootId));
        }

        [UnityTest]
        public IEnumerator NonEquippableLoot_IsRejectedWithoutMutatingAnyState()
        {
            yield return StartRaidPlayer();

            int inventoryBefore = _receiver.GetLootAmount(_trinket.LootId);
            int revisionBefore = _equipment.ObservedEquipmentRevision;

            Assert.That(_equipment.CanEquip(_trinket.LootId, EquipmentSlot.WeaponSetAMainHand), Is.False);
            Assert.That(_equipment.TryRequestEquip(_trinket.LootId, EquipmentSlot.WeaponSetAMainHand), Is.False);
            yield return EquipThroughAuthority(_trinket, EquipmentOperationResult.InvalidEquipment);

            Assert.That(_receiver.GetLootAmount(_trinket.LootId), Is.EqualTo(inventoryBefore));
            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(revisionBefore));
            Assert.That(_equipment.HasAnyEquipment, Is.False);
        }

        [UnityTest]
        public IEnumerator WeaponWithUnmetRequirements_IsRejectedByAuthorityWithoutMutation()
        {
            yield return StartRaidPlayer(CreateAttributes(strength: 5));

            int inventoryBefore = _receiver.GetLootAmount(_greatsword.LootId);
            int revisionBefore = _equipment.ObservedEquipmentRevision;

            Assert.That(_equipment.CanEquip(_greatsword.LootId, EquipmentSlot.WeaponSetAMainHand), Is.False);
            Assert.That(_equipment.TryRequestEquip(_greatsword.LootId, EquipmentSlot.WeaponSetAMainHand), Is.False);
            yield return EquipThroughAuthority(
                _greatsword,
                EquipmentOperationResult.AttributeRequirementsNotMet);

            Assert.That(_receiver.GetLootAmount(_greatsword.LootId), Is.EqualTo(inventoryBefore));
            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(revisionBefore));
            Assert.That(_equipment.HasAnyEquipment, Is.False);
            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.None));
        }

        [UnityTest]
        public IEnumerator WeaponWithSatisfiedRequirements_CanEquipAndBecomeActive()
        {
            yield return StartRaidPlayer(CreateAttributes(strength: 10));

            yield return Equip(_greatsword, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
            Assert.That(
                _equipment.TryGetEquippedDefinition(out LootDefinition equipped),
                Is.True);
            Assert.That(equipped, Is.SameAs(_greatsword));
        }

        [UnityTest]
        public IEnumerator RuntimeAttributeOverride_DrivesRequirementsScalingAndReset()
        {
            yield return StartRaidPlayer(CreateAttributes(strength: 5));

            Assert.That(
                _equipment.CanEquip(_greatsword.LootId, EquipmentSlot.WeaponSetAMainHand),
                Is.False);
            Assert.That(_attributeOverride.RequestAdjustment(CharacterAttribute.Strength, 5), Is.True);
            yield return WaitUntil(
                () => TryGetEffectiveStrength(out int value) && value == 10,
                "The first authoritative Strength override was not applied.");
            Assert.That(_attributeOverride.RequestAdjustment(CharacterAttribute.Strength, 5), Is.True);
            yield return WaitUntil(
                () => TryGetEffectiveStrength(out int value) && value == 15,
                "Persistent Strength 5 plus the runtime +10 did not produce 15.");

            Assert.That(
                _equipment.CanEquip(_greatsword.LootId, EquipmentSlot.WeaponSetAMainHand),
                Is.True);
            yield return Equip(_greatsword, EquipmentOperationResult.Succeeded);
            AssertRuntimeParameters(
                _equipment.GetComponent<MeleeAttack>(),
                48f,
                DamageType.Physical,
                1.4f,
                2f,
                10f);

            Assert.That(_attributeOverride.RequestAdjustment(CharacterAttribute.Strength, 5), Is.True);
            yield return WaitUntil(
                () => TryGetRuntimeDamage(_equipment.GetComponent<MeleeAttack>(), out float damage) &&
                    Mathf.Approximately(damage, 49f),
                "The active weapon did not rebuild scaling from effective Strength 20.");

            Assert.That(_attributeOverride.RequestReset(CharacterAttribute.Strength), Is.True);
            yield return WaitUntil(
                () => TryGetEffectiveStrength(out int value) && value == 5 &&
                    _equipment.ActiveWeaponSetSlot == WeaponSetSlot.None,
                "Reset did not restore persistent Strength and invalidate the active weapon.");
            Assert.That(
                _participant.TryGetPersistentCharacterAttributeState(out CharacterAttributeState persistent),
                Is.True);
            Assert.That(persistent.Strength, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator PreparedWeaponWithUnmetRequirements_IsRejectedBeforeInventoryMutation()
        {
            yield return StartRaidPlayer(CreateAttributes(strength: 5));
            int inventoryBefore = _receiver.GetLootAmount(_greatsword.LootId);
            int revisionBefore = _equipment.ObservedEquipmentRevision;
            var reserved = new[] { new LootEntry(_greatsword.LootId, inventoryBefore) };
            var indices = new[] { 1, 0, 0, 0, 0, 0, 0, 0 };

            Assert.That(
                _equipment.TryInitializePreparedEquipment(reserved, indices, WeaponSetSlot.SetA, out string error),
                Is.False);
            Assert.That(error, Does.Contain("attribute requirements"));
            Assert.That(_receiver.GetLootAmount(_greatsword.LootId), Is.EqualTo(inventoryBefore));
            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(revisionBefore));
            Assert.That(_equipment.HasAnyEquipment, Is.False);
        }

        [UnityTest]
        public IEnumerator UnequipWithFullInventory_LeavesInventoryAndEquipmentUnchanged()
        {
            yield return StartRaidPlayer();
            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);

            // Shrink the inventory to a capacity it already fills, so returning the helmet would
            // need a new stack that cannot fit.
            yield return SyncInventory(new[]
            {
                new LootEntry(_meleeWeapon.LootId, 1),
                new LootEntry(_trinket.LootId, 1)
            });
            EquipmentTestContent.SetField(_receiver, "_slotCapacity", _receiver.OccupiedSlotCount);

            Assert.That(_receiver.OccupiedSlotCount, Is.EqualTo(_receiver.SlotCapacity));
            Assert.That(_receiver.GetLootAmount(_helmet.LootId), Is.Zero);
            int revisionBefore = _equipment.ObservedEquipmentRevision;
            IReadOnlyList<LootEntry> inventoryBefore = _receiver.GetLootContent();
            int distinctBefore = inventoryBefore.Count;

            yield return Unequip(EquipmentSlot.Helmet, EquipmentOperationResult.InventoryFull);

            Assert.That(_receiver.GetLootContent().Count, Is.EqualTo(distinctBefore));
            Assert.That(_receiver.GetLootAmount(_helmet.LootId), Is.Zero);
            Assert.That(_equipment.IsSlotOccupied(EquipmentSlot.Helmet), Is.True);
            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(revisionBefore));
        }

        [UnityTest]
        public IEnumerator SecondMainHand_FillsTheOtherWeaponSetAndKeepsTheActiveOne()
        {
            yield return StartRaidPlayer();

            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);
            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));

            yield return Equip(_rangedWeapon, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.TryGetSlotLoot(EquipmentSlot.WeaponSetAMainHand, out LootEntry first), Is.True);
            Assert.That(first.LootId, Is.EqualTo(_meleeWeapon.LootId), "The first weapon was replaced.");
            Assert.That(_equipment.TryGetSlotLoot(EquipmentSlot.WeaponSetBMainHand, out LootEntry second), Is.True);
            Assert.That(second.LootId, Is.EqualTo(_rangedWeapon.LootId));
            Assert.That(
                _equipment.ActiveWeaponSetSlot,
                Is.EqualTo(WeaponSetSlot.SetA),
                "Inserting an inactive weapon must not change the active selection.");

            // Explicit targeting replaces only the requested Set's Main Hand.
            yield return EquipThroughAuthority(
                _meleeWeapon,
                EquipmentSlot.WeaponSetAMainHand,
                EquipmentOperationResult.Succeeded);
        }

        [UnityTest]
        public IEnumerator TwoHandedWeapon_DisplacesBothHandsWithoutChangingTheOtherSet()
        {
            yield return StartRaidPlayer(CreateAttributes(strength: 10));

            Assert.That(
                _equipment.TryRequestEquip(_meleeWeapon.LootId, EquipmentSlot.WeaponSetAMainHand),
                Is.True);
            yield return AwaitResolution(EquipmentOperationResult.Succeeded);
            Assert.That(
                _equipment.TryRequestEquip(_rangedWeapon.LootId, EquipmentSlot.WeaponSetAOffHand),
                Is.True);
            yield return AwaitResolution(EquipmentOperationResult.Succeeded);
            Assert.That(
                _equipment.TryRequestEquip(_meleeWeapon.LootId, EquipmentSlot.WeaponSetBMainHand),
                Is.True);
            yield return AwaitResolution(EquipmentOperationResult.Succeeded);

            yield return EquipThroughAuthority(
                _greatsword,
                EquipmentSlot.WeaponSetAMainHand,
                EquipmentOperationResult.Succeeded);

            Assert.That(
                _equipment.TryGetSlotLoot(EquipmentSlot.WeaponSetAMainHand, out LootEntry setAMain),
                Is.True);
            Assert.That(setAMain.LootId, Is.EqualTo(_greatsword.LootId));
            Assert.That(_equipment.IsSlotOccupied(EquipmentSlot.WeaponSetAOffHand), Is.False);
            Assert.That(
                _equipment.TryGetSlotLoot(EquipmentSlot.WeaponSetBMainHand, out LootEntry setBMain),
                Is.True);
            Assert.That(setBMain.LootId, Is.EqualTo(_meleeWeapon.LootId));
            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
            Assert.That(ResolveActiveWeapon().LootId, Is.EqualTo(_greatsword.LootId));
            Assert.That(_receiver.GetLootAmount(_meleeWeapon.LootId), Is.EqualTo(1));
            Assert.That(_receiver.GetLootAmount(_rangedWeapon.LootId), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ArmorMutations_NeverReconfigureTheActiveAttack()
        {
            yield return StartRaidPlayer();
            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);

            Assert.That(_combat.TryGetPrimaryAttackStatus(out _), Is.True);
            LootDefinition activeBefore = ResolveActiveWeapon();

            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);
            yield return Equip(_boots, EquipmentOperationResult.Succeeded);
            yield return Unequip(EquipmentSlot.Helmet, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetA));
            Assert.That(ResolveActiveWeapon(), Is.SameAs(activeBefore));
            Assert.That(_combat.TryGetPrimaryAttackStatus(out _), Is.True);
        }

        [UnityTest]
        public IEnumerator ArmorMutations_RebuildRuntimeStatisticsAndPreserveCurrentResources()
        {
            yield return StartRaidPlayer();

            AssertRuntimeStatistics(100, 100, 100, 0, 0);
            Assert.That(_character.Health, Is.EqualTo(100f));
            Assert.That(_stamina.CurrentStamina, Is.EqualTo(100f));

            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);
            AssertRuntimeStatistics(120, 100, 100, 10, 1);
            Assert.That(_character.Health, Is.EqualTo(100f), "Increasing Max Health must not heal.");

            HealResult heal = _character.ApplyHealing(new HealRequest(20f));
            Assert.That(heal.Success, Is.True);
            Assert.That(_character.Health, Is.EqualTo(120f));

            yield return Unequip(EquipmentSlot.Helmet, EquipmentOperationResult.Succeeded);
            yield return WaitUntil(
                () => Mathf.Approximately(_character.Health, 100f),
                "Removing a Health bonus did not clamp current Health.");
            AssertRuntimeStatistics(100, 100, 100, 0, 0);

            yield return Equip(_armor, EquipmentOperationResult.Succeeded);
            AssertRuntimeStatistics(100, 130, 100, 20, 2);
            Assert.That(_stamina.CurrentStamina, Is.EqualTo(100f), "Increasing Max Stamina must not recover.");

            EquipmentTestContent.SetField(_stamina, "_regenerationPerSecond", 10_000f);
            EquipmentTestContent.SetField(_stamina, "_regenerationDelaySeconds", 0f);
            yield return WaitUntil(
                () => Mathf.Approximately(_stamina.CurrentStamina, 130f),
                "The fixture could not raise current Stamina to the equipment-adjusted maximum.");

            yield return Unequip(EquipmentSlot.Armor, EquipmentOperationResult.Succeeded);
            yield return WaitUntil(
                () => Mathf.Approximately(_stamina.CurrentStamina, 100f),
                "Removing a Stamina bonus did not clamp current Stamina.");

            yield return Equip(_gloves, EquipmentOperationResult.Succeeded);
            AssertRuntimeStatistics(100, 100, 140, 3, 30);
        }

        [UnityTest]
        public IEnumerator EquippedArmor_MitigatesOnlyCompatibleDamageTypes()
        {
            yield return StartRaidPlayer();
            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);

            DamageResult physical = ApplyDamage(50f, DamageType.Physical);
            DamageResult magical = ApplyDamage(25f, DamageType.Magical);
            DamageResult trueDamage = ApplyDamage(1f, DamageType.TrueDamage);

            Assert.That(physical.AppliedDamage, Is.EqualTo(45f));
            Assert.That(magical.AppliedDamage, Is.EqualTo(24f));
            Assert.That(trueDamage.AppliedDamage, Is.EqualTo(1f));
            Assert.That(_character.Health, Is.EqualTo(30f));
        }

        [UnityTest]
        public IEnumerator EffectiveAttributeRevision_InvalidatesFinalStatisticsWithoutEquipmentChange()
        {
            yield return StartRaidPlayer();
            int equipmentRevision = _equipment.ObservedEquipmentRevision;

            Assert.That(_attributeOverride.RequestAdjustment(CharacterAttribute.Vitality, 5), Is.True);
            yield return WaitUntil(
                () => TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics) &&
                    statistics.MaximumHealth == 125,
                "A Vitality revision did not invalidate final runtime statistics.");

            Assert.That(_attributeOverride.RequestAdjustment(CharacterAttribute.Resistance, 5), Is.True);
            yield return WaitUntil(
                () => TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics) &&
                    statistics.MaximumStamina == 125,
                "A Resistance revision did not invalidate final runtime statistics.");

            Assert.That(_equipment.ObservedEquipmentRevision, Is.EqualTo(equipmentRevision));
            Assert.That(_character.Health, Is.EqualTo(100f), "Increasing Vitality must not heal.");
            Assert.That(_stamina.CurrentStamina, Is.EqualTo(100f), "Increasing Resistance must not recover Stamina.");
        }

        [UnityTest]
        public IEnumerator UnequippingTheActiveWeapon_FallsBackToTheOtherValidWeaponSet()
        {
            yield return StartRaidPlayer();
            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);
            AssertRuntimeParameters(
                _equipment.GetComponent<MeleeAttack>(),
                22f,
                DamageType.Physical,
                1f,
                1.5f,
                5f);
            yield return Equip(_rangedWeapon, EquipmentOperationResult.Succeeded);

            AssertRuntimeParameters(
                _equipment.GetComponent<RangedAttack>(),
                0f,
                DamageType.Physical,
                0f,
                0f,
                0f);

            yield return Unequip(EquipmentSlot.WeaponSetAMainHand, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetB));
            Assert.That(ResolveActiveWeapon().LootId, Is.EqualTo(_rangedWeapon.LootId));
            AssertRuntimeParameters(
                _equipment.GetComponent<RangedAttack>(),
                22f,
                DamageType.Magical,
                0.7f,
                5f,
                0f);

            yield return Unequip(EquipmentSlot.WeaponSetBMainHand, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.None));
            Assert.That(_equipment.HasAnyWeapon, Is.False);
            Assert.That(_combat.TryGetPrimaryAttackStatus(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator RebuildingForTheRemainingWeapon_PreservesAnExistingCooldown()
        {
            yield return StartRaidPlayer();
            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);
            yield return Equip(_rangedWeapon, EquipmentOperationResult.Succeeded);

            _cooldownDriver.Target = _combat;
            _cooldownDriver.RequestedCooldownSeconds = 2f;
            _cooldownDriver.IsRequested = true;
            yield return WaitUntil(
                () => !_cooldownDriver.IsRequested,
                "The authoritative cooldown setup did not run.");
            Assert.That(_combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus before), Is.True);
            Assert.That(before.IsAvailable, Is.False);

            yield return Unequip(EquipmentSlot.WeaponSetAMainHand, EquipmentOperationResult.Succeeded);

            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.SetB));
            Assert.That(_combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus after), Is.True);
            Assert.That(after.IsAvailable, Is.False);
            Assert.That(after.CooldownDurationSeconds, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(after.CooldownRemainingSeconds, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator ExpeditionSnapshot_AggregatesAndClearsAllEightSlots()
        {
            yield return StartRaidPlayer();
            yield return Equip(_meleeWeapon, EquipmentOperationResult.Succeeded);
            yield return Equip(_rangedWeapon, EquipmentOperationResult.Succeeded);
            yield return Equip(_helmet, EquipmentOperationResult.Succeeded);
            yield return Equip(_armor, EquipmentOperationResult.Succeeded);
            yield return Equip(_gloves, EquipmentOperationResult.Succeeded);
            yield return Equip(_boots, EquipmentOperationResult.Succeeded);

            Assert.That(
                PlayerExpeditionLootSnapshot.TryCapture(
                    _receiver, _equipment, out PlayerExpeditionLootSnapshot snapshot, out string error),
                Is.True,
                error);

            Assert.That(snapshot.WeaponSetAMainHand.HasValue, Is.True);
            Assert.That(snapshot.WeaponSetBMainHand.HasValue, Is.True);
            Assert.That(snapshot.Helmet.Value.LootId, Is.EqualTo(_helmet.LootId));
            Assert.That(snapshot.Armor.Value.LootId, Is.EqualTo(_armor.LootId));
            Assert.That(snapshot.Gloves.Value.LootId, Is.EqualTo(_gloves.LootId));
            Assert.That(snapshot.Boots.Value.LootId, Is.EqualTo(_boots.LootId));

            foreach (LootDefinition equipped in
                     new[] { _helmet, _armor, _gloves, _boots })
            {
                int inInventory = _receiver.GetLootAmount(equipped.LootId);
                int inCombined = TotalIn(snapshot.Combined, equipped.LootId);
                Assert.That(
                    inCombined,
                    Is.EqualTo(inInventory + 1),
                    $"Combined must aggregate the equipped {equipped.Id} exactly once.");
            }

            var profile = new ProfileId("equipment-owner");
            RaidTeamId.TryCreate(1, out RaidTeamId teamId);
            Assert.That(
                RaidInitialAffiliationSnapshot.TryCreate(
                    new[] { new RaidLaunchParticipant(profile, teamId) },
                    out RaidInitialAffiliationSnapshot affiliations),
                Is.True);
            RaidParticipantIdAssignment.TryResolve(
                new[] { profile }, profile, out RaidParticipantId extractorId);
            Assert.That(
                RaidLootEligibilityResolver.TryResolve(
                    extractorId,
                    snapshot,
                    affiliations,
                    out RaidLootEligibilitySnapshot eligibility,
                    out error),
                Is.True,
                error);
            Assert.That(eligibility.TotalAmount, Is.GreaterThan(0));
            Assert.That(eligibility.EligibleAmount, Is.Zero,
                "Inventory and Equipment introduced by the extractor must remain ineligible.");

            Assert.That(snapshot.MatchesCurrent(_receiver, _equipment, out error), Is.True, error);
            Assert.That(snapshot.TryClearExact(_receiver, _equipment, out error), Is.True, error);

            Assert.That(_equipment.HasAnyEquipment, Is.False, "Every slot must be cleared exactly once.");
            Assert.That(_equipment.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.None));
            Assert.That(_receiver.GetLootContent(), Is.Empty);
        }

        // ---- fixture -------------------------------------------------------------------------

        private IEnumerator StartRaidPlayer(CharacterAttributeState? admittedAttributes = null)
        {
            // The player prefab logs missing dependencies for systems this fixture does not host.
            LogAssert.ignoreFailingMessages = true;
            yield return StartRunner();

            CharacterAttributeState attributes = admittedAttributes ??
                ProgressionBalanceDefaults.InitialCharacterAttributeState;
            NetworkObject participantObject = SpawnParticipant(attributes);
            _participant = participantObject.GetComponent<NetworkRaidParticipant>();
            _attributeOverride = participantObject.GetComponent<RuntimeAttributeOverrideNetworkController>();
            NetworkObject playerObject = SpawnPlayer(participantObject);
            _equipment = playerObject.GetComponent<PlayerWeaponEquipmentNetworkController>();
            _receiver = playerObject.GetComponent<PlayerLootReceiver>();
            _combat = playerObject.GetComponent<PlayerCombatNetworkController>();
            _character = playerObject.GetComponent<PlayerCharacter>();
            _stamina = playerObject.GetComponent<PlayerStaminaNetworkController>();
            Assert.That(_equipment, Is.Not.Null);
            Assert.That(_receiver, Is.Not.Null);
            Assert.That(_combat, Is.Not.Null);
            Assert.That(_character, Is.Not.Null);
            Assert.That(_stamina, Is.Not.Null);
            Assert.That(_participant, Is.Not.Null);
            Assert.That(_attributeOverride, Is.Not.Null);
            Assert.That(_equipment.HasStateAuthority, Is.True);
            Assert.That(_equipment.HasInputAuthority, Is.True);

            BuildTestContent();
            EquipmentTestContent.SetField(_equipment, "_lootCatalog", _catalog);
            EquipmentTestContent.SetField(_receiver, "_lootCatalog", _catalog);

            yield return SyncInventory(new[]
            {
                new LootEntry(_meleeWeapon.LootId, 2),
                new LootEntry(_rangedWeapon.LootId, 1),
                new LootEntry(_greatsword.LootId, 1),
                new LootEntry(_helmet.LootId, 2),
                new LootEntry(_armor.LootId, 1),
                new LootEntry(_gloves.LootId, 1),
                new LootEntry(_boots.LootId, 1),
                new LootEntry(_trinket.LootId, 1)
            });
        }

        private void BuildTestContent()
        {
            _meleeWeapon = AssetDatabase.LoadAssetAtPath<LootDefinition>(MeleeWeaponPath);
            _rangedWeapon = AssetDatabase.LoadAssetAtPath<LootDefinition>(RangedWeaponPath);
            _greatsword = AssetDatabase.LoadAssetAtPath<LootDefinition>(GreatswordPath);
            Assert.That(_meleeWeapon, Is.Not.Null, MeleeWeaponPath);
            Assert.That(_rangedWeapon, Is.Not.Null, RangedWeaponPath);
            Assert.That(_greatsword, Is.Not.Null, GreatswordPath);

            _helmet = EquipmentTestContent.CreateArmorDefinition(
                "test_helmet", LootCategory.Helmet, 10, 1, MaximumResourceType.Health, 20);
            _armor = EquipmentTestContent.CreateArmorDefinition(
                "test_armor", LootCategory.Armor, 20, 2, MaximumResourceType.Stamina, 30);
            _gloves = EquipmentTestContent.CreateArmorDefinition(
                "test_gloves", LootCategory.Gloves, 3, 30, MaximumResourceType.Mana, 40);
            _boots = EquipmentTestContent.CreateArmorDefinition(
                "test_boots", LootCategory.Boots, 4, 40, MaximumResourceType.Health, 5);
            _trinket = EquipmentTestContent.CreateNonEquippableDefinition("test_trinket");

            _catalog = EquipmentTestContent.CreateCatalog(
                _meleeWeapon, _rangedWeapon, _greatsword,
                _helmet, _armor, _gloves, _boots, _trinket);
        }

        private NetworkObject SpawnParticipant(CharacterAttributeState attributes)
        {
            RaidParticipantId.TryCreate(1, out RaidParticipantId participantId);
            NetworkObject prefab = LoadPrefab(ParticipantPrefabGuid);
            return _runner.Spawn(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        "equipment-test-profile",
                        participantId,
                        attributes,
                        ExperienceCurve.InitialLevel,
                        0,
                        "equipment-test-generation"));
        }

        private NetworkObject SpawnPlayer(NetworkObject participantObject)
        {
            NetworkObject prefab = LoadPrefab(PlayerPrefabGuid);
            return _runner.Spawn(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
        }

        private NetworkObject LoadPrefab(string prefabGuid)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(prefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, prefabGuid);
            return prefab;
        }

        private static CharacterAttributeState CreateAttributes(int strength)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    5, 5, strength, 5, 5, 5, 0, out CharacterAttributeState attributes),
                Is.True);
            return attributes;
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("PlayerEquipmentTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<PlayerEquipmentSimulationDriver>();
            _cooldownDriver = runnerObject.AddComponent<PrimaryAttackStatusSimulationDriver>();
            _runner.ProvideInput = true;

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"equipment-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });

            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkObject Spawn(string prefabGuid, PlayerRef inputAuthority)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(prefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, prefabGuid);
            return _runner.Spawn(prefab, Vector3.zero, Quaternion.identity, inputAuthority);
        }

        private IEnumerator SyncInventory(IReadOnlyList<LootEntry> items)
        {
            int previous = _driver.CompletionSequence;
            _driver.RequestForceSyncLoadout(_receiver, items);
            yield return WaitUntil(
                () => _driver.CompletionSequence != previous,
                "The inventory setup never ran inside Fusion simulation.");
            Assert.That(_driver.LastResult, Is.True, _driver.LastError);
        }

        /// <summary>Drives the full Input Authority intention and waits for the confirmation.</summary>
        private IEnumerator Equip(LootDefinition definition, EquipmentOperationResult expected)
        {
            Assert.That(
                _equipment.TryRequestEquip(definition.LootId, ResolveTestTarget(definition)),
                Is.True,
                $"The equip intention for {definition.Id} was refused before reaching authority.");
            yield return AwaitResolution(expected);
        }

        /// <summary>
        /// Bypasses the client-side guard so an authoritative rejection can be observed directly.
        /// </summary>
        private IEnumerator EquipThroughAuthority(
            LootDefinition definition,
            EquipmentOperationResult expected)
            => EquipThroughAuthority(definition, ResolveTestTarget(definition), expected);

        private IEnumerator EquipThroughAuthority(
            LootDefinition definition,
            EquipmentSlot slot,
            EquipmentOperationResult expected)
        {
            Assert.That(_catalog.TryGetIndex(definition.LootId, out int catalogIndex), Is.True);
            yield return InvokeAuthorityRequest(kind: 1, catalogIndex, (int)slot, expected);
        }

        private EquipmentSlot ResolveTestTarget(LootDefinition definition)
        {
            EquipmentSlot fixedSlot = EquipmentSlotRules.ResolveFixedSlot(definition.Category);
            if (fixedSlot != EquipmentSlot.None) return fixedSlot;
            return !_equipment.IsSlotOccupied(EquipmentSlot.WeaponSetAMainHand)
                ? EquipmentSlot.WeaponSetAMainHand
                : EquipmentSlot.WeaponSetBMainHand;
        }

        private IEnumerator Unequip(EquipmentSlot slot, EquipmentOperationResult expected)
        {
            if (expected == EquipmentOperationResult.Succeeded ||
                expected == EquipmentOperationResult.InventoryFull)
            {
                Assert.That(
                    _equipment.TryRequestUnequip(slot),
                    Is.True,
                    $"The unequip intention for {slot} was refused before reaching authority.");
                yield return AwaitResolution(expected);
                yield break;
            }

            yield return InvokeAuthorityRequest(kind: 2, -1, (int)slot, expected);
        }

        /// <summary>
        /// Sends the equipment request straight to State Authority, skipping the local guard that
        /// already refuses obviously invalid intentions, so authoritative rejections are observable.
        /// The request sequence is advanced exactly like the production sender does.
        /// </summary>
        private IEnumerator InvokeAuthorityRequest(
            int kind,
            int catalogIndex,
            int slotValue,
            EquipmentOperationResult expected)
        {
            FieldInfo sequenceField = typeof(PlayerWeaponEquipmentNetworkController)
                .GetField("_nextRequestSequence", BindingFlags.Instance | BindingFlags.NonPublic);
            int sequence = (int)sequenceField.GetValue(_equipment) + 1;
            sequenceField.SetValue(_equipment, sequence);

            EquipmentOperationResult observed = EquipmentOperationResult.None;
            void OnResolved(EquipmentOperationResult result) => observed = result;

            _equipment.EquipRequestResolved += OnResolved;
            try
            {
                typeof(PlayerWeaponEquipmentNetworkController)
                    .GetMethod("RPC_RequestEquipment", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_equipment, new object[] { kind, catalogIndex, slotValue, sequence, default(RpcInfo) });

                yield return WaitUntil(
                    () => observed != EquipmentOperationResult.None,
                    $"The authority never resolved the request (expected {expected}).");
            }
            finally
            {
                _equipment.EquipRequestResolved -= OnResolved;
            }

            Assert.That(observed, Is.EqualTo(expected));
        }

        private IEnumerator AwaitResolution(EquipmentOperationResult expected)
        {
            EquipmentOperationResult observed = EquipmentOperationResult.None;
            void OnResolved(EquipmentOperationResult result) => observed = result;

            _equipment.EquipRequestResolved += OnResolved;
            try
            {
                yield return WaitUntil(
                    () => observed != EquipmentOperationResult.None,
                    $"The authority never confirmed the request (expected {expected}).");
            }
            finally
            {
                _equipment.EquipRequestResolved -= OnResolved;
            }

            Assert.That(observed, Is.EqualTo(expected));
        }

        private LootDefinition ResolveActiveWeapon()
        {
            Assert.That(_equipment.TryGetEquippedDefinition(out LootDefinition definition), Is.True);
            return definition;
        }

        private static void AssertRuntimeParameters(
            MonoBehaviour attack,
            float damage,
            DamageType damageType,
            float cooldown,
            float range,
            float knockback)
        {
            FieldInfo field = attack.GetType().GetField(
                "_runtimeParameters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var actual = (AttackExecutionParameters)field.GetValue(attack);
            Assert.That(actual.Damage, Is.EqualTo(damage).Within(0.0001f));
            Assert.That(actual.DamageType, Is.EqualTo(damageType));
            Assert.That(actual.CooldownSeconds, Is.EqualTo(cooldown).Within(0.0001f));
            Assert.That(actual.Range, Is.EqualTo(range).Within(0.0001f));
            Assert.That(actual.KnockbackForce, Is.EqualTo(knockback).Within(0.0001f));
        }

        private void AssertRuntimeStatistics(
            int maximumHealth,
            int maximumStamina,
            int maximumMana,
            int physicalDefense,
            int magicalDefense)
        {
            Assert.That(_character.TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics), Is.True);
            Assert.That(statistics.MaximumHealth, Is.EqualTo(maximumHealth));
            Assert.That(statistics.MaximumStamina, Is.EqualTo(maximumStamina));
            Assert.That(statistics.MaximumMana, Is.EqualTo(maximumMana));
            Assert.That(statistics.PhysicalDefense, Is.EqualTo(physicalDefense));
            Assert.That(statistics.MagicalDefense, Is.EqualTo(magicalDefense));
        }

        private DamageResult ApplyDamage(float amount, DamageType damageType)
        {
            DamageResult result = _character.ApplyDamage(new DamageRequest(
                new EntityId(-1),
                _character.Id,
                amount,
                damageType,
                Vector2.right,
                _character.transform.position,
                _runner.Tick));
            Assert.That(result.IsApplied, Is.True, result.FailureReason.ToString());
            return result;
        }

        private bool TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics)
        {
            statistics = default;
            return _character != null && _character.TryGetRuntimeStatistics(out statistics);
        }

        private bool TryGetEffectiveStrength(out int strength)
        {
            strength = 0;
            return _participant != null &&
                _participant.TryGetCharacterAttributeState(out CharacterAttributeState attributes) &&
                attributes.TryGetValue(CharacterAttribute.Strength, out strength);
        }

        private static bool TryGetRuntimeDamage(MonoBehaviour attack, out float damage)
        {
            damage = 0f;
            if (attack == null)
            {
                return false;
            }

            FieldInfo field = attack.GetType().GetField(
                "_runtimeParameters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            damage = ((AttackExecutionParameters)field.GetValue(attack)).Damage;
            return true;
        }

        private static int TotalIn(IReadOnlyList<LootEntry> entries, LootId lootId)
        {
            int total = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].LootId == lootId)
                {
                    total += entries[index].Amount;
                }
            }

            return total;
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            int framesRemaining = 300;
            while (!predicate() && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }
    }
}
#endif

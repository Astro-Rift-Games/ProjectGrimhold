#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TownCharacterStatisticsBuilderTests
    {
        private readonly List<Object> _createdObjects = new();
        private readonly Dictionary<string, LootDefinition> _catalog = new();
        private readonly CharacterDerivedStatisticsConfiguration _configuration =
            ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration;

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
            _catalog.Clear();
        }

        [Test]
        public void TryBuild_NoEquipment_DerivesResourcesFromAttributesOnly()
        {
            CharacterAttributeState attributes = Attributes(vitality: 10, resistance: 8);

            TownCharacterStatisticsPresentation result = Build(attributes, default);

            Assert.That(result.MaximumHealth.Total, Is.EqualTo(125));
            Assert.That(result.MaximumHealth.FromAttributes, Is.EqualTo(125));
            Assert.That(result.MaximumHealth.FromEquipment, Is.Zero);
            Assert.That(result.MaximumMana.Total, Is.EqualTo(100));
            Assert.That(result.MaximumMana.FromEquipment, Is.Zero);
            Assert.That(result.MaximumStamina.Total, Is.EqualTo(115));
            Assert.That(result.MaximumStamina.FromAttributes, Is.EqualTo(115));
            Assert.That(result.MaximumStamina.FromEquipment, Is.Zero);
            Assert.That(result.PhysicalDefense, Is.Zero);
            Assert.That(result.MagicalDefense, Is.Zero);
            Assert.That(result.PhysicalMitigationPercent, Is.Zero);
            Assert.That(result.MagicalMitigationPercent, Is.Zero);
        }

        [Test]
        public void TryBuild_ArmorHealthModifier_AddsEquipmentContributionToTotal()
        {
            Register("helm", CreateArmorLoot("Helm", LootCategory.Helmet, 0, 0, MaximumResourceType.Health, 20));
            var loadout = new PreparedEquipmentLoadout(default, default, helmet: new LootId("helm"));

            TownCharacterStatisticsPresentation result = Build(Attributes(vitality: 10), loadout);

            Assert.That(result.MaximumHealth.Total, Is.EqualTo(145));
            Assert.That(result.MaximumHealth.FromAttributes, Is.EqualTo(125));
            Assert.That(result.MaximumHealth.FromEquipment, Is.EqualTo(20));
        }

        [Test]
        public void TryBuild_ArmorStaminaAndManaModifiers_AreSplitFromAttributeContribution()
        {
            Register("armor", CreateArmorLoot("Armor", LootCategory.Armor, 0, 0, MaximumResourceType.Stamina, 15));
            Register("gloves", CreateArmorLoot("Gloves", LootCategory.Gloves, 0, 0, MaximumResourceType.Mana, 30));
            var loadout = new PreparedEquipmentLoadout(
                default, default, armor: new LootId("armor"), gloves: new LootId("gloves"));

            TownCharacterStatisticsPresentation result = Build(Attributes(resistance: 8), loadout);

            Assert.That(result.MaximumStamina.Total, Is.EqualTo(130));
            Assert.That(result.MaximumStamina.FromAttributes, Is.EqualTo(115));
            Assert.That(result.MaximumStamina.FromEquipment, Is.EqualTo(15));
            Assert.That(result.MaximumMana.Total, Is.EqualTo(130));
            Assert.That(result.MaximumMana.FromAttributes, Is.EqualTo(100));
            Assert.That(result.MaximumMana.FromEquipment, Is.EqualTo(30));
        }

        [Test]
        public void TryBuild_Defense_IsSummedAcrossTheFourArmorPieces()
        {
            Register("helm", CreateArmorLoot("Helm", LootCategory.Helmet, 40, 1, MaximumResourceType.Health, 1));
            Register("armor", CreateArmorLoot("Armor", LootCategory.Armor, 30, 2, MaximumResourceType.Health, 1));
            Register("gloves", CreateArmorLoot("Gloves", LootCategory.Gloves, 20, 3, MaximumResourceType.Health, 1));
            Register("boots", CreateArmorLoot("Boots", LootCategory.Boots, 10, 4, MaximumResourceType.Health, 1));
            var loadout = new PreparedEquipmentLoadout(
                default, default,
                new LootId("helm"), new LootId("armor"), new LootId("gloves"), new LootId("boots"));

            TownCharacterStatisticsPresentation result = Build(Attributes(), loadout);

            Assert.That(result.PhysicalDefense, Is.EqualTo(100));
            Assert.That(result.MagicalDefense, Is.EqualTo(10));
            Assert.That(result.PhysicalMitigationPercent, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(result.MagicalMitigationPercent, Is.EqualTo(10f / 110f * 100f).Within(0.0001f));
        }

        [Test]
        public void TryBuild_ZeroDefense_HasZeroMitigation()
        {
            TownCharacterStatisticsPresentation result = Build(Attributes(), default);

            Assert.That(result.PhysicalMitigationPercent, Is.Zero);
            Assert.That(result.MagicalMitigationPercent, Is.Zero);
        }

        [Test]
        public void TryBuild_MitigationUsesTheSuppliedConstant()
        {
            Register("armor", CreateArmorLoot("Armor", LootCategory.Armor, 100, 0, MaximumResourceType.Health, 1));
            var loadout = new PreparedEquipmentLoadout(default, default, armor: new LootId("armor"));

            TownCharacterStatisticsPresentation result = Build(Attributes(), loadout, mitigationConstant: 300f);

            Assert.That(result.PhysicalMitigationPercent, Is.EqualTo(25f).Within(0.0001f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryBuild_InvalidMitigationConstant_FailsWithoutThrowing(float constant)
        {
            CharacterAttributeState attributes = Attributes();
            PreparedEquipmentLoadout loadout = default;

            bool built = TownCharacterStatisticsBuilder.TryBuild(
                attributes, loadout, Resolve, _configuration, constant,
                out TownCharacterStatisticsPresentation presentation, out string failure);

            Assert.That(built, Is.False);
            Assert.That(failure, Is.Not.Null.And.Not.Empty);
            Assert.That(presentation, Is.EqualTo(default(TownCharacterStatisticsPresentation)));
        }

        [Test]
        public void TryBuild_MissingConfiguration_FailsWithoutThrowing()
        {
            CharacterAttributeState attributes = Attributes();
            PreparedEquipmentLoadout loadout = default;

            bool built = TownCharacterStatisticsBuilder.TryBuild(
                attributes, loadout, Resolve, null,
                TownCharacterStatisticsBuilder.DefaultDefenseMitigationConstant,
                out _, out string failure);

            Assert.That(built, Is.False);
            Assert.That(failure, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryBuild_InvalidArmorDefinition_FailsExplicitly()
        {
            LootDefinition broken = CreateArmorLoot("Broken", LootCategory.Helmet, 0, 0, MaximumResourceType.None, 0);
            Register("broken", broken);
            var loadout = new PreparedEquipmentLoadout(default, default, helmet: new LootId("broken"));
            CharacterAttributeState attributes = Attributes();

            bool built = TownCharacterStatisticsBuilder.TryBuild(
                attributes, loadout, Resolve, _configuration,
                TownCharacterStatisticsBuilder.DefaultDefenseMitigationConstant,
                out _, out string failure);

            Assert.That(built, Is.False);
            Assert.That(failure, Is.Not.Null.And.Not.Empty);
        }

        [TestCase(0, 0f)]
        [TestCase(10, 10f)]
        [TestCase(25, 25f)]
        [TestCase(40, 30f)]
        public void TryBuild_Luck_ConvertsBasisPointsToCappedPercent(int luck, float expectedPercent)
        {
            TownCharacterStatisticsPresentation result = Build(Attributes(luck: luck), default);

            Assert.That(result.LootBonusPercent, Is.EqualTo(expectedPercent).Within(0.0001f));
        }

        [Test]
        public void TryBuild_Equipment_DoesNotChangeLootBonus()
        {
            Register("helm", CreateArmorLoot("Helm", LootCategory.Helmet, 10, 10, MaximumResourceType.Health, 50));
            var loadout = new PreparedEquipmentLoadout(default, default, helmet: new LootId("helm"));
            CharacterAttributeState attributes = Attributes(luck: 10);

            TownCharacterStatisticsPresentation without = Build(attributes, default);
            TownCharacterStatisticsPresentation with = Build(attributes, loadout);

            Assert.That(with.LootBonusPercent, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(with.LootBonusPercent, Is.EqualTo(without.LootBonusPercent));
        }

        [Test]
        public void TryBuild_WeaponWithScaling_UsesTheRuntimeDamageFormula()
        {
            Register("sword", CreateWeaponLoot(
                "Sword", 30f, DamageType.Physical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0.70f));
            var loadout = new PreparedEquipmentLoadout(new LootId("sword"), default);

            TownCharacterStatisticsPresentation result = Build(Attributes(strength: 20), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(1));
            TownWeaponStatistics weapon = result.Weapons[0];
            Assert.That(weapon.Slot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(weapon.LootId, Is.EqualTo(new LootId("sword")));
            Assert.That(weapon.DisplayName, Is.EqualTo("Sword"));
            Assert.That(weapon.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(weapon.BaseDamage, Is.EqualTo(30f));
            Assert.That(weapon.EffectiveDamage, Is.EqualTo(34f));
        }

        [Test]
        public void TryBuild_WeaponWithoutScaling_EffectiveDamageEqualsBase()
        {
            Register("dagger", CreateWeaponLoot(
                "Dagger", 12f, DamageType.Magical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0f));
            var loadout = new PreparedEquipmentLoadout(default, new LootId("dagger"));

            TownCharacterStatisticsPresentation result = Build(Attributes(strength: 25), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(1));
            Assert.That(result.Weapons[0].Slot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));
            Assert.That(result.Weapons[0].DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(result.Weapons[0].EffectiveDamage, Is.EqualTo(12f));
        }

        [Test]
        public void TryBuild_Shield_IsNotListedAsWeapon()
        {
            Register("sword", CreateWeaponLoot(
                "Sword", 30f, DamageType.Physical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0.70f));
            Register("shield", CreateShieldLoot("Shield"));
            var loadout = new PreparedEquipmentLoadout(
                new LootId("sword"), default, weaponSetAOffHand: new LootId("shield"));

            TownCharacterStatisticsPresentation result = Build(Attributes(strength: 20), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(1));
            Assert.That(result.Weapons[0].Slot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            TownEquipmentSlotEntry offHand = FindSlot(result, EquipmentSlot.WeaponSetAOffHand);
            Assert.That(offHand.IsEmpty, Is.False);
            Assert.That(offHand.DisplayName, Is.EqualTo("Shield"));
        }

        [Test]
        public void TryBuild_TwoHandedWeapon_AppearsOnce()
        {
            Register("greatsword", CreateWeaponLoot(
                "Greatsword", 40f, DamageType.Physical, WeaponHandedness.TwoHanded,
                CharacterAttribute.Strength, 0.70f));
            var loadout = new PreparedEquipmentLoadout(new LootId("greatsword"), default);

            TownCharacterStatisticsPresentation result = Build(Attributes(strength: 20), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(1));
            Assert.That(result.Weapons[0].Slot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(result.Weapons[0].EffectiveDamage, Is.EqualTo(45f));
        }

        [Test]
        public void TryBuild_TwoHandedWeapon_BlocksTheOffHandOfItsSet()
        {
            Register("greatsword", CreateWeaponLoot(
                "Greatsword", 50f, DamageType.Physical, WeaponHandedness.TwoHanded,
                CharacterAttribute.Strength, 0f));
            Register("dagger", CreateWeaponLoot(
                "Dagger", 12f, DamageType.Physical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0f));
            var loadout = new PreparedEquipmentLoadout(
                new LootId("greatsword"), default, weaponSetAOffHand: new LootId("dagger"));

            TownCharacterStatisticsPresentation result = Build(Attributes(), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(1));
            Assert.That(result.Weapons[0].Slot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
        }

        [Test]
        public void TryBuild_TwoWeaponSets_ListsEveryEquippedWeapon()
        {
            Register("sword", CreateWeaponLoot(
                "Sword", 30f, DamageType.Physical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0f));
            Register("bow", CreateWeaponLoot(
                "Bow", 25f, DamageType.Physical, WeaponHandedness.TwoHanded,
                CharacterAttribute.Dexterity, 0.5f));
            var loadout = new PreparedEquipmentLoadout(new LootId("sword"), new LootId("bow"));

            TownCharacterStatisticsPresentation result = Build(Attributes(dexterity: 20), loadout);

            Assert.That(result.Weapons.Count, Is.EqualTo(2));
            Assert.That(result.Weapons[0].Slot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(result.Weapons[1].Slot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));
            Assert.That(result.Weapons[1].EffectiveDamage, Is.EqualTo(27f));
        }

        [Test]
        public void TryBuild_UnknownLootId_DoesNotThrowAndContributesNothing()
        {
            var loadout = new PreparedEquipmentLoadout(
                new LootId("ghost_weapon"), default, helmet: new LootId("ghost_helm"));

            TownCharacterStatisticsPresentation result = Build(Attributes(vitality: 10), loadout);

            Assert.That(result.Weapons, Is.Empty);
            Assert.That(result.MaximumHealth.Total, Is.EqualTo(125));
            Assert.That(result.MaximumHealth.FromEquipment, Is.Zero);
            Assert.That(result.PhysicalDefense, Is.Zero);
            TownEquipmentSlotEntry weaponSlot = FindSlot(result, EquipmentSlot.WeaponSetAMainHand);
            Assert.That(weaponSlot.IsEmpty, Is.False);
            Assert.That(weaponSlot.LootId, Is.EqualTo(new LootId("ghost_weapon")));
            Assert.That(weaponSlot.DisplayName, Is.EqualTo("ghost_weapon"));
            Assert.That(weaponSlot.Icon, Is.Null);
            Assert.That(FindSlot(result, EquipmentSlot.Helmet).DisplayName, Is.EqualTo("ghost_helm"));
        }

        [Test]
        public void TryBuild_NullResolver_DoesNotThrowAndShowsLootIds()
        {
            var loadout = new PreparedEquipmentLoadout(new LootId("sword"), default);
            CharacterAttributeState attributes = Attributes(vitality: 10);

            bool built = TownCharacterStatisticsBuilder.TryBuild(
                attributes, loadout, null, _configuration,
                TownCharacterStatisticsBuilder.DefaultDefenseMitigationConstant,
                out TownCharacterStatisticsPresentation result, out string failure);

            Assert.That(built, Is.True, failure);
            Assert.That(result.Weapons, Is.Empty);
            Assert.That(result.MaximumHealth.Total, Is.EqualTo(125));
            Assert.That(FindSlot(result, EquipmentSlot.WeaponSetAMainHand).DisplayName, Is.EqualTo("sword"));
        }

        [Test]
        public void TryBuild_Slots_ListAllEightInCanonicalOrder()
        {
            Register("helm", CreateArmorLoot("Helm", LootCategory.Helmet, 5, 0, MaximumResourceType.Health, 1));
            var loadout = new PreparedEquipmentLoadout(default, default, helmet: new LootId("helm"));

            TownCharacterStatisticsPresentation result = Build(Attributes(), loadout);

            EquipmentSlot[] expected =
            {
                EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
                EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetAOffHand,
                EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.WeaponSetBOffHand
            };
            Assert.That(result.Slots.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(result.Slots[index].Slot, Is.EqualTo(expected[index]));
            }

            Assert.That(result.Slots[0].IsEmpty, Is.False);
            Assert.That(result.Slots[0].DisplayName, Is.EqualTo("Helm"));
            Assert.That(result.Slots[1].IsEmpty, Is.True);
            Assert.That(result.Slots[1].DisplayName, Is.Empty);
        }

        [Test]
        public void TryBuild_SameInputs_ProduceTheSameOutputs()
        {
            Register("helm", CreateArmorLoot("Helm", LootCategory.Helmet, 40, 5, MaximumResourceType.Health, 20));
            Register("sword", CreateWeaponLoot(
                "Sword", 30f, DamageType.Physical, WeaponHandedness.OneHanded,
                CharacterAttribute.Strength, 0.70f));
            var loadout = new PreparedEquipmentLoadout(
                new LootId("sword"), default, helmet: new LootId("helm"));
            CharacterAttributeState attributes = Attributes(vitality: 10, strength: 20, luck: 7);

            TownCharacterStatisticsPresentation first = Build(attributes, loadout);
            TownCharacterStatisticsPresentation second = Build(attributes, loadout);

            Assert.That(second.MaximumHealth, Is.EqualTo(first.MaximumHealth));
            Assert.That(second.MaximumStamina, Is.EqualTo(first.MaximumStamina));
            Assert.That(second.MaximumMana, Is.EqualTo(first.MaximumMana));
            Assert.That(second.PhysicalDefense, Is.EqualTo(first.PhysicalDefense));
            Assert.That(second.MagicalDefense, Is.EqualTo(first.MagicalDefense));
            Assert.That(second.PhysicalMitigationPercent, Is.EqualTo(first.PhysicalMitigationPercent));
            Assert.That(second.LootBonusPercent, Is.EqualTo(first.LootBonusPercent));
            Assert.That(second.Weapons.Count, Is.EqualTo(first.Weapons.Count));
            Assert.That(second.Weapons[0], Is.EqualTo(first.Weapons[0]));
            Assert.That(second.Slots.Count, Is.EqualTo(first.Slots.Count));
            for (int index = 0; index < first.Slots.Count; index++)
            {
                Assert.That(second.Slots[index], Is.EqualTo(first.Slots[index]));
            }
        }

        private TownCharacterStatisticsPresentation Build(
            CharacterAttributeState attributes,
            PreparedEquipmentLoadout loadout,
            float mitigationConstant = TownCharacterStatisticsBuilder.DefaultDefenseMitigationConstant)
        {
            bool built = TownCharacterStatisticsBuilder.TryBuild(
                attributes, loadout, Resolve, _configuration, mitigationConstant,
                out TownCharacterStatisticsPresentation presentation, out string failure);
            Assert.That(built, Is.True, failure);
            return presentation;
        }

        private LootDefinition Resolve(LootId lootId) =>
            _catalog.TryGetValue(lootId.Value, out LootDefinition definition) ? definition : null;

        private void Register(string id, LootDefinition definition) => _catalog[id] = definition;

        private static CharacterAttributeState Attributes(
            int vitality = 5,
            int resistance = 5,
            int strength = 5,
            int dexterity = 5,
            int intelligence = 5,
            int luck = 5)
        {
            bool created = CharacterAttributeState.TryCreate(
                vitality, resistance, strength, dexterity, intelligence, luck, 0,
                out CharacterAttributeState state);
            Assert.That(created, Is.True);
            return state;
        }

        private static TownEquipmentSlotEntry FindSlot(
            TownCharacterStatisticsPresentation presentation,
            EquipmentSlot slot)
        {
            for (int index = 0; index < presentation.Slots.Count; index++)
            {
                if (presentation.Slots[index].Slot == slot) return presentation.Slots[index];
            }

            Assert.Fail($"Slot {slot} is missing.");
            return default;
        }

        private LootDefinition CreateArmorLoot(
            string displayName,
            LootCategory category,
            int physicalDefense,
            int magicalDefense,
            MaximumResourceType resource,
            int amount)
        {
            ArmorDefinition armor = Create<ArmorDefinition>();
            SetPrivateField(armor, "_physicalDefense", physicalDefense);
            SetPrivateField(armor, "_magicalDefense", magicalDefense);
            SetPrivateField(armor, "_maximumResourceModifier", new MaximumResourceModifier(resource, amount));
            LootDefinition loot = CreateLoot(displayName, category);
            SetPrivateField(loot, "_armorDefinition", armor);
            return loot;
        }

        private LootDefinition CreateShieldLoot(string displayName)
        {
            ShieldDefinition shield = Create<ShieldDefinition>();
            LootDefinition loot = CreateLoot(displayName, LootCategory.Shield);
            SetPrivateField(loot, "_shieldDefinition", shield);
            return loot;
        }

        private LootDefinition CreateWeaponLoot(
            string displayName,
            float baseDamage,
            DamageType damageType,
            WeaponHandedness handedness,
            CharacterAttribute scalingAttribute,
            float coefficient)
        {
            WeaponDefinition weapon = Create<WeaponDefinition>();
            SetPrivateField(weapon, "_baseDamage", baseDamage);
            SetPrivateField(weapon, "_damageType", damageType);
            SetPrivateField(weapon, "_handedness", handedness);
            SetPrivateField(weapon, "_naturalScalingAttribute", scalingAttribute);
            SetPrivateField(
                weapon,
                "_offensiveScaling",
                new WeaponOffensiveScaling(scalingAttribute, coefficient));
            LootDefinition loot = CreateLoot(displayName, LootCategory.Weapon);
            SetPrivateField(loot, "_weaponDefinition", weapon);
            return loot;
        }

        private LootDefinition CreateLoot(string displayName, LootCategory category)
        {
            LootDefinition loot = Create<LootDefinition>();
            SetPrivateField(loot, "_displayName", displayName);
            SetPrivateField(loot, "_category", category);
            return loot;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif

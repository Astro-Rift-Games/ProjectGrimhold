#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Equipment
{
    /// <summary>
    /// Verifies the Equipment controller fails closed before Fusion spawns it: no query reports
    /// equipment and no intention mutates anything without authority.
    /// </summary>
    [TestFixture]
    public sealed class PlayerEquipmentControllerTests
    {
        private static readonly EquipmentSlot[] EverySlot =
            PlayerWeaponEquipmentNetworkController.AllSlots;

        private GameObject _host;
        private PlayerWeaponEquipmentNetworkController _controller;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject(nameof(PlayerEquipmentControllerTests));
            _controller = _host.AddComponent<PlayerWeaponEquipmentNetworkController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            EquipmentTestContent.Cleanup();
        }

        [Test]
        public void AllSlots_DescribesExactlyTheEightMvpSlots()
        {
            Assert.That(EverySlot, Is.EqualTo(new[]
            {
                EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.Helmet,
                EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
                EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand
            }));
        }

        [Test]
        public void UnspawnedController_ReportsEverySlotAsEmpty()
        {
            for (int index = 0; index < EverySlot.Length; index++)
            {
                EquipmentSlot slot = EverySlot[index];
                Assert.That(_controller.IsSlotOccupied(slot), Is.False, slot.ToString());
                Assert.That(_controller.TryGetSlotLoot(slot, out _), Is.False, slot.ToString());
                Assert.That(_controller.TryGetSlotDefinition(slot, out _), Is.False, slot.ToString());
            }

            Assert.That(_controller.HasAnyEquipment, Is.False);
            Assert.That(_controller.HasAnyWeapon, Is.False);
            Assert.That(_controller.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.None));
            Assert.That(_controller.ObservedEquipmentRevision, Is.Zero);
        }

        [Test]
        public void UnspawnedController_RejectsEveryIntentionWithoutAuthority()
        {
            LootDefinition helmet = EquipmentTestContent.CreateArmorDefinition("test_helmet", LootCategory.Helmet);
            LootDefinitionCatalog catalog = EquipmentTestContent.CreateCatalog(helmet);
            EquipmentTestContent.SetField(_controller, "_lootCatalog", catalog);

            Assert.That(_controller.CanEquip(helmet.LootId, EquipmentSlot.Helmet), Is.False);
            Assert.That(_controller.TryRequestEquip(helmet.LootId, EquipmentSlot.Helmet), Is.False);
            for (int index = 0; index < EverySlot.Length; index++)
            {
                Assert.That(_controller.TryRequestUnequip(EverySlot[index]), Is.False, EverySlot[index].ToString());
            }

            Assert.That(_controller.TryRequestUnequip(WeaponSetSlot.SetA), Is.False);
            Assert.That(_controller.HasAnyEquipment, Is.False);
        }

        [Test]
        public void UnequipIntention_RejectsSlotsThatAreNotEquipmentSlots()
        {
            Assert.That(_controller.TryRequestUnequip(EquipmentSlot.None), Is.False);
            Assert.That(_controller.TryRequestUnequip(WeaponSetSlot.None), Is.False);
        }

        [Test]
        public void SnapshotComparison_TreatsEverySlotAsEmptyBeforeSpawn()
        {
            Assert.That(
                _controller.TryMatchesExactEquipment(null, null, null, null, null, null, null, null, out string error),
                Is.True,
                error);

            Assert.That(
                _controller.TryMatchesExactEquipment(
                    new LootEntry(new LootId("test_helmet"), 1), null, null, null, null, null, null, null, out _),
                Is.False);
        }
    }
}
#endif

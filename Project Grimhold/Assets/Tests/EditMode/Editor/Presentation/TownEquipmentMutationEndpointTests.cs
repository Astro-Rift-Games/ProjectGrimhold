#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Presentation
{
    public sealed class TownEquipmentMutationEndpointTests
    {
        private static readonly ProfileId Profile = new("30303030303030303030303030303030");
        private LootDefinitionCatalog _catalog;
        private FakeLoadoutService _service;
        private bool _canMutate;
        private TownEquipmentMutationEndpoint _endpoint;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
            _service = new FakeLoadoutService();
            _canMutate = true;
            _endpoint = new TownEquipmentMutationEndpoint(
                _service,
                Profile,
                _catalog,
                () => _canMutate);
        }

        [Test]
        public void Equip_UsesDeterministicCurrentSlotsAndSupportsReplacement()
        {
            LootId sword = new("rapier");
            LootId recoverySword = new("arming_sword");
            _service.Loadout = new[] { new StashItem(sword, 1), new StashItem(recoverySword, 1) };

            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastAssignedSlot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(_endpoint.TryEquip(recoverySword, EquipmentSlot.WeaponSetBMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastAssignedSlot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));

            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                _service.LastAssignedSlot,
                Is.EqualTo(EquipmentSlot.WeaponSetAMainHand),
                "A third weapon deterministically replaces the first current weapon slot.");
        }

        [Test]
        public void ReadyGate_BlocksEquipAndExactUnequipWithoutCallingPersistence()
        {
            LootId sword = new("rapier");
            _service.Loadout = new[] { new StashItem(sword, 1) };
            _canMutate = false;

            Assert.That(_endpoint.CanEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.False);
            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(
                _endpoint.TryUnequip(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_service.AssignmentCalls, Is.Zero);
            Assert.That(_service.ClearCalls, Is.Zero);

            _canMutate = true;
            Assert.That(_endpoint.TryUnequip(EquipmentSlot.WeaponSetBMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastClearedSlot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));
        }

        private sealed class FakeLoadoutService : IPlayerLoadoutService
        {
            public IReadOnlyList<StashItem> Loadout { get; set; } = Array.Empty<StashItem>();
            public PreparedEquipmentLoadout PreparedEquipment { get; private set; }
            public EquipmentSlot LastAssignedSlot { get; private set; }
            public EquipmentSlot LastClearedSlot { get; private set; }
            public int AssignmentCalls { get; private set; }
            public int ClearCalls { get; private set; }

            public event Action<ProfileId> LoadoutChanged
            {
                add { }
                remove { }
            }

            public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId) => Loadout;
            public PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId) => PreparedEquipment;

            public StashOperationResult TryAssignPreparedEquipment(
                ProfileId profileId,
                EquipmentSlot slot,
                LootId lootId)
            {
                AssignmentCalls++;
                LastAssignedSlot = slot;
                PreparedEquipment = PreparedEquipment.With(slot, lootId);
                return StashOperationResult.Success;
            }

            public StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot)
            {
                ClearCalls++;
                LastClearedSlot = slot;
                PreparedEquipment = PreparedEquipment.Without(slot);
                return StashOperationResult.Success;
            }

            public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId) =>
                ExpeditionPreparationResult.ProfileUnavailable;
            public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferAllToLoadout(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferAllToStash(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryCreateLoadoutReservation(ProfileId profileId, string reservationId, out PendingLoadoutReservation reservation)
            {
                reservation = null;
                return StashOperationResult.InvalidInventory;
            }
            public StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId) => StashOperationResult.InvalidInventory;
        }
    }
}
#endif

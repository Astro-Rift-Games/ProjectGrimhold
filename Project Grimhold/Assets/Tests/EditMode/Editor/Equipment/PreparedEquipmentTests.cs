#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Equipment
{
    /// <summary>
    /// Covers Town preparation of the eight Equipment slots: compatibility, exclusive ownership,
    /// atomic equip/unequip and persistence.
    /// </summary>
    public sealed class PreparedEquipmentTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private static readonly LootId Sword = new("rapier");
        private static readonly LootId Greatsword = new("long_sword");
        private static readonly LootId RecoverySword = new("arming_sword");
        private static readonly LootId TrainingShield = new("shield");
        private static readonly LootId Helmet = new("placeholder_helmet");
        private static readonly LootId Boots = new("placeholder_boots");

        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void Loadout_RejectsEveryIncompatibleSlotAndAcceptsTheFixedOne()
        {
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Helmet, EquipmentSlot.Helmet, _catalog),
                Is.True);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Helmet, EquipmentSlot.Armor, _catalog),
                Is.False);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Helmet, EquipmentSlot.WeaponSetAMainHand, _catalog),
                Is.False);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Sword, EquipmentSlot.WeaponSetBMainHand, _catalog),
                Is.True);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Sword, EquipmentSlot.Boots, _catalog),
                Is.False);
        }

        [Test]
        public void Loadout_ValidatesEveryOccupiedSlot()
        {
            var loadout = new PreparedEquipmentLoadout(Sword, default, Helmet);

            Assert.That(
                PreparedEquipmentLoadout.TryValidate(loadout, _catalog, true, out string error),
                Is.True,
                error);
        }

        [Test]
        public void Loadout_AcceptsShieldOnlyInOffHandWithoutWeaponRequirements()
        {
            var loadout = new PreparedEquipmentLoadout(
                Sword,
                default,
                weaponSetAOffHand: TrainingShield);

            Assert.That(
                PreparedEquipmentLoadout.TryValidate(loadout, _catalog, true, out string error),
                Is.True,
                error);
            Assert.That(
                PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                    loadout,
                    ProgressionBalanceDefaults.InitialCharacterAttributeState,
                    _catalog,
                    out error),
                Is.True,
                error);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(
                    TrainingShield,
                    EquipmentSlot.WeaponSetAMainHand,
                    _catalog),
                Is.False);
        }

        [Test]
        public void Store_EquipsArmorByMovingOneUnitOutOfInventory()
        {
            LocalProfileStore store = CreateStore("40404040404040404040404040404040");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Helmet, Is.EqualTo(Helmet));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
        }

        [Test]
        public void Store_RejectsEquipWhenTheUnitOnlyExistsInStash()
        {
            LocalProfileStore store = CreateStore("41414141414141414141414141414141");
            Assert.That(
                store.TrySecureLoot(new[] { new StashItem(Boots, 3) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Boots, Boots),
                Is.EqualTo(StashOperationResult.InvalidInventory));

            Assert.That(store.GetPreparedEquipment().Boots.IsValid, Is.False);
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash()[0].Amount, Is.EqualTo(3));
        }

        [Test]
        public void Store_RejectsAnIncompatibleSlotAndKeepsOwnership()
        {
            LocalProfileStore store = CreateStore("42424242424242424242424242424242");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Armor, Helmet),
                Is.EqualTo(StashOperationResult.InvalidInventory));

            Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(
                store.GetLoadout(),
                Is.EqualTo(new[] { new StashItem(Helmet, 1) }),
                "A rejected assignment moves nothing.");
        }

        [Test]
        public void Store_RejectsUnmetWeaponRequirementsWithoutChangingOwnershipOrCommitting()
        {
            CharacterAttributeState attributes = CreateAttributes(strength: 5);
            LocalProfileStore store = CreateStore(
                "52525252525252525252525252525252",
                attributes,
                snapshot => snapshot.Loadout.Add(new StashItem(Greatsword, 1)));
            int commitCount = 0;
            store.ProfileCommitted += _ => commitCount++;

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.AttributeRequirementsNotMet));

            Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Greatsword, 1) }));
            Assert.That(commitCount, Is.Zero);
        }

        [Test]
        public void Store_AcceptsWeaponWhenConfirmedAttributesMeetItsRequirement()
        {
            LocalProfileStore store = CreateStore(
                "53535353535353535353535353535353",
                CreateAttributes(strength: 10),
                snapshot => snapshot.Loadout.Add(new StashItem(Greatsword, 1)));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetLoadout(), Is.Empty);
        }

        [Test]
        public void Store_EquippingTwoHandedWeaponDisplacesBothHandsInOnlyItsSet()
        {
            LocalProfileStore store = CreateStore(
                "53535353535353535353535353535354",
                CreateAttributes(strength: 10),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
                        Sword, Sword, default, default, default, default, RecoverySword, default);
                    snapshot.Loadout.Add(new StashItem(Greatsword, 1));
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.Success));

            PreparedEquipmentLoadout prepared = store.GetPreparedEquipment();
            Assert.That(prepared.WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(prepared.WeaponSetAOffHand.IsValid, Is.False);
            Assert.That(prepared.WeaponSetBMainHand, Is.EqualTo(Sword));
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(Sword, 1)));
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(RecoverySword, 1)));
        }

        [Test]
        public void Store_EquippingTwoHandedWeaponReturnsDisplacedShieldAtomically()
        {
            LocalProfileStore store = CreateStore(
                "53535353535353535353535353535357",
                CreateAttributes(strength: 10),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
                        Sword,
                        default,
                        weaponSetAOffHand: TrainingShield);
                    snapshot.Loadout.Add(new StashItem(Greatsword, 1));
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.Success));

            PreparedEquipmentLoadout prepared = store.GetPreparedEquipment();
            Assert.That(prepared.WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(prepared.WeaponSetAOffHand.IsValid, Is.False);
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(Sword, 1)));
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(TrainingShield, 1)));
        }

        [Test]
        public void Store_RejectsBlockedOffHandWithoutRemovingTwoHandedWeapon()
        {
            LocalProfileStore store = CreateStore(
                "53535353535353535353535353535355",
                CreateAttributes(strength: 10),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Greatsword, default);
                    snapshot.Loadout.Add(new StashItem(Sword, 1));
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAOffHand, Sword),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(store.GetPreparedEquipment().WeaponSetAOffHand.IsValid, Is.False);
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
        }

        [Test]
        public void Store_RejectsTwoHandedExchangeWhenFinalInventoryCannotHoldBothDisplacedUnits()
        {
            LocalProfileStore store = CreateStore(
                "53535353535353535353535353535356",
                CreateAttributes(strength: 10),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
                        Sword, default, default, default, default, default, RecoverySword, default);
                    snapshot.Loadout.Add(new StashItem(Greatsword, 1));
                    for (int index = 1; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
                    {
                        snapshot.Loadout.Add(new StashItem(new LootId($"two-hand-capacity-{index}"), 1));
                    }
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.PersistenceFailed));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(store.GetPreparedEquipment().WeaponSetAOffHand, Is.EqualTo(RecoverySword));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(Greatsword, 1)));
        }

        [Test]
        public void Store_RejectsUnmetWeaponAlreadyInLoadoutWithoutChangingAssignmentOrCommitting()
        {
            LocalProfileStore store = CreateStore(
                "56565656565656565656565656565656",
                CreateAttributes(strength: 5),
                snapshot => snapshot.Loadout.Add(new StashItem(Greatsword, 1)));
            int commitCount = 0;
            store.ProfileCommitted += _ => commitCount++;

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.AttributeRequirementsNotMet));

            Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Greatsword, 1) }));
            Assert.That(commitCount, Is.Zero);
        }

        [Test]
        public void PreparationAndReservation_RejectPersistedWeaponWithUnmetRequirementsAtomically()
        {
            LocalProfileStore store = CreateStore(
                "54545454545454545454545454545454",
                CreateAttributes(strength: 5),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Greatsword, default);
                });
            int commitCount = 0;
            store.ProfileCommitted += _ => commitCount++;

            Assert.That(
                store.TryPrepareExpeditionEquipment(),
                Is.EqualTo(ExpeditionPreparationResult.AttributeRequirementsNotMet));
            Assert.That(
                store.TryCreateLoadoutReservation("unmet-requirements", out PendingLoadoutReservation reservation),
                Is.EqualTo(StashOperationResult.AttributeRequirementsNotMet));

            Assert.That(reservation, Is.Null);
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(commitCount, Is.Zero);
        }

        [Test]
        public void Rollback_RejectsReservedWeaponWithUnmetRequirementsWithoutPartialRestore()
        {
            const string reservationId = "rollback-unmet-requirements";
            LocalProfileStore store = CreateStore(
                "55555555555555555555555555555555",
                CreateAttributes(strength: 5),
                snapshot => snapshot.PendingReservation = new PendingLoadoutReservation(
                    reservationId,
                    Array.Empty<StashItem>(),
                    new PreparedEquipmentLoadout(Greatsword, default)));
            int commitCount = 0;
            store.ProfileCommitted += _ => commitCount++;

            Assert.That(
                store.TryRollbackLoadoutReservation(reservationId),
                Is.EqualTo(StashOperationResult.AttributeRequirementsNotMet));

            Assert.That(store.PendingReservation, Is.Not.Null);
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(commitCount, Is.Zero);
        }

        [Test]
        public void Store_ReleasingASlotKeepsTheUnitInTheLoadout()
        {
            LocalProfileStore store = CreateStore("43434343434343434343434343434343");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryClearPreparedEquipment(EquipmentSlot.Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Helmet.IsValid, Is.False);
            Assert.That(store.GetLoadout()[0].LootId, Is.EqualTo(Helmet));
        }

        [Test]
        public void Store_EquipAndUnequipPreserveStackQuantityAcrossLocations()
        {
            LocalProfileStore store = CreateStore("43434343434343434343434343434340");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Sword, 2) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Sword, 1) }));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));

            Assert.That(
                store.TryClearPreparedEquipment(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Sword, 2) }));
            Assert.That(store.GetPreparedEquipment().HasAnyWeapon, Is.False);
        }

        [Test]
        public void Store_CannotTransferAnEquippedPieceThroughInventoryOperations()
        {
            LocalProfileStore store = CreateStore("44444444444444444444444444444444");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Sword, 1), new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.TryTransferToStash(Helmet, 1), Is.EqualTo(StashOperationResult.InvalidInventory));

            Assert.That(store.GetPreparedEquipment().Helmet, Is.EqualTo(Helmet));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Sword));
        }

        [Test]
        public void Store_FullInventoryAllowsOneForOneEquipmentSwapAgainstFinalCapacity()
        {
            LocalProfileStore store = CreateStore(
                "47474747474747474747474747474740",
                CreateAttributes(strength: 10),
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Sword, default);
                    snapshot.Loadout.Add(new StashItem(Greatsword, 1));
                    for (int index = 1; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
                    {
                        snapshot.Loadout.Add(new StashItem(new LootId($"capacity-{index}"), 1));
                    }
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Greatsword),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand, Is.EqualTo(Greatsword));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(store.GetLoadout(), Has.Some.EqualTo(new StashItem(Sword, 1)));
            Assert.That(store.GetLoadout(), Has.None.EqualTo(new StashItem(Greatsword, 1)));
        }

        [Test]
        public void Store_FullInventoryEquipIntoEmptySlotFreesOneInventorySlot()
        {
            LocalProfileStore store = CreateStore(
                "47474747474747474747474747474741",
                ProgressionBalanceDefaults.InitialCharacterAttributeState,
                snapshot =>
                {
                    snapshot.Loadout.Add(new StashItem(Helmet, 1));
                    for (int index = 1; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
                    {
                        snapshot.Loadout.Add(new StashItem(new LootId($"occupied-{index}"), 1));
                    }
                });

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Helmet, Is.EqualTo(Helmet));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots - 1));
        }

        [Test]
        public void Store_FullInventoryRejectsUnequipWithoutPartialMutation()
        {
            LocalProfileStore store = CreateStore(
                "48484848484848484848484848484840",
                ProgressionBalanceDefaults.InitialCharacterAttributeState,
                snapshot =>
                {
                    snapshot.PreparedEquipment = new PreparedEquipmentLoadout(default, default, Helmet);
                    for (int index = 0; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
                    {
                        snapshot.Loadout.Add(new StashItem(new LootId($"full-{index}"), 1));
                    }
                });

            Assert.That(
                store.TryClearPreparedEquipment(EquipmentSlot.Helmet),
                Is.EqualTo(StashOperationResult.PersistenceFailed));

            Assert.That(store.GetPreparedEquipment().Helmet, Is.EqualTo(Helmet));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
        }

        [Test]
        public void Store_ReservationCarriesEveryPreparedSlot()
        {
            LocalProfileStore store = CreateStore("45454545454545454545454545454545");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Sword, 1), new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryCreateLoadoutReservation("reservation-armor", out PendingLoadoutReservation reservation),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(reservation.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(reservation.PreparedEquipment.Helmet, Is.EqualTo(Helmet));
            Assert.That(reservation.Items, Is.Empty);

            Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
            Assert.That(
                RaidAdmissionData.TryCreate(
                    code,
                    new ProfileId("45454545454545454545454545454545"),
                    reservation,
                    ProgressionBalanceDefaults.InitialCharacterAttributeState,
                    ExperienceCurve.InitialLevel,
                    0,
                    0,
                    out RaidAdmissionData admission),
                Is.True);
            Assert.That(admission.HelmetEntryIndexPlusOne, Is.GreaterThan(0));
            Assert.That(
                admission.ReservedLoadout[admission.HelmetEntryIndexPlusOne - 1].LootId,
                Is.EqualTo(Helmet));

            Assert.That(RaidAdmissionDataCodec.TryEncode(admission, out byte[] token), Is.True);
            Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
            Assert.That(
                decoded.HelmetEntryIndexPlusOne,
                Is.EqualTo(admission.HelmetEntryIndexPlusOne));
            Assert.That(
                decoded.WeaponSetAMainHandEntryIndexPlusOne,
                Is.EqualTo(admission.WeaponSetAMainHandEntryIndexPlusOne));
        }

        [Test]
        public void Codec_RoundTripsEveryPreparedSlot()
        {
            var profile = new ProfileId("46464646464646464646464646464646");
            var snapshot = new LocalProfileSnapshot { ProfileId = profile };
            snapshot.Loadout.Add(new StashItem(Sword, 2));
            snapshot.Loadout.Add(new StashItem(Helmet, 2));
            snapshot.Loadout.Add(new StashItem(Boots, 2));
            snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
                Sword,
                default,
                Helmet,
                default,
                default,
                Boots);

            Assert.That(
                LocalProfileSaveCodec.TryDecode(
                    LocalProfileSaveCodec.Encode(snapshot),
                    profile,
                    _catalog,
                    out LocalProfileSnapshot restored,
                    out _,
                    out string error),
                Is.True,
                error);

            Assert.That(restored.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(restored.PreparedEquipment.Helmet, Is.EqualTo(Helmet));
            Assert.That(restored.PreparedEquipment.Boots, Is.EqualTo(Boots));
            Assert.That(restored.PreparedEquipment.Armor.IsValid, Is.False);
            Assert.That(restored.PreparedEquipment.Gloves.IsValid, Is.False);
            Assert.That(restored.Loadout, Is.EqualTo(snapshot.Loadout));
        }

        [Test]
        public void Codec_MigratesSchema3WeaponSlotsAndPendingReservationToWeaponSetMains()
        {
            var profile = new ProfileId("48484848484848484848484848484848");
            string json =
                $"{{\"schemaVersion\":3,\"profileId\":\"{profile.Value}\",\"level\":1," +
                "\"loadout\":[{\"lootId\":\"bone\",\"amount\":2}]," +
                "\"preparedWeaponSlot1\":\"rapier\"," +
                "\"preparedWeaponSlot2\":\"arming_sword\"," +
                "\"pendingReservation\":{\"reservationId\":\"legacy-reservation\"," +
                "\"items\":[{\"lootId\":\"bone\",\"amount\":1}]," +
                "\"preparedWeaponSlot2\":\"arming_sword\"}}";

            Assert.That(
                LocalProfileSaveCodec.TryDecode(
                    json,
                    profile,
                    _catalog,
                    out LocalProfileSnapshot restored,
                    out _,
                    out string error),
                Is.True,
                error);

            Assert.That(restored.SchemaVersion, Is.EqualTo(4));
            Assert.That(restored.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(restored.PreparedEquipment.WeaponSetBMainHand, Is.EqualTo(RecoverySword));
            Assert.That(restored.PreparedEquipment.WeaponSetAOffHand.IsValid, Is.False);
            Assert.That(restored.PreparedEquipment.WeaponSetBOffHand.IsValid, Is.False);
            Assert.That(restored.PendingReservation, Is.Not.Null);
            Assert.That(restored.PendingReservation.PreparedEquipment.WeaponSetAMainHand.IsValid, Is.False);
            Assert.That(restored.PendingReservation.PreparedEquipment.WeaponSetBMainHand, Is.EqualTo(RecoverySword));
            Assert.That(restored.PendingReservation.PreparedEquipment.WeaponSetAOffHand.IsValid, Is.False);
            Assert.That(restored.PendingReservation.PreparedEquipment.WeaponSetBOffHand.IsValid, Is.False);
            Assert.That(restored.Loadout, Is.EqualTo(new[] { new StashItem(new LootId("bone"), 2) }));
            Assert.That(restored.PendingReservation.Items, Is.EqualTo(new[] { new StashItem(new LootId("bone"), 1) }));
        }

        [Test]
        public void Codec_MigratesLegacyNonOwningEquipmentToExclusiveInventory()
        {
            var profile = new ProfileId("49494949494949494949494949494949");
            string json =
                $"{{\"schemaVersion\":2,\"profileId\":\"{profile.Value}\",\"level\":1," +
                "\"loadout\":[{\"lootId\":\"rapier\",\"amount\":2}," +
                "{\"lootId\":\"placeholder_helmet\",\"amount\":1}]," +
                "\"preparedWeaponSlot1\":\"rapier\"," +
                "\"preparedHelmet\":\"placeholder_helmet\"}";

            Assert.That(
                LocalProfileSaveCodec.TryDecode(
                    json,
                    profile,
                    _catalog,
                    out LocalProfileSnapshot restored,
                    out _,
                    out string error),
                Is.True,
                error);

            Assert.That(restored.SchemaVersion, Is.EqualTo(LocalProfileSnapshot.CurrentSchemaVersion));
            Assert.That(restored.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(restored.PreparedEquipment.Helmet, Is.EqualTo(Helmet));
            Assert.That(restored.Loadout, Is.EqualTo(new[] { new StashItem(Sword, 1) }));
        }

        [Test]
        public void Codec_KeepsLegacyExclusiveSaveWhenReferencesAreNotInInventory()
        {
            var profile = new ProfileId("50505050505050505050505050505050");
            string json =
                $"{{\"schemaVersion\":2,\"profileId\":\"{profile.Value}\",\"level\":1," +
                "\"loadout\":[{\"lootId\":\"bone\",\"amount\":2}]," +
                "\"preparedWeaponSlot1\":\"rapier\"}";

            Assert.That(
                LocalProfileSaveCodec.TryDecode(
                    json,
                    profile,
                    _catalog,
                    out LocalProfileSnapshot restored,
                    out _,
                    out string error),
                Is.True,
                error);

            Assert.That(restored.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(Sword));
            Assert.That(
                restored.Loadout,
                Is.EqualTo(new[] { new StashItem(new LootId("bone"), 2) }));
        }

        private LocalProfileStore CreateStore(string profileValue)
        {
            return CreateStore(
                profileValue,
                ProgressionBalanceDefaults.InitialCharacterAttributeState,
                null);
        }

        private LocalProfileStore CreateStore(
            string profileValue,
            in CharacterAttributeState attributes,
            Action<LocalProfileSnapshot> configure)
        {
            var profile = new ProfileId(profileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            snapshot.CharacterAttributes = attributes;
            configure?.Invoke(snapshot);
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            return new LocalProfileStore(repository, profile, _catalog);
        }

        private static CharacterAttributeState CreateAttributes(int strength)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    5, 5, strength, 5, 5, 5, 0, out CharacterAttributeState attributes),
                Is.True);
            return attributes;
        }
    }
}
#endif

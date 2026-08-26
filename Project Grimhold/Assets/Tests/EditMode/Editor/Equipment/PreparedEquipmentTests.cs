#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Equipment
{
    /// <summary>
    /// Covers Town preparation of the six Equipment slots: slot compatibility, ownership, equipping
    /// from either the Loadout or the Stash, releasing a slot and reconciliation after a transfer.
    /// </summary>
    public sealed class PreparedEquipmentTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private static readonly LootId Sword = new("training_sword");
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
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Helmet, EquipmentSlot.WeaponSlot1, _catalog),
                Is.False);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Sword, EquipmentSlot.WeaponSlot2, _catalog),
                Is.True);
            Assert.That(
                PreparedEquipmentLoadout.IsUsableEquipmentDefinition(Sword, EquipmentSlot.Boots, _catalog),
                Is.False);
        }

        [Test]
        public void Loadout_ValidatesOwnershipOfEveryOccupiedSlot()
        {
            var loadout = new PreparedEquipmentLoadout(Sword, default, Helmet);
            var owned = new[] { new StashItem(Sword, 1) };

            Assert.That(
                PreparedEquipmentLoadout.TryValidate(loadout, owned, _catalog, false, out string error),
                Is.False,
                "A helmet that is not owned cannot stay prepared.");
            Assert.That(error, Does.Contain("placeholder_helmet"));

            var complete = new[] { new StashItem(Sword, 1), new StashItem(Helmet, 1) };
            Assert.That(
                PreparedEquipmentLoadout.TryValidate(loadout, complete, _catalog, true, out error),
                Is.True,
                error);
        }

        [Test]
        public void Store_EquipsArmorFromTheLoadoutWithoutMovingUnits()
        {
            LocalProfileStore store = CreateStore("40404040404040404040404040404040");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Helmet, Is.EqualTo(Helmet));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(1));
            Assert.That(store.GetStash(), Is.Empty);
        }

        /// <summary>
        /// Equipping a unit that still lives in the Stash pulls exactly one unit into the Loadout,
        /// because the Loadout is what the raid reservation transfers.
        /// </summary>
        [Test]
        public void Store_EquipsFromTheStashByPullingOneUnitIntoTheLoadout()
        {
            LocalProfileStore store = CreateStore("41414141414141414141414141414141");
            Assert.That(
                store.TrySecureLoot(new[] { new StashItem(Boots, 3) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Boots, Boots),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Boots, Is.EqualTo(Boots));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(1));
            Assert.That(store.GetLoadout()[0].Amount, Is.EqualTo(1));
            Assert.That(store.GetStash()[0].Amount, Is.EqualTo(2));
        }

        [Test]
        public void Store_RejectsAnIncompatibleSlotAndKeepsOwnership()
        {
            LocalProfileStore store = CreateStore("42424242424242424242424242424242");
            Assert.That(
                store.TrySecureLoot(new[] { new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Armor, Helmet),
                Is.EqualTo(StashOperationResult.InvalidInventory));

            Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(store.GetStash()[0].Amount, Is.EqualTo(1), "A rejected assignment moves nothing.");
            Assert.That(store.GetLoadout(), Is.Empty);
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
        public void Store_MovingAnEquippedPieceBackToTheStashReleasesItsSlot()
        {
            LocalProfileStore store = CreateStore("44444444444444444444444444444444");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Sword, 1), new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSlot1, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.TryTransferToStash(Helmet, 1), Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.GetPreparedEquipment().Helmet.IsValid, Is.False);
            Assert.That(
                store.GetPreparedEquipment().WeaponSlot1,
                Is.EqualTo(Sword),
                "Reconciliation only releases the slots whose units left the Loadout.");
        }

        [Test]
        public void Store_ReservationCarriesEveryPreparedSlot()
        {
            LocalProfileStore store = CreateStore("45454545454545454545454545454545");
            Assert.That(
                store.TryImportItems(new[] { new StashItem(Sword, 1), new StashItem(Helmet, 1) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSlot1, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                store.TryAssignPreparedEquipment(EquipmentSlot.Helmet, Helmet),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryCreateLoadoutReservation("reservation-armor", out PendingLoadoutReservation reservation),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(reservation.PreparedEquipment.WeaponSlot1, Is.EqualTo(Sword));
            Assert.That(reservation.PreparedEquipment.Helmet, Is.EqualTo(Helmet));

            Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
            Assert.That(
                RaidAdmissionData.TryCreate(
                    code,
                    new ProfileId("45454545454545454545454545454545"),
                    reservation,
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
                decoded.WeaponSlot1EntryIndexPlusOne,
                Is.EqualTo(admission.WeaponSlot1EntryIndexPlusOne));
        }

        [Test]
        public void Codec_RoundTripsEveryPreparedSlot()
        {
            var profile = new ProfileId("46464646464646464646464646464646");
            var snapshot = new LocalProfileSnapshot { ProfileId = profile };
            snapshot.Loadout.Add(new StashItem(Sword, 1));
            snapshot.Loadout.Add(new StashItem(Helmet, 1));
            snapshot.Loadout.Add(new StashItem(Boots, 1));
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

            Assert.That(restored.PreparedEquipment.WeaponSlot1, Is.EqualTo(Sword));
            Assert.That(restored.PreparedEquipment.Helmet, Is.EqualTo(Helmet));
            Assert.That(restored.PreparedEquipment.Boots, Is.EqualTo(Boots));
            Assert.That(restored.PreparedEquipment.Armor.IsValid, Is.False);
            Assert.That(restored.PreparedEquipment.Gloves.IsValid, Is.False);
        }

        private LocalProfileStore CreateStore(string profileValue)
        {
            var profile = new ProfileId(profileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, _catalog), Is.True);
            return new LocalProfileStore(repository, profile, _catalog);
        }
    }
}
#endif

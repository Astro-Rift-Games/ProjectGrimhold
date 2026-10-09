#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sandbox
{
    public sealed class SandboxTestProfileTests
    {
        private const string ConfigurationPath = "Assets/Resources/LocalProfilePersistenceConfiguration.asset";

        private AbilityDefinition[] _definitions;
        private AbilityDefinitionCatalog _abilityCatalog;
        private LocalProfilePersistenceConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = AssetDatabase.LoadAssetAtPath<LocalProfilePersistenceConfiguration>(ConfigurationPath);
            Assert.That(_configuration, Is.Not.Null);

            _definitions = new[]
            {
                AbilityTestFactory.CreateDefinition(
                    "charge",
                    requirements: new[] { new CharacterAttributeRequirement(CharacterAttribute.Strength, 10) }),
                AbilityTestFactory.CreateDefinition(
                    "seismic",
                    requirements: new[] { new CharacterAttributeRequirement(CharacterAttribute.Strength, 15) }),
                AbilityTestFactory.CreateDefinition(
                    "heavy",
                    requirements: new[] { new CharacterAttributeRequirement(CharacterAttribute.Intelligence, 30) })
            };
            _abilityCatalog = AbilityTestFactory.CreateCatalog(_definitions);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_abilityCatalog);
            foreach (AbilityDefinition definition in _definitions)
            {
                Object.DestroyImmediate(definition);
            }
        }

        private LocalProfileSnapshot CreateSnapshot()
        {
            bool created = SandboxTestProfile.TryCreateSnapshot(
                _abilityCatalog, _configuration.RecoveryWeaponLootId, out LocalProfileSnapshot snapshot, out string error);
            Assert.That(created, Is.True, error);
            return snapshot;
        }

        [Test]
        public void Identity_IsFixedAndNotARealOrDevelopmentProfile()
        {
            LocalProfileSnapshot snapshot = CreateSnapshot();

            Assert.That(snapshot.ProfileId.Value, Is.EqualTo("sandbox-test-player"));
            Assert.That(SandboxTestProfile.Id, Is.EqualTo(snapshot.ProfileId));
            Assert.That(SandboxTestProfile.IsSandboxProfile(snapshot.ProfileId), Is.True);
            Assert.That(SandboxTestProfile.IsSandboxProfile(new ProfileId("dev-local-host")), Is.False);
            Assert.That(DevelopmentProfileIdentity.IsDevelopmentProfile(snapshot.ProfileId), Is.False);
        }

        [Test]
        public void Attributes_AreAllSetToTheTestValueWithNoPendingPoints()
        {
            CharacterAttributeState attributes = CreateSnapshot().CharacterAttributes;

            Assert.That(SandboxTestProfile.AttributeValue, Is.EqualTo(30));
            Assert.That(attributes.Vitality, Is.EqualTo(30));
            Assert.That(attributes.Resistance, Is.EqualTo(30));
            Assert.That(attributes.Strength, Is.EqualTo(30));
            Assert.That(attributes.Dexterity, Is.EqualTo(30));
            Assert.That(attributes.Intelligence, Is.EqualTo(30));
            Assert.That(attributes.Luck, Is.EqualTo(30));
            Assert.That(attributes.AvailablePoints, Is.Zero);
        }

        [Test]
        public void Abilities_EveryCatalogAbilityIsUnlockedAndNothingIsPrepared()
        {
            LocalProfileSnapshot snapshot = CreateSnapshot();

            Assert.That(snapshot.UnlockedAbilities.Count, Is.EqualTo(3));
            foreach (AbilityDefinition definition in _definitions)
            {
                Assert.That(snapshot.UnlockedAbilities, Does.Contain(definition.AbilityId));
            }

            Assert.That(snapshot.PreparedAbilities.Get(UniversalAbilitySlot.Slot1).IsValid, Is.False);
            Assert.That(snapshot.PreparedAbilities.Get(UniversalAbilitySlot.Slot2).IsValid, Is.False);
        }

        [Test]
        public void Inventory_IsEmptyWithNoReservationsReceiptsOrMissions()
        {
            LocalProfileSnapshot snapshot = CreateSnapshot();

            Assert.That(snapshot.Stash, Is.Empty);
            Assert.That(snapshot.Loadout, Is.Empty);
            Assert.That(snapshot.Currency, Is.Zero);
            Assert.That(snapshot.PendingReservation, Is.Null);
            Assert.That(snapshot.PendingExtractionCommit, Is.Null);
            Assert.That(snapshot.AppliedExtractionReceipts, Is.Empty);
            Assert.That(snapshot.ActiveMissions, Is.Empty);
            Assert.That(snapshot.RemoteRevision, Is.Zero);
        }

        [Test]
        public void Equipment_PreparesTheConfiguredRecoveryWeaponInSetAMainHand()
        {
            LocalProfileSnapshot snapshot = CreateSnapshot();

            Assert.That(_configuration.RecoveryWeaponLootId.IsValid, Is.True);
            Assert.That(
                snapshot.PreparedEquipment.Get(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(_configuration.RecoveryWeaponLootId));
        }

        [Test]
        public void Creation_IsDeterministic()
        {
            LocalProfileSnapshot first = CreateSnapshot();
            LocalProfileSnapshot second = CreateSnapshot();

            Assert.That(second.ProfileId, Is.EqualTo(first.ProfileId));
            Assert.That(second.CharacterAttributes, Is.EqualTo(first.CharacterAttributes));
            Assert.That(second.UnlockedAbilities, Is.EqualTo(first.UnlockedAbilities));
            Assert.That(second.PreparedEquipment.Get(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(first.PreparedEquipment.Get(EquipmentSlot.WeaponSetAMainHand)));
        }

        [Test]
        public void Creation_RejectsMissingCatalogEmptyCatalogAndMissingWeapon()
        {
            LootId weapon = _configuration.RecoveryWeaponLootId;
            AbilityDefinitionCatalog empty = AbilityTestFactory.CreateCatalog();
            try
            {
                Assert.That(SandboxTestProfile.TryCreateSnapshot(null, weapon, out _, out string e1), Is.False);
                Assert.That(e1, Is.Not.Empty);
                Assert.That(SandboxTestProfile.TryCreateSnapshot(empty, weapon, out _, out string e2), Is.False);
                Assert.That(e2, Is.Not.Empty);
                Assert.That(SandboxTestProfile.TryCreateSnapshot(_abilityCatalog, default, out _, out string e3), Is.False);
                Assert.That(e3, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void Store_IsBackedByTheInMemoryRepositoryAndPassesRaidAdmissionChecks()
        {
            bool created = SandboxTestProfile.TryCreateStore(
                _abilityCatalog, _configuration, out LocalProfileStore store, out string error);
            Assert.That(created, Is.True, error);

            Assert.That(store.ProfileId, Is.EqualTo(SandboxTestProfile.Id));
            Assert.That(store.IsAvailable, Is.True);
            Assert.That(store.GetUnlockedAbilities().Count, Is.EqualTo(3));
            Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
            Assert.That(
                store.TryGetRaidAdmissionAbilitySnapshot(out CharacterAttributeState attributes, out _, out string admissionError),
                Is.True,
                admissionError);
            Assert.That(attributes.Strength, Is.EqualTo(30));
            Assert.That(
                store.TryCreateLoadoutReservation("sandbox-test-reservation", out PendingLoadoutReservation reservation),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(reservation, Is.Not.Null);

            object repository = typeof(LocalProfileStore)
                .GetField("_repository", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(store);
            Assert.That(repository, Is.TypeOf<InMemoryLocalProfileRepository>());
        }

        [Test]
        public void Store_CanPrepareEveryUnlockedAbilityWithoutAttributeFailures()
        {
            Assert.That(SandboxTestProfile.TryCreateStore(_abilityCatalog, _configuration, out LocalProfileStore store, out _), Is.True);

            Assert.That(
                store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, _definitions[1].AbilityId),
                Is.EqualTo(AbilityPreparationResult.Success));
            Assert.That(
                store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, _definitions[2].AbilityId),
                Is.EqualTo(AbilityPreparationResult.Success));
        }

        [Test]
        public void Store_NeverWritesAProfileFileToDisk()
        {
            string expected = Path.Combine(Application.persistentDataPath, "grimhold-profile-sandbox-test-player.json");
            Assert.That(File.Exists(expected), Is.False, "Precondition: no stale sandbox profile file.");

            Assert.That(SandboxTestProfile.TryCreateStore(_abilityCatalog, _configuration, out LocalProfileStore store, out _), Is.True);
            Assert.That(store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, _definitions[0].AbilityId),
                Is.EqualTo(AbilityPreparationResult.Success));
            Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));

            Assert.That(File.Exists(expected), Is.False);
            Assert.That(File.Exists(expected + ".bak"), Is.False);
            Assert.That(File.Exists(expected + ".tmp"), Is.False);
        }
    }
}
#endif

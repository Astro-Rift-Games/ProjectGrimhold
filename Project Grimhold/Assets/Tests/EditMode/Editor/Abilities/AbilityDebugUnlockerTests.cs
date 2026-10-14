#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityDebugUnlockerTests
    {
        private static readonly ProfileId Profile = new("95959595959595959595959595959595");

        private LootDefinitionCatalog _lootCatalog;
        private AbilityDefinition[] _definitions;
        private AbilityDefinitionCatalog _abilityCatalog;
        private LocalProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_lootCatalog, Is.Not.Null);

            _definitions = new[]
            {
                AbilityTestFactory.CreateDefinition("charge"),
                AbilityTestFactory.CreateDefinition("trap"),
                AbilityTestFactory.CreateDefinition("arcane_projectile")
            };
            _abilityCatalog = AbilityTestFactory.CreateCatalog(_definitions);

            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
            _store = new LocalProfileStore(
                repository,
                Profile,
                lootCatalog: _lootCatalog,
                abilityCatalog: _abilityCatalog);
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

        [Test]
        public void UnlockAll_UnlocksEveryCatalogAbilityAndReturnsHowManyWereNew()
        {
            int unlocked = AbilityDebugUnlocker.UnlockAll(_store, _abilityCatalog);

            Assert.That(unlocked, Is.EqualTo(3));
            Assert.That(_store.GetUnlockedAbilities().Count, Is.EqualTo(3));
            Assert.That(_store.IsAbilityUnlocked(new AbilityId("trap")), Is.True);
        }

        [Test]
        public void UnlockAll_SkipsAbilitiesThatAreAlreadyUnlocked()
        {
            Assert.That(_store.TryUnlockAbility(new AbilityId("charge")), Is.EqualTo(AbilityUnlockResult.Success));

            Assert.That(AbilityDebugUnlocker.UnlockAll(_store, _abilityCatalog), Is.EqualTo(2));
            Assert.That(AbilityDebugUnlocker.UnlockAll(_store, _abilityCatalog), Is.Zero);
        }

        [Test]
        public void Unlock_ReportsTheStoreResult()
        {
            Assert.That(AbilityDebugUnlocker.Unlock(_store, _definitions[0]), Is.EqualTo(AbilityUnlockResult.Success));
            Assert.That(AbilityDebugUnlocker.Unlock(_store, _definitions[0]), Is.EqualTo(AbilityUnlockResult.AlreadyUnlocked));
        }

        [Test]
        public void MissingStoreCatalogOrDefinition_IsReportedWithoutThrowing()
        {
            Assert.That(AbilityDebugUnlocker.UnlockAll(null, _abilityCatalog), Is.Zero);
            Assert.That(AbilityDebugUnlocker.UnlockAll(_store, null), Is.Zero);
            Assert.That(AbilityDebugUnlocker.Unlock(null, _definitions[0]), Is.EqualTo(AbilityUnlockResult.ProfileUnavailable));
            Assert.That(AbilityDebugUnlocker.Unlock(_store, null), Is.EqualTo(AbilityUnlockResult.InvalidAbility));
        }

        [Test, Category("TASK206")]
        public void RelockAll_ClearsRepertoireAndPreparedSlotsInOneCommit()
        {
            Assert.That(UnlockAllAndPrepare(), Is.True);
            long currency = _store.GetCurrency();
            int stashCount = _store.GetStash().Count;
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            int removed = AbilityDebugUnlocker.RelockAll(_store);

            Assert.That(removed, Is.EqualTo(3));
            Assert.That(_store.GetUnlockedAbilities(), Is.Empty);
            Assert.That(_store.GetPreparedAbilities().Slot1.IsValid, Is.False);
            Assert.That(_store.GetPreparedAbilities().Slot2.IsValid, Is.False);
            Assert.That(_store.GetCurrency(), Is.EqualTo(currency));
            Assert.That(_store.GetStash().Count, Is.EqualTo(stashCount));
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test, Category("TASK206")]
        public void RelockAll_WithEmptyRepertoire_ReturnsZeroWithoutCommitting()
        {
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            Assert.That(AbilityDebugUnlocker.RelockAll(_store), Is.Zero);
            Assert.That(commits, Is.Zero);
        }

        [Test, Category("TASK206")]
        public void RelockAll_WithMissingOrUnavailableStore_ReturnsZeroWithoutCommitting()
        {
            Assert.That(AbilityDebugUnlocker.RelockAll(null), Is.Zero);

            var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
            snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
            var repository = new UnavailableRepository(snapshot);
            var store = new LocalProfileStore(repository, Profile, abilityCatalog: _abilityCatalog);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(AbilityDebugUnlocker.RelockAll(store), Is.Zero);
            Assert.That(repository.Snapshot.UnlockedAbilities.Count, Is.EqualTo(1));
            Assert.That(commits, Is.Zero);
        }

        private bool UnlockAllAndPrepare()
        {
            _store.TryCreditCurrency(125L);
            AbilityDebugUnlocker.UnlockAll(_store, _abilityCatalog);
            _store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));
            return _store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge"))
                       == AbilityPreparationResult.Success
                   && _store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("trap"))
                       == AbilityPreparationResult.Success;
        }

        private sealed class UnavailableRepository : ILocalProfileRepository
        {
            public UnavailableRepository(LocalProfileSnapshot snapshot) => Snapshot = snapshot;

            public LocalProfilePersistenceStatus Status => LocalProfilePersistenceStatus.Unavailable;
            public string LastError => "Unavailable.";
            public LocalProfileSnapshot Snapshot { get; }

            public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) => false;

            public bool TrySave(LocalProfileSnapshot snapshot, out string error)
            {
                error = LastError;
                return false;
            }
        }
    }
}
#endif

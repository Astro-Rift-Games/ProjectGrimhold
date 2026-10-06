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
    }
}
#endif

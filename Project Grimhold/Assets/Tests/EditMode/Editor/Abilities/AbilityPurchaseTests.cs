#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[Category("TASK206")]
public sealed class AbilityPurchaseTests
{
    private const long Price = 100L;

    private static readonly ProfileId Profile = new("94949494949494949494949494949494");

    private LootDefinitionCatalog _lootCatalog;
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;
    private AbilityDefinitionCatalog _abilityCatalog;

    [SetUp]
    public void SetUp()
    {
        _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_lootCatalog, Is.Not.Null);

        _charge = AbilityTestFactory.CreateDefinition(
            "charge",
            requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        _trap = AbilityTestFactory.CreateDefinition("trap");
        _abilityCatalog = AbilityTestFactory.CreateCatalog(_charge, _trap);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_abilityCatalog);
        Object.DestroyImmediate(_charge);
        Object.DestroyImmediate(_trap);
    }

    [Test]
    public void TryPurchaseAbility_WithEnoughCurrency_DebitsPriceAndUnlocksOnce()
    {
        LocalProfileStore store = CreateStore(out _, currency: 250L);
        AbilityId abilityId = new("trap");
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityPurchaseResult result = store.TryPurchaseAbility(abilityId, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(250L - Price));
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { abilityId }));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void TryPurchaseAbility_AlreadyUnlocked_IsRejectedWithoutDebit()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository, currency: 250L);
        AbilityId abilityId = new("trap");
        Assert.That(store.TryUnlockAbility(abilityId), Is.EqualTo(AbilityUnlockResult.Success));
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityPurchaseResult result = store.TryPurchaseAbility(abilityId, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.AlreadyUnlocked));
        Assert.That(store.GetCurrency(), Is.EqualTo(250L));
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { abilityId }));
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void TryPurchaseAbility_InsufficientCurrency_IsRejectedWithoutChanges()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository, currency: Price - 1L);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityPurchaseResult result = store.TryPurchaseAbility(new AbilityId("trap"), Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.InsufficientCurrency));
        Assert.That(store.GetCurrency(), Is.EqualTo(Price - 1L));
        Assert.That(store.GetUnlockedAbilities(), Is.Empty);
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void TryPurchaseAbility_ExactCurrency_Succeeds()
    {
        LocalProfileStore store = CreateStore(out _, currency: Price);
        AbilityId abilityId = new("trap");

        AbilityPurchaseResult result = store.TryPurchaseAbility(abilityId, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.Success));
        Assert.That(store.GetCurrency(), Is.Zero);
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { abilityId }));
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    [TestCase(long.MinValue)]
    public void TryPurchaseAbility_NonPositivePrice_IsRejected(long price)
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository, currency: 250L);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityPurchaseResult result = store.TryPurchaseAbility(new AbilityId("trap"), price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.InvalidPrice));
        Assert.That(store.GetCurrency(), Is.EqualTo(250L));
        Assert.That(store.GetUnlockedAbilities(), Is.Empty);
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void TryPurchaseAbility_InvalidOrUnknownAbility_IsRejected()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository, currency: 250L);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(
            store.TryPurchaseAbility(default, Price),
            Is.EqualTo(AbilityPurchaseResult.InvalidAbility));
        Assert.That(
            store.TryPurchaseAbility(new AbilityId("missing"), Price),
            Is.EqualTo(AbilityPurchaseResult.UnknownAbility));

        Assert.That(store.GetCurrency(), Is.EqualTo(250L));
        Assert.That(store.GetUnlockedAbilities(), Is.Empty);
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void TryPurchaseAbility_IgnoresAttributeRequirements()
    {
        LocalProfileStore store = CreateStore(out _, currency: 250L);
        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 0));
        AbilityId abilityId = new("charge");

        AbilityPurchaseResult result = store.TryPurchaseAbility(abilityId, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.Success));
        Assert.That(store.GetCurrency(), Is.EqualTo(250L - Price));
        Assert.That(store.IsAbilityUnlocked(abilityId), Is.True);
    }

    [Test]
    public void TryPurchaseAbility_WhenSaveFails_PreservesConfirmedSnapshot()
    {
        var repository = new RejectingRepository(
            new LocalProfileSnapshot { ProfileId = Profile, Currency = 250L },
            LocalProfilePersistenceStatus.Ready);
        var store = new LocalProfileStore(repository, Profile, abilityCatalog: _abilityCatalog);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileStore\\] Commit failed\\."));

        AbilityPurchaseResult result = store.TryPurchaseAbility(new AbilityId("trap"), Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.PersistenceFailed));
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(repository.Snapshot.Currency, Is.EqualTo(250L));
        Assert.That(repository.Snapshot.UnlockedAbilities, Is.Empty);
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void TryPurchaseAbility_ProfileUnavailable_IsRejected()
    {
        var repository = new RejectingRepository(
            new LocalProfileSnapshot { ProfileId = Profile, Currency = 250L },
            LocalProfilePersistenceStatus.Unavailable);
        var store = new LocalProfileStore(repository, Profile, abilityCatalog: _abilityCatalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityPurchaseResult result = store.TryPurchaseAbility(new AbilityId("trap"), Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.ProfileUnavailable));
        Assert.That(repository.Snapshot.Currency, Is.EqualTo(250L));
        Assert.That(repository.Snapshot.UnlockedAbilities, Is.Empty);
        Assert.That(commits, Is.Zero);
    }

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository, long currency)
    {
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
        var store = new LocalProfileStore(
            repository,
            Profile,
            lootCatalog: _lootCatalog,
            abilityCatalog: _abilityCatalog);
        Assert.That(store.TryCreditCurrency(currency), Is.EqualTo(StashOperationResult.Success));
        return store;
    }

    private sealed class RejectingRepository : ILocalProfileRepository
    {
        public RejectingRepository(LocalProfileSnapshot snapshot, LocalProfilePersistenceStatus status)
        {
            Snapshot = snapshot;
            Status = status;
        }

        public LocalProfilePersistenceStatus Status { get; }
        public string LastError => "Simulated persistence failure.";
        public LocalProfileSnapshot Snapshot { get; }

        public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) => false;

        public bool TrySave(LocalProfileSnapshot snapshot, out string error)
        {
            error = LastError;
            return false;
        }
    }
}
#endif

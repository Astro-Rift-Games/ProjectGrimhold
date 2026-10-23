#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public sealed class RecoveryWeaponConsentTests
{
    private LootDefinitionCatalog _catalog;
    private static readonly LootId Weapon = new("rapier");

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
    }

    [TestCase(RecoveryWeaponPreparation.Source.Prepared)]
    [TestCase(RecoveryWeaponPreparation.Source.Loadout)]
    [TestCase(RecoveryWeaponPreparation.Source.Stash)]
    [TestCase(RecoveryWeaponPreparation.Source.NewGrant)]
    public void Inspect_DistinguishesOwnershipWithoutCommitting(RecoveryWeaponPreparation.Source source)
    {
        LocalProfileStore store = Create(source, out _);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(store.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation),
            Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(preparation.WeaponSource, Is.EqualTo(source));
        Assert.That(preparation.RequiresConsent, Is.EqualTo(source == RecoveryWeaponPreparation.Source.NewGrant));
        Assert.That(commits, Is.Zero);
        Assert.That(Count(store), Is.EqualTo(source == RecoveryWeaponPreparation.Source.NewGrant ? 0 : 1));
    }

    [Test]
    public void Cancel_DiscardsInspectionWithoutGrantOrEquipmentChange()
    {
        LocalProfileStore store = Create(RecoveryWeaponPreparation.Source.NewGrant, out _);
        store.InspectRecoveryWeaponPreparation(out _);
        Assert.That(Count(store), Is.Zero);
        Assert.That(store.GetPreparedEquipment().HasAnyMainHand, Is.False);
        Assert.That(store.PendingReservation, Is.Null);
    }

    [TestCase(RecoveryWeaponPreparation.Source.Loadout)]
    [TestCase(RecoveryWeaponPreparation.Source.Stash)]
    [TestCase(RecoveryWeaponPreparation.Source.NewGrant)]
    public void Accept_CommitsExactlyOneUnit_AndRetryCannotDuplicate(RecoveryWeaponPreparation.Source source)
    {
        LocalProfileStore store = Create(source, out _);
        store.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(store.TryPrepareExpeditionEquipment(preparation), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.TryPrepareExpeditionEquipment(preparation), Is.EqualTo(ExpeditionPreparationResult.ProfileUnavailable));
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(Count(store), Is.EqualTo(1));
        Assert.That(store.GetPreparedEquipment().Get(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(Weapon));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void ChangedProfileRevision_RejectsStaleConsentEvenWithSameRemoteRevision()
    {
        LocalProfileStore store = Create(RecoveryWeaponPreparation.Source.NewGrant, out InMemoryLocalProfileRepository repository);
        store.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation);
        Assert.That(repository.TrySave(repository.Snapshot.Clone(), out _), Is.True);

        Assert.That(store.IsRecoveryWeaponPreparationCurrent(preparation), Is.False);
        Assert.That(store.TryPrepareExpeditionEquipment(preparation), Is.EqualTo(ExpeditionPreparationResult.ProfileUnavailable));
        Assert.That(Count(store), Is.Zero);
    }

    [Test]
    public void AnotherStoreOrCharacter_CannotReuseConsent()
    {
        LocalProfileStore original = Create(RecoveryWeaponPreparation.Source.NewGrant, out _);
        original.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation);
        LocalProfileStore other = Create(RecoveryWeaponPreparation.Source.NewGrant, out _);
        Assert.That(other.TryPrepareExpeditionEquipment(preparation), Is.EqualTo(ExpeditionPreparationResult.ProfileUnavailable));
        Assert.That(Count(other), Is.Zero);
    }

    private LocalProfileStore Create(RecoveryWeaponPreparation.Source source, out InMemoryLocalProfileRepository repository)
    {
        var profile = new ProfileId("47474747474747474747474747474747");
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
        if (source == RecoveryWeaponPreparation.Source.Prepared)
            snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Weapon, default);
        if (source == RecoveryWeaponPreparation.Source.Loadout) snapshot.Loadout.Add(new StashItem(Weapon, 1));
        if (source == RecoveryWeaponPreparation.Source.Stash) snapshot.Stash.Add(new StashItem(Weapon, 1));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        return new LocalProfileStore(repository, profile, _catalog, Weapon);
    }

    private static int Count(LocalProfileStore store)
    {
        int count = store.GetPreparedEquipment().Get(EquipmentSlot.WeaponSetAMainHand) == Weapon ? 1 : 0;
        foreach (StashItem item in store.GetLoadout()) if (item.LootId == Weapon) count += item.Amount;
        foreach (StashItem item in store.GetStash()) if (item.LootId == Weapon) count += item.Amount;
        return count;
    }
}
#endif

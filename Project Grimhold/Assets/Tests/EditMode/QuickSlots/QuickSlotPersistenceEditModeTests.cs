#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

[Category("TASK214")]
public sealed class QuickSlotPersistenceEditModeTests
{
    private static readonly ProfileId Profile = new ProfileId("21421421421421421421421421421421");
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Rapier = new LootId("rapier");

    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
    }

    private static string WithSchemaVersion(string json, int version)
    {
        string replaced = Regex.Replace(json, @"""schemaVersion"":\s*\d+",$"\"schemaVersion\": {version}");
        Assert.That(replaced, Is.Not.EqualTo(json));
        return replaced;
    }

    [Test]
    public void HealthPotion_IsQuickSlotCompatibleAndRapierIsNot()
    {
        Assert.That(_catalog.TryGet(Potion.Value, out LootDefinition potion), Is.True);
        Assert.That(_catalog.TryGet(Rapier.Value, out LootDefinition rapier), Is.True);

        Assert.That(potion.QuickSlotCompatible, Is.True);
        Assert.That(rapier.QuickSlotCompatible, Is.False);
        Assert.That(potion.TryValidate(out string error), Is.True, error);
    }

    [Test]
    public void Codec_RoundTripsQuickSlots()
    {
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            QuickSlots = QuickSlotBindings.Empty.With(2, Potion)
        };

        bool decoded = LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(snapshot),
            Profile,
            _catalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(restored.SchemaVersion, Is.EqualTo(LocalProfileSnapshot.CurrentSchemaVersion));
        Assert.That(restored.QuickSlots.Get(2), Is.EqualTo(Potion));
        Assert.That(restored.QuickSlots.IsAssigned(0), Is.False);
    }

    [Test]
    public void Codec_PreviousSchemaLoadsWithEmptyQuickSlots()
    {
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            QuickSlots = QuickSlotBindings.Empty.With(0, Potion)
        };
        string previousSchema = WithSchemaVersion(LocalProfileSaveCodec.Encode(snapshot), 8);

        bool decoded = LocalProfileSaveCodec.TryDecode(
            previousSchema,
            Profile,
            _catalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(restored.QuickSlots.IsEmpty, Is.True);
    }

    [Test]
    public void Codec_NewerSchemaIsRejected()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        string newerSchema = WithSchemaVersion(
            LocalProfileSaveCodec.Encode(snapshot),
            LocalProfileSnapshot.CurrentSchemaVersion + 1);

        bool decoded = LocalProfileSaveCodec.TryDecode(
            newerSchema,
            Profile,
            _catalog,
            out _,
            out LocalProfilePersistenceStatus status,
            out _);

        Assert.That(decoded, Is.False);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.UnsupportedVersion));
    }

    [Test]
    public void Codec_IncompatibleDefinitionLoadsUnassigned()
    {
        var snapshot = new LocalProfileSnapshot
        {
            ProfileId = Profile,
            QuickSlots = QuickSlotBindings.Empty.With(0, Rapier).With(1, Potion)
        };

        bool decoded = LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(snapshot),
            Profile,
            _catalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(restored.QuickSlots.IsAssigned(0), Is.False);
        Assert.That(restored.QuickSlots.Get(1), Is.EqualTo(Potion));
    }

    [Test]
    public void Store_CommitsQuickSlotsPublishesAndLeavesInventoryUntouched()
    {
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        var seeded = repository.Snapshot.Clone();
        seeded.Stash.Add(new StashItem(Potion, 3));
        seeded.Loadout.Add(new StashItem(Potion, 2));
        Assert.That(repository.TrySave(seeded, out string saveError), Is.True, saveError);
        var store = new LocalProfileStore(repository, Profile, _catalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        StashOperationResult result = store.TrySetQuickSlots(QuickSlotBindings.Empty.With(3, Potion));

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(store.GetQuickSlots().Get(3), Is.EqualTo(Potion));
        Assert.That(store.GetStash(), Has.Count.EqualTo(1));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(3));
        Assert.That(store.GetLoadout(), Has.Count.EqualTo(1));
        Assert.That(store.GetLoadout()[0].Amount, Is.EqualTo(2));
    }

    [Test]
    public void Store_RejectsIncompatibleDefinitionWithoutCommitting()
    {
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, Profile, _catalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        StashOperationResult result = store.TrySetQuickSlots(QuickSlotBindings.Empty.With(0, Rapier));

        Assert.That(result, Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(commits, Is.Zero);
        Assert.That(store.GetQuickSlots().IsEmpty, Is.True);
    }

    [Test]
    public void Store_UnchangedBindingsDoNotCommit()
    {
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, Profile, _catalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        StashOperationResult result = store.TrySetQuickSlots(QuickSlotBindings.Empty);

        Assert.That(result, Is.EqualTo(StashOperationResult.Success));
        Assert.That(commits, Is.Zero);
    }
}
#endif

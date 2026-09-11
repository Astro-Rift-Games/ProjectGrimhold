#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LocalLoadoutInventoryReadSourceTests
{
    private static readonly ProfileId Profile = new("11111111111111111111111111111111");
    private static readonly ProfileId OtherProfile = new("22222222222222222222222222222222");

    private GameObject _contextObject;
    private ApplicationStashContext _context;
    private FakeLoadoutService _loadoutService;
    private LocalLoadoutInventoryReadSource _source;

    [SetUp]
    public void SetUp()
    {
        LootDefinitionCatalog catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, catalog), Is.True);

        _contextObject = new GameObject("ApplicationStashContextTests");
        _context = _contextObject.AddComponent<ApplicationStashContext>();
        _loadoutService = new FakeLoadoutService();
        _context.Initialize(
            new LocalProfileStore(repository, Profile, catalog),
            null,
            _loadoutService,
            null,
            null);
        _source = new LocalLoadoutInventoryReadSource(_context, Profile);
    }

    [TearDown]
    public void TearDown()
    {
        _source?.Dispose();
        UnityEngine.Object.DestroyImmediate(_contextObject);
    }

    [Test]
    public void Read_ProjectsOrderedLoadoutWithFixedCapacity()
    {
        _loadoutService.Loadout = new[]
        {
            new StashItem(new LootId("bone"), 3),
            new StashItem(new LootId("healthpotion"), 2)
        };

        Assert.That(_source.SlotCapacity, Is.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
        Assert.That(_source.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
        Assert.That(content.Count, Is.EqualTo(2));
        Assert.That(content[0], Is.EqualTo(new LootEntry(new LootId("bone"), 3)));
        Assert.That(content[1], Is.EqualTo(new LootEntry(new LootId("healthpotion"), 2)));
    }

    [Test]
    public void Read_InvalidOrOversizedLoadout_IsUnavailable()
    {
        _loadoutService.Loadout = new[] { new StashItem(default, 1) };
        Assert.That(_source.TryGetLootContent(out _), Is.False);

        var oversized = new List<StashItem>();
        for (int index = 0; index <= LocalProfileSnapshot.MaxLoadoutSlots; index++)
        {
            oversized.Add(new StashItem(new LootId($"item_{index}"), 1));
        }

        _loadoutService.Loadout = oversized;
        Assert.That(_source.TryGetLootContent(out _), Is.False);
    }

    [Test]
    public void Read_ProjectsConfirmedPreparedEquipment()
    {
        var expected = new PreparedEquipmentLoadout(
            new LootId("rapier"),
            new LootId("arming_sword"),
            new LootId("helmet"),
            new LootId("armor"),
            new LootId("gloves"),
            new LootId("boots"));
        _loadoutService.PreparedEquipment = expected;

        Assert.That(_source.TryGetPreparedEquipment(out PreparedEquipmentLoadout equipment), Is.True);
        Assert.That(equipment.WeaponSetAMainHand, Is.EqualTo(expected.WeaponSetAMainHand));
        Assert.That(equipment.WeaponSetBMainHand, Is.EqualTo(expected.WeaponSetBMainHand));
        Assert.That(equipment.Helmet, Is.EqualTo(expected.Helmet));
        Assert.That(equipment.Armor, Is.EqualTo(expected.Armor));
        Assert.That(equipment.Gloves, Is.EqualTo(expected.Gloves));
        Assert.That(equipment.Boots, Is.EqualTo(expected.Boots));
    }

    [Test]
    public void Commits_AreFilteredAndDisposalIsIdempotent()
    {
        int notificationCount = 0;
        _source.Changed += () => notificationCount++;

        PublishCommit(OtherProfile);
        Assert.That(notificationCount, Is.Zero);
        Assert.That(_source.Revision, Is.Zero);

        PublishCommit(Profile);
        Assert.That(notificationCount, Is.EqualTo(1));
        Assert.That(_source.Revision, Is.EqualTo(1));

        _source.Dispose();
        _source.Dispose();
        PublishCommit(Profile);
        Assert.That(notificationCount, Is.EqualTo(1));
        Assert.That(_source.TryGetLootContent(out _), Is.False);
    }

    private void PublishCommit(ProfileId profileId)
    {
        MethodInfo method = typeof(ApplicationStashContext).GetMethod(
            "OnProfileCommitted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(_context, new object[] { profileId });
    }

    private sealed class FakeLoadoutService : IPlayerLoadoutService
    {
        public IReadOnlyList<StashItem> Loadout { get; set; } = Array.Empty<StashItem>();
        public PreparedEquipmentLoadout PreparedEquipment { get; set; }
        public event Action<ProfileId> LoadoutChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId) => Loadout;
        public PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId) => PreparedEquipment;
        public StashOperationResult TryAssignPreparedEquipment(ProfileId profileId, EquipmentSlot slot, LootId lootId) => StashOperationResult.InvalidInventory;
        public StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot) => StashOperationResult.InvalidInventory;
        public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId) => ExpeditionPreparationResult.ProfileUnavailable;
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
#endif

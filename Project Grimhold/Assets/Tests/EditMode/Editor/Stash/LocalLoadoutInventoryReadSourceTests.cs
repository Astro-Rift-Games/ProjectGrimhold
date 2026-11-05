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
    private readonly List<UnityEngine.Object> _created = new();

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
        for (int i = _created.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
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
    public void Read_TownPanelKeepsDistinctInstancesAndUsesConfirmedAttributesForTooltips()
    {
        var attack = Create<MeleeAttackConfig>();
        SetField(attack, "_targetLayerMask", new LayerMask { value = 1 });
        var weapon = Create<WeaponDefinition>();
        SetField(weapon, "_baseDamage", 30f);
        SetField(weapon, "_range", 1.5f);
        SetField(weapon, "_primaryAttack", attack);
        var definition = Create<LootDefinition>();
        SetField(definition, "_id", "sword");
        SetField(definition, "_displayName", "Sword");
        SetField(definition, "_category", LootCategory.Weapon);
        SetField(definition, "_weaponDefinition", weapon);
        var gem = Create<LootDefinition>();
        SetField(gem, "_id", "ruby");
        SetField(gem, "_displayName", "Ruby");
        var gemDefinition = Create<GemDefinition>();
        SetField(gemDefinition, "_damageBonus", 5);
        SetField(gem, "_gemDefinition", gemDefinition);
        var catalog = Create<LootDefinitionCatalog>();
        SetField(catalog, "_definitions", new List<LootDefinition> { definition, gem });
        Assert.That(WeaponScalingModifier.TryCreate(CharacterAttribute.Strength, WeaponScalingGrade.S,
            out var primary, out _), Is.True);
        Assert.That(WeaponGemSocket.TryCreateOccupied(gem.LootId, out var socket, out _), Is.True);
        Assert.That(WeaponInstanceModifiers.TryCreate(primary, default, CharacterAttribute.Strength,
            1, socket, default, out var modifiers, out _), Is.True);
        var first = new EquipmentInstance("0123456789abcdef01234567", modifiers);
        var second = EquipmentInstance.PlainCache(definition);
        _loadoutService.Loadout = new[] {
            new StashItem(definition.LootId, 1, 2, first),
            new StashItem(definition.LootId, 1, 7, second) };
        Assert.That(CharacterAttributeState.TryCreate(5, 5, 20, 5, 5, 5, 0, out var attributes), Is.True);
        _context.Store.ForceCharacterAttributeState(attributes);

        var root = new GameObject("Town projection test", typeof(RectTransform));
        _created.Add(root);
        var view = root.AddComponent<RaidLootPanelView>();
        var cells = new RaidInventorySlotView[LocalProfileSnapshot.MaxLoadoutSlots];
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = new GameObject($"Slot {i}", typeof(RectTransform));
            cell.transform.SetParent(root.transform);
            cells[i] = cell.AddComponent<RaidInventorySlotView>();
        }
        SetField(view, "_authoredSlots", cells);
        var presenter = new RaidLootPanelPresenter();
        Assert.That(presenter.Refresh(_source, catalog, view, null, false,
            RaidLootSlotInteractionMode.ReadOnly, default, root), Is.True);
        Assert.That(presenter.Slots[0].Tooltip.Body, Does.Contain("Ruby"));
        Assert.That(presenter.Slots[0].Tooltip.Body, Does.Contain("Daño efectivo: 42"));
        Assert.That(presenter.Slots[1].Tooltip.Body, Does.Contain("Daño efectivo: 30"));
        Assert.That(presenter.Slots[1].Tooltip.Body, Does.Not.Contain("Ruby"));
        Assert.That(_source.TryGetLootContent(out var content), Is.True);
        var physical = ((TownInventoryProjection)content).PhysicalItems;
        Assert.That(physical[0].Instance, Is.SameAs(first));
        Assert.That(physical[1].Instance, Is.SameAs(second));
        Assert.That(_loadoutService.Loadout.Count, Is.EqualTo(2));
        Assert.That(_loadoutService.Loadout[0].Slot, Is.EqualTo(2));
        Assert.That(_loadoutService.Loadout[1].Slot, Is.EqualTo(7));

        _loadoutService.PreparedEquipment = new PreparedEquipmentLoadout(definition.LootId, definition.LootId,
            mainAInstance: first, mainBInstance: second);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        var screen = UnityEngine.Object.Instantiate(prefab);
        _created.Add(screen);
        var inventory = screen.GetComponent<RaidInventoryPresenter>();
        SetField(inventory, "_lootCatalog", catalog);
        var reader = root.AddComponent<PlayerInputReader>();
        inventory.BindTown(_source, _source,
            new TownEquipmentMutationEndpoint(_loadoutService, Profile, catalog, () => true),
            reader, externallyHosted: true, readConfirmedAttributes: _source.ReadConfirmedAttributes);
        inventory.ShowHosted();
        var inventoryView = screen.GetComponent<RaidInventoryView>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var mainA = (RaidInventorySlotView)typeof(RaidInventoryView)
            .GetField("_weaponSetAMainHandView", flags).GetValue(inventoryView);
        var mainB = (RaidInventorySlotView)typeof(RaidInventoryView)
            .GetField("_weaponSetBMainHandView", flags).GetValue(inventoryView);
        foreach (var cell in new[] { mainA, mainB })
        {
            EquipmentTooltipPresentation shown = default;
            cell.TooltipRequested += (tooltip, _) => shown = tooltip;
            cell.OnPointerEnter(null);
            Assert.That(shown.Body, Does.Contain(cell == mainA ? "Daño efectivo: 42" : "Daño efectivo: 30"));
            cell.OnPointerExit(null);
        }
        inventory.Unbind();
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        _created.Add(value);
        return value;
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

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
        public StashOperationResult TryOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) => StashOperationResult.InvalidInventory;
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
        public StashOperationResult TryEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot, out PreparedEquipmentLoadout equipment)
        {
            equipment = default;
            return StashOperationResult.InvalidInventory;
        }
        public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId) => ExpeditionPreparationResult.ProfileUnavailable;
        public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
        public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
        public StashOperationResult TryTransferAllToLoadout(ProfileId profileId) => StashOperationResult.InvalidInventory;
        public StashOperationResult TryTransferAllToStash(ProfileId profileId) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateTransferToLoadout(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateTransferToStash(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateTransferAllToLoadout(ProfileId profileId) => StashOperationResult.InvalidInventory;
        public StashOperationResult EvaluateTransferAllToStash(ProfileId profileId) => StashOperationResult.InvalidInventory;
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

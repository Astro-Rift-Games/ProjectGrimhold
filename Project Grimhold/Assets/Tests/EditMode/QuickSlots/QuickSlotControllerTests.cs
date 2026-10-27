#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

[Category("TASK214"), Category("TASK230")]
public sealed class QuickSlotControllerTests
{
    private static readonly ProfileId Profile = new ProfileId("21521521521521521521521521521521");
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Rapier = new LootId("rapier");
    private static readonly LootId Compass = new LootId("UTL_RUNE_COMPASS");

    private LootDefinitionCatalog _catalog;
    private LocalProfileStore _store;
    private FakeInventory _inventory;
    private bool _canRelease;
    private QuickSlotController _controller;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        _store = new LocalProfileStore(repository, Profile, _catalog);
        _inventory = new FakeInventory();
        _canRelease = true;
        _controller = new QuickSlotController(_store, _inventory, () => _canRelease);
    }

    [TearDown]
    public void TearDown() => _controller.Dispose();

    [Test]
    public void Assign_CompatibleItemInInventory_PersistsBinding()
    {
        _inventory.Set(new LootEntry(Potion, 2));

        var result = _controller.TryAssign(1, Potion);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Success));
        Assert.That(_store.GetQuickSlots().Get(1), Is.EqualTo(Potion));
        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(2));
    }

    [Test]
    public void Assign_UtilityWithQuickSlotCompatible_IsAccepted()
    {
        _inventory.Set(new LootEntry(Compass, 1));

        var result = _controller.TryAssign(2, Compass);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Success));
        Assert.That(_store.GetQuickSlots().Get(2), Is.EqualTo(Compass));
        Assert.That(_controller.GetAvailableAmount(Compass), Is.EqualTo(1));
    }

    [Test]
    public void Assign_AmountSumsEveryStackOfTheDefinition()
    {
        _inventory.Set(new LootEntry(Potion, 2), new LootEntry(Rapier, 1), new LootEntry(Potion, 3));

        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(5));
    }

    [Test]
    public void Assign_ItemMissingFromInventory_IsRejected()
    {
        var result = _controller.TryAssign(0, Potion);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.NotInInventory));
        Assert.That(_store.GetQuickSlots().IsEmpty, Is.True);
    }

    [Test]
    public void Assign_IncompatibleItem_IsRejected()
    {
        _inventory.Set(new LootEntry(Rapier, 1));

        var result = _controller.TryAssign(0, Rapier);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.NotCompatible));
        Assert.That(_store.GetQuickSlots().IsEmpty, Is.True);
    }

    [Test]
    public void Assign_DefinitionAlreadyInAnotherSlot_IsRejectedWithAlreadyAssigned()
    {
        _inventory.Set(new LootEntry(Potion, 2));
        _controller.TryAssign(0, Potion);

        var result = _controller.TryAssign(1, Potion);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.AlreadyAssigned));
        Assert.That(_store.GetQuickSlots().Get(0), Is.EqualTo(Potion));
        Assert.That(_store.GetQuickSlots().IsAssigned(1), Is.False);
    }

    [Test]
    public void Assign_DoesNotChangeTheInventory()
    {
        _inventory.Set(new LootEntry(Potion, 2));

        _controller.TryAssign(0, Potion);
        _controller.TryClear(0);

        Assert.That(_inventory.Content, Has.Count.EqualTo(1));
        Assert.That(_inventory.Content[0], Is.EqualTo(new LootEntry(Potion, 2)));
    }

    [Test]
    public void Depletion_LeavesSlotBoundWithZeroAvailableUnits()
    {
        _inventory.Set(new LootEntry(Potion, 1));
        _controller.TryAssign(3, Potion);

        _inventory.Set();

        Assert.That(_store.GetQuickSlots().Get(3), Is.EqualTo(Potion));
        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(0));
    }

    [Test]
    public void PickingUpUnits_ReactivatesDepletedQuickSlot()
    {
        _inventory.Set(new LootEntry(Potion, 1));
        _controller.TryAssign(3, Potion);

        _inventory.Set();
        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(0));

        _inventory.Set(new LootEntry(Potion, 5));
        Assert.That(_store.GetQuickSlots().Get(3), Is.EqualTo(Potion));
        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(5));
    }

    [Test]
    public void ReturningUnitsAfterATransientEmptyInventory_KeepsTheBinding()
    {
        _inventory.Set(new LootEntry(Potion, 1));
        _controller.TryAssign(0, Potion);
        _canRelease = false;
        _inventory.Set();
        _canRelease = true;

        _inventory.Set(new LootEntry(Potion, 4));

        Assert.That(_store.GetQuickSlots().Get(0), Is.EqualTo(Potion));
        Assert.That(_controller.GetAvailableAmount(Potion), Is.EqualTo(4));
    }

    [Test]
    public void ChangedIsRaisedForInventoryAndBindingChanges()
    {
        _inventory.Set(new LootEntry(Potion, 2));
        int changes = 0;
        _controller.Changed += () => changes++;

        _controller.TryAssign(0, Potion);
        int afterAssign = changes;
        _inventory.Set(new LootEntry(Potion, 1));

        Assert.That(afterAssign, Is.GreaterThan(0));
        Assert.That(changes, Is.GreaterThan(afterAssign));
    }

    [TestCase(DragSlotLocation.Inventory, EquipmentSlot.None, true)]
    [TestCase(DragSlotLocation.Container, EquipmentSlot.None, false)]
    [TestCase(DragSlotLocation.Equipment, EquipmentSlot.None, false)]
    [TestCase(DragSlotLocation.Stash, EquipmentSlot.None, false)]
    [TestCase(DragSlotLocation.Inventory, EquipmentSlot.Helmet, false)]
    public void DropPolicy_OnlyInventoryContentCanBeDroppedOnAQuickSlot(
        DragSlotLocation source,
        EquipmentSlot targetEquipmentSlot,
        bool expected)
    {
        Assert.That(
            DropPolicy.CanAttemptDrop(source, DragSlotLocation.QuickSlot, targetEquipmentSlot),
            Is.EqualTo(expected));
    }

    private sealed class FakeInventory : IInventoryReadSource
    {
        private List<LootEntry> _content = new();

        public int SlotCapacity => 30;
        public int Revision { get; private set; }
        public IReadOnlyList<LootEntry> Content => _content;
        public event Action Changed;

        public void Set(params LootEntry[] entries)
        {
            _content = new List<LootEntry>(entries);
            Revision++;
            Changed?.Invoke();
        }

        public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
        {
            content = _content;
            return true;
        }
    }
}
#endif

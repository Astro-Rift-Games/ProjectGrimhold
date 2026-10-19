#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK214")]
public sealed class QuickSlotAssignmentRulesTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Elixir = new LootId("elixir");

    [Test]
    public void Assign_CompatibleItemInInventory_BindsSlot()
    {
        var result = QuickSlotAssignmentRules.TryAssign(
            QuickSlotBindings.Empty, 0, Potion, true, 3, out var next);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Success));
        Assert.That(next.Get(0), Is.EqualTo(Potion));
    }

    [Test]
    public void Assign_IncompatibleItem_IsRejectedAndKeepsBindings()
    {
        var result = QuickSlotAssignmentRules.TryAssign(
            QuickSlotBindings.Empty, 0, Potion, false, 3, out var next);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.NotCompatible));
        Assert.That(next, Is.EqualTo(QuickSlotBindings.Empty));
    }

    [Test]
    public void Assign_ItemNotInInventory_IsRejected()
    {
        var result = QuickSlotAssignmentRules.TryAssign(
            QuickSlotBindings.Empty, 0, Potion, true, 0, out var next);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.NotInInventory));
        Assert.That(next.IsEmpty, Is.True);
    }

    [TestCase(-1)]
    [TestCase(4)]
    public void Assign_InvalidSlotIndex_IsRejected(int slotIndex)
    {
        var result = QuickSlotAssignmentRules.TryAssign(
            QuickSlotBindings.Empty, slotIndex, Potion, true, 1, out _);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.InvalidSlot));
    }

    [Test]
    public void Assign_DefinitionAlreadyInAnotherSlot_MovesTheBinding()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 0, Potion, true, 1, out var first);

        var result = QuickSlotAssignmentRules.TryAssign(first, 2, Potion, true, 1, out var moved);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Success));
        Assert.That(moved.IsAssigned(0), Is.False);
        Assert.That(moved.Get(2), Is.EqualTo(Potion));
        Assert.That(moved.FindSlot(Potion), Is.EqualTo(2));
    }

    [Test]
    public void Assign_SameDefinitionInSameSlot_IsUnchanged()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 1, Potion, true, 1, out var first);

        var result = QuickSlotAssignmentRules.TryAssign(first, 1, Potion, true, 1, out var next);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Unchanged));
        Assert.That(next, Is.EqualTo(first));
    }

    [Test]
    public void Assign_DifferentDefinitionInOccupiedSlot_ReplacesIt()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 0, Potion, true, 1, out var first);

        QuickSlotAssignmentRules.TryAssign(first, 0, Elixir, true, 1, out var next);

        Assert.That(next.Get(0), Is.EqualTo(Elixir));
        Assert.That(next.FindSlot(Potion), Is.EqualTo(-1));
    }

    [Test]
    public void Clear_AssignedSlot_Unassigns()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 3, Potion, true, 1, out var first);

        var result = QuickSlotAssignmentRules.TryClear(first, 3, out var next);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Success));
        Assert.That(next.IsEmpty, Is.True);
    }

    [Test]
    public void Clear_EmptySlot_IsUnchanged()
    {
        var result = QuickSlotAssignmentRules.TryClear(QuickSlotBindings.Empty, 0, out _);

        Assert.That(result, Is.EqualTo(QuickSlotAssignmentResult.Unchanged));
    }

    [Test]
    public void ReleaseDepleted_PositiveToZero_ClearsOnlyThatSlot()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 0, Potion, true, 1, out var first);
        QuickSlotAssignmentRules.TryAssign(first, 1, Elixir, true, 1, out var both);

        bool changed = QuickSlotAssignmentRules.TryReleaseDepleted(
            both, new[] { 1, 2, 0, 0 }, new[] { 0, 2, 0, 0 }, out var next);

        Assert.That(changed, Is.True);
        Assert.That(next.IsAssigned(0), Is.False);
        Assert.That(next.Get(1), Is.EqualTo(Elixir));
    }

    [Test]
    public void ReleaseDepleted_AlreadyZeroOrUnobserved_KeepsBinding()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 0, Potion, true, 1, out var first);

        bool changed = QuickSlotAssignmentRules.TryReleaseDepleted(
            first, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 }, out var next);

        Assert.That(changed, Is.False);
        Assert.That(next, Is.EqualTo(first));
    }

    [Test]
    public void ReleaseDepleted_MalformedAmountArrays_KeepBindings()
    {
        QuickSlotAssignmentRules.TryAssign(QuickSlotBindings.Empty, 0, Potion, true, 1, out var first);

        Assert.That(QuickSlotAssignmentRules.TryReleaseDepleted(first, null, new[] { 0, 0, 0, 0 }, out var next), Is.False);
        Assert.That(QuickSlotAssignmentRules.TryReleaseDepleted(first, new[] { 1 }, new[] { 0 }, out next), Is.False);
        Assert.That(next, Is.EqualTo(first));
    }

    [Test]
    public void TryCreate_RejectsDuplicatesAndWrongLength()
    {
        Assert.That(QuickSlotBindings.TryCreate(new[] { "a", "a", null, null }, out _, out string duplicateError), Is.False);
        Assert.That(duplicateError, Does.Contain("more than one slot"));
        Assert.That(QuickSlotBindings.TryCreate(new[] { "a" }, out _, out _), Is.False);
    }

    [Test]
    public void TryCreate_BlankEntriesAreUnassigned()
    {
        Assert.That(QuickSlotBindings.TryCreate(new[] { "healthpotion", "", null, "elixir" }, out var bindings, out _), Is.True);

        Assert.That(bindings.Get(0), Is.EqualTo(Potion));
        Assert.That(bindings.IsAssigned(1), Is.False);
        Assert.That(bindings.Get(3), Is.EqualTo(Elixir));
    }
}
#endif

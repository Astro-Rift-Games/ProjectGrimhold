using NUnit.Framework;
using UnityEngine;

public sealed class AbilityRuntimeSlotsTests
{
    private AbilityDefinition _first;
    private AbilityDefinition _second;
    private AbilityDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _first = AbilityTestFactory.CreateDefinition("runtime_first");
        _second = AbilityTestFactory.CreateDefinition("runtime_second");
        _catalog = AbilityTestFactory.CreateCatalog(_first, _second);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_catalog);
        Object.DestroyImmediate(_first);
        Object.DestroyImmediate(_second);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void PreparedPair_ResolvesExactlyWithIndependentSlots(bool firstPrepared, bool secondPrepared)
    {
        var prepared = new PreparedAbilityLoadout(
            firstPrepared ? _first.AbilityId : default,
            secondPrepared ? _second.AbilityId : default);

        Assert.That(AbilityRuntimeSlots.TryCreate(prepared, _catalog, out var slots, out string error), Is.True, error);
        Assert.That(slots.TryGetSlot(UniversalAbilitySlot.Slot1, out var first), Is.True);
        Assert.That(slots.TryGetSlot(UniversalAbilitySlot.Slot2, out var second), Is.True);
        Assert.That(first.Slot, Is.EqualTo(UniversalAbilitySlot.Slot1));
        Assert.That(second.Slot, Is.EqualTo(UniversalAbilitySlot.Slot2));
        Assert.That(first.AbilityId, Is.EqualTo(prepared.Slot1));
        Assert.That(second.AbilityId, Is.EqualTo(prepared.Slot2));
        Assert.That(first.IsPrepared, Is.EqualTo(firstPrepared));
        Assert.That(second.IsPrepared, Is.EqualTo(secondPrepared));
        Assert.That(first.Definition, Is.SameAs(firstPrepared ? _first : null));
        Assert.That(second.Definition, Is.SameAs(secondPrepared ? _second : null));
        Assert.That(slots.TryGetSlot((UniversalAbilitySlot)99, out _), Is.False);
    }

    [Test]
    public void DuplicatePreparedIdentity_IsRejectedAtomically()
    {
        AssertRejected(new PreparedAbilityLoadout(_first.AbilityId, _first.AbilityId), _catalog);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnknownPreparedIdentity_IsRejectedAtomically(bool unknownFirst)
    {
        var unknown = new AbilityId("runtime_unknown");
        AssertRejected(new PreparedAbilityLoadout(
            unknownFirst ? unknown : _first.AbilityId,
            unknownFirst ? _second.AbilityId : unknown), _catalog);
    }

    [Test]
    public void MissingCatalog_IsRejectedEvenForEmptySlots()
    {
        AssertRejected(default, null);
    }

    [Test]
    public void InvalidCatalogDefinition_IsRejectedEvenWhenNotPrepared()
    {
        AbilityTestFactory.SetPrivateField(_second, "_cost", -1);
        AssertRejected(new PreparedAbilityLoadout(_first.AbilityId, default), _catalog);
    }

    [Test]
    public void DuplicateCatalogIdentity_IsRejected()
    {
        AbilityTestFactory.SetPrivateField(_second, "_id", _first.Id);
        AssertRejected(default, _catalog);
    }

    private static void AssertRejected(in PreparedAbilityLoadout prepared, AbilityDefinitionCatalog catalog)
    {
        Assert.That(AbilityRuntimeSlots.TryCreate(prepared, catalog, out var slots, out string error), Is.False);
        Assert.That(slots, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }
}

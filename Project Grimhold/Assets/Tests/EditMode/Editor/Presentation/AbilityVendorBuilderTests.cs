#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[Category("TASK206")]
public sealed class AbilityVendorBuilderTests
{
    private const long Price = 100L;

    private static readonly AbilityId ChargeId = new("charge");
    private static readonly AbilityId TrapId = new("trap");

    private readonly List<Object> _created = new();
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;

    [SetUp]
    public void SetUp()
    {
        _charge = Define("charge", new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        _trap = Define("trap");
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(_created[index]);
        }

        _created.Clear();
    }

    [Test]
    public void Build_EmptyOffers_ReturnsNoEntries()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(new AbilityVendorOffer[0], new AbilityId[0], Price);

        Assert.That(entries, Is.Empty);
    }

    [Test]
    public void Build_AffordableUnownedOffer_IsAvailable()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new AbilityId[0], Price + 1);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].AbilityId, Is.EqualTo(TrapId));
        Assert.That(entries[0].Definition, Is.SameAs(_trap));
        Assert.That(entries[0].Price, Is.EqualTo(Price));
        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.Available));
        Assert.That(entries[0].CanBuy, Is.True);
    }

    [Test]
    public void Build_ExactCurrency_IsAvailable()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new AbilityId[0], Price);

        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.Available));
        Assert.That(entries[0].CanBuy, Is.True);
    }

    [Test]
    public void Build_OwnedOffer_IsAlreadyUnlocked()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new[] { TrapId }, Price * 10);

        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.AlreadyUnlocked));
        Assert.That(entries[0].CanBuy, Is.False);
    }

    [Test]
    public void Build_OwnedOfferWithoutCurrency_IsAlreadyUnlockedNotInsufficient()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new[] { TrapId }, 0L);

        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.AlreadyUnlocked));
        Assert.That(entries[0].CanBuy, Is.False);
    }

    [Test]
    public void Build_UnaffordableOffer_IsInsufficientCurrency()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new AbilityId[0], Price - 1);

        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.InsufficientCurrency));
        Assert.That(entries[0].CanBuy, Is.False);
    }

    [Test]
    public void Build_UnmetRequirements_StaysAvailableAndFlagsRequirements()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_charge, Price) },
            new AbilityId[0],
            Price,
            AbilityTestFactory.CreateAttributes(strength: 9));

        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.Available));
        Assert.That(entries[0].CanBuy, Is.True);
        Assert.That(entries[0].RequirementsMet, Is.False);
    }

    [Test]
    public void Build_MetRequirements_FlagsRequirementsMet()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_charge, Price) },
            new AbilityId[0],
            Price,
            AbilityTestFactory.CreateAttributes(strength: 10));

        Assert.That(entries[0].RequirementsMet, Is.True);
    }

    [Test]
    public void Build_NoRequirements_AreMet()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, Price) }, new AbilityId[0], Price);

        Assert.That(entries[0].RequirementsMet, Is.True);
    }

    [Test]
    public void Build_PreservesAuthoredOrder()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[] { new AbilityVendorOffer(_trap, 50L), new AbilityVendorOffer(_charge, 200L) },
            new[] { ChargeId },
            Price);

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].AbilityId, Is.EqualTo(TrapId));
        Assert.That(entries[0].State, Is.EqualTo(AbilityVendorOfferState.Available));
        Assert.That(entries[1].AbilityId, Is.EqualTo(ChargeId));
        Assert.That(entries[1].State, Is.EqualTo(AbilityVendorOfferState.AlreadyUnlocked));
    }

    [Test]
    public void Build_NullDefinitionOffer_IsSkipped()
    {
        IReadOnlyList<AbilityVendorEntry> entries = Build(
            new[]
            {
                new AbilityVendorOffer(null, Price),
                new AbilityVendorOffer(_trap, Price)
            },
            new AbilityId[0],
            Price);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].AbilityId, Is.EqualTo(TrapId));
    }

    private static IReadOnlyList<AbilityVendorEntry> Build(
        AbilityVendorOffer[] offers,
        AbilityId[] unlocked,
        long currency,
        CharacterAttributeState? attributes = null)
    {
        return AbilityVendorBuilder.Build(
            offers,
            unlocked,
            attributes ?? AbilityTestFactory.CreateAttributes(),
            currency);
    }

    private AbilityDefinition Define(string id, params CharacterAttributeRequirement[] requirements)
    {
        AbilityDefinition definition = AbilityTestFactory.CreateDefinition(id, requirements: requirements);
        _created.Add(definition);
        return definition;
    }
}
#endif

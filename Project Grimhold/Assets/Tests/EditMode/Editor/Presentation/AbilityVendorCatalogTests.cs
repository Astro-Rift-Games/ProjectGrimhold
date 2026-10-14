#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[Category("TASK206")]
public sealed class AbilityVendorCatalogTests
{
    private readonly List<Object> _created = new();
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;
    private AbilityDefinitionCatalog _abilityCatalog;

    [SetUp]
    public void SetUp()
    {
        _charge = Track(AbilityTestFactory.CreateDefinition("charge"));
        _trap = Track(AbilityTestFactory.CreateDefinition("trap"));
        _abilityCatalog = Track(AbilityTestFactory.CreateCatalog(_charge, _trap));
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
    public void TryValidate_ValidOffers_ReturnsTrue()
    {
        AbilityVendorCatalog catalog = CreateCatalog(
            new AbilityVendorOffer(_charge, 100L), new AbilityVendorOffer(_trap, 250L));

        bool valid = catalog.TryValidate(_abilityCatalog, out string error);

        Assert.That(valid, Is.True, error);
        Assert.That(error, Is.Null);
        Assert.That(catalog.Offers, Has.Count.EqualTo(2));
    }

    [Test]
    public void TryValidate_NullAbility_ReturnsFalse()
    {
        AbilityVendorCatalog catalog = CreateCatalog(new AbilityVendorOffer(null, 100L));

        Assert.That(catalog.TryValidate(_abilityCatalog, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryValidate_InvalidAbilityId_ReturnsFalse()
    {
        AbilityDefinition invalid = Track(AbilityTestFactory.CreateDefinition("Not Valid"));
        AbilityVendorCatalog catalog = CreateCatalog(new AbilityVendorOffer(invalid, 100L));

        Assert.That(catalog.TryValidate(_abilityCatalog, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryValidate_UnknownAbility_ReturnsFalse()
    {
        AbilityDefinition unknown = Track(AbilityTestFactory.CreateDefinition("not_in_catalog"));
        AbilityVendorCatalog catalog = CreateCatalog(new AbilityVendorOffer(unknown, 100L));

        Assert.That(catalog.TryValidate(_abilityCatalog, out string error), Is.False);
        Assert.That(error, Does.Contain("not_in_catalog"));
    }

    [TestCase(0L)]
    [TestCase(-5L)]
    public void TryValidate_NonPositivePrice_ReturnsFalse(long price)
    {
        AbilityVendorCatalog catalog = CreateCatalog(new AbilityVendorOffer(_charge, price));

        Assert.That(catalog.TryValidate(_abilityCatalog, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryValidate_DuplicateAbility_ReturnsFalse()
    {
        AbilityVendorCatalog catalog = CreateCatalog(
            new AbilityVendorOffer(_charge, 100L), new AbilityVendorOffer(_charge, 200L));

        Assert.That(catalog.TryValidate(_abilityCatalog, out string error), Is.False);
        Assert.That(error, Does.Contain("charge"));
    }

    [Test]
    public void TryValidate_NullAbilityCatalog_ReturnsFalse()
    {
        AbilityVendorCatalog catalog = CreateCatalog(new AbilityVendorOffer(_charge, 100L));

        Assert.That(catalog.TryValidate(null, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryGetOffer_ReturnsAuthoredPrice()
    {
        AbilityVendorCatalog catalog = CreateCatalog(
            new AbilityVendorOffer(_charge, 100L), new AbilityVendorOffer(_trap, 250L));

        Assert.That(catalog.TryGetOffer(new AbilityId("trap"), out AbilityVendorOffer offer), Is.True);
        Assert.That(offer.Price, Is.EqualTo(250L));
        Assert.That(catalog.TryGetOffer(new AbilityId("missing"), out _), Is.False);
    }

    private AbilityVendorCatalog CreateCatalog(params AbilityVendorOffer[] offers)
    {
        var catalog = ScriptableObject.CreateInstance<AbilityVendorCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        AbilityTestFactory.SetPrivateField(catalog, "_offers", new List<AbilityVendorOffer>(offers));
        return Track(catalog);
    }

    private T Track<T>(T created) where T : Object
    {
        _created.Add(created);
        return created;
    }
}
#endif

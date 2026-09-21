using NUnit.Framework;
using UnityEngine;

public sealed class AbilityDefinitionCatalogTests
{
    private AbilityDefinition _first;
    private AbilityDefinition _second;
    private AbilityDefinitionCatalog _catalog;

    [TearDown]
    public void TearDown()
    {
        if (_catalog != null) Object.DestroyImmediate(_catalog);
        if (_first != null) Object.DestroyImmediate(_first);
        if (_second != null && _second != _first) Object.DestroyImmediate(_second);
    }

    [Test]
    public void ValidCatalog_ResolvesIdentityInBothDirections()
    {
        _first = AbilityTestFactory.CreateDefinition("charge");
        _second = AbilityTestFactory.CreateDefinition("trap");
        _catalog = AbilityTestFactory.CreateCatalog(_first, _second);

        Assert.That(_catalog.TryValidate(out string error), Is.True, error);
        Assert.That(_catalog.DefinitionCount, Is.EqualTo(2));
        Assert.That(_catalog.TryGet(new AbilityId("trap"), out AbilityDefinition resolved), Is.True);
        Assert.That(resolved, Is.SameAs(_second));
        Assert.That(_catalog.TryGetId(_first, out AbilityId abilityId), Is.True);
        Assert.That(abilityId, Is.EqualTo(new AbilityId("charge")));
    }

    [Test]
    public void DuplicateIdentity_FailsValidation()
    {
        _first = AbilityTestFactory.CreateDefinition("charge");
        _second = AbilityTestFactory.CreateDefinition("charge");
        _catalog = AbilityTestFactory.CreateCatalog(_first, _second);

        Assert.That(_catalog.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("duplicate ID"));
    }

    [Test]
    public void DuplicateReference_FailsValidation()
    {
        _first = AbilityTestFactory.CreateDefinition("charge");
        _catalog = AbilityTestFactory.CreateCatalog(_first, _first);

        Assert.That(_catalog.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("duplicate reference"));
    }

    [Test]
    public void NullDefinition_FailsValidation()
    {
        _catalog = AbilityTestFactory.CreateCatalog((AbilityDefinition)null);

        Assert.That(_catalog.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("null definition"));
    }

    [Test]
    public void InvalidDefinition_FailsValidation()
    {
        _first = AbilityTestFactory.CreateDefinition("Invalid");
        _catalog = AbilityTestFactory.CreateCatalog(_first);

        Assert.That(_catalog.TryValidate(out string error), Is.False);
        Assert.That(error, Does.Contain("invalid definition"));
    }

    [Test]
    public void DefinitionOutsideCatalog_DoesNotResolveToIdentity()
    {
        _first = AbilityTestFactory.CreateDefinition("charge");
        _second = AbilityTestFactory.CreateDefinition("trap");
        _catalog = AbilityTestFactory.CreateCatalog(_first);

        Assert.That(_catalog.TryGetId(_second, out AbilityId abilityId), Is.False);
        Assert.That(abilityId.IsValid, Is.False);
    }
}

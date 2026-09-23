using System;
using NUnit.Framework;

public sealed class AbilityIdTests
{
    [TestCase("charge")]
    [TestCase("seismic_strike")]
    [TestCase("ability_2")]
    public void TryCreate_ValidStableId_Succeeds(string value)
    {
        bool created = AbilityId.TryCreate(value, out AbilityId abilityId);

        Assert.That(created, Is.True);
        Assert.That(abilityId.Value, Is.EqualTo(value));
        Assert.That(abilityId.IsValid, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Charge")]
    [TestCase("seismic-strike")]
    [TestCase("drenaje vital")]
    public void TryCreate_InvalidStableId_Fails(string value)
    {
        Assert.That(AbilityId.TryCreate(value, out AbilityId abilityId), Is.False);
        Assert.That(abilityId.IsValid, Is.False);
    }

    [Test]
    public void Constructor_InvalidStableId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AbilityId("Invalid"));
    }

    [Test]
    public void TryCreate_EnforcesNetworkSnapshotLengthLimit()
    {
        string maximumLengthId = new string('a', AbilityId.MaximumLength);
        string oversizedId = maximumLengthId + "a";

        Assert.That(AbilityId.TryCreate(maximumLengthId, out AbilityId valid), Is.True);
        Assert.That(valid.Value, Has.Length.EqualTo(AbilityId.MaximumLength));
        Assert.That(AbilityId.TryCreate(oversizedId, out _), Is.False);
        Assert.Throws<ArgumentException>(() => new AbilityId(oversizedId));
    }

    [Test]
    public void Equality_UsesStableOrdinalValue()
    {
        var left = new AbilityId("arcane_projectile");
        var right = new AbilityId(string.Concat("arcane", "_projectile"));

        Assert.That(left, Is.EqualTo(right));
        Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
    }
}

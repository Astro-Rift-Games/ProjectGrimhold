using System;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidStartRejectionTextTests
{
    [Test]
    public void PartyUnavailable_HasAMessageThatTellsTheHostToRetry()
    {
        Assert.That(TownRaidStartRejectionText.TryGetMessage(TownRaidStartResult.PartyUnavailable, out string message), Is.True);
        Assert.That(message, Is.Not.Empty);
        StringAssert.Contains("party", message);
        StringAssert.Contains("intentar", message);
    }

    [TestCase(TownRaidStartResult.Started)]
    [TestCase(TownRaidStartResult.Rejected)]
    public void ResultsWithoutNotice_ProduceNoMessage(TownRaidStartResult result)
    {
        Assert.That(TownRaidStartRejectionText.TryGetMessage(result, out string message), Is.False);
        Assert.That(message, Is.Null);
    }

    [Test]
    public void EveryDefinedResult_MapsDeterministically()
    {
        foreach (TownRaidStartResult result in Enum.GetValues(typeof(TownRaidStartResult)))
        {
            bool first = TownRaidStartRejectionText.TryGetMessage(result, out string a);
            bool second = TownRaidStartRejectionText.TryGetMessage(result, out string b);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(b, Is.EqualTo(a));
        }
    }

    [Test]
    public void UndefinedResult_ProducesNoMessage()
    {
        Assert.That(TownRaidStartRejectionText.TryGetMessage((TownRaidStartResult)99, out string message), Is.False);
        Assert.That(message, Is.Null);
    }
}

using NUnit.Framework;

public sealed class RaidClosureStateTests
{
    [Test]
    public void ClosureStatesPreserveTheAuthoritativeOrder()
    {
        Assert.That((byte)RaidClosureState.AwaitingPersistence, Is.LessThan((byte)RaidClosureState.Cleaning));
        Assert.That((byte)RaidClosureState.Cleaning, Is.LessThan((byte)RaidClosureState.ResultsRetained));
        Assert.That((byte)RaidClosureState.ResultsRetained, Is.LessThan((byte)RaidClosureState.Finished));
    }

    [Test]
    public void ClosureReasonsRemainDistinct()
    {
        Assert.That(RaidClosureReason.NaturalCompletion, Is.Not.EqualTo(RaidClosureReason.HostCancellation));
    }
}

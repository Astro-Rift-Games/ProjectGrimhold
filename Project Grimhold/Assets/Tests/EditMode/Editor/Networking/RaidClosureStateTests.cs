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
    public void ClosureReasonsKeepTheirNetworkedByteValues()
    {
        // [Networked] byte: value 1 (removed Host cancellation) stays reserved, never renumbered.
        Assert.That((byte)RaidClosureReason.NaturalCompletion, Is.EqualTo(0));
        Assert.That((byte)RaidClosureReason.BootstrapFailure, Is.EqualTo(2));
    }
}

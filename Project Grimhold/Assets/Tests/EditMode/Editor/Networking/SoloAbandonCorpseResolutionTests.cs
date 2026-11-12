using NUnit.Framework;

public sealed class SoloAbandonCorpseResolutionTests
{
    private const ExpeditionProgressionFinalizationCause Abandon =
        ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed;

    [Test]
    public void CorpseConverted_ProceedsToAbortedAndKeepsTheAbandonUntouched()
    {
        SoloAbandonCorpseResolution resolution =
            SoloAbandonCorpseResolution.Resolve(true, Abandon, failureSequence: 4);

        Assert.That(resolution.ProceedsToAborted, Is.True);
        Assert.That(resolution.FinalizationCause, Is.EqualTo(Abandon));
        Assert.That(resolution.FailureSequence, Is.EqualTo(4));
    }

    [Test]
    public void CorpseFailure_KeepsTheParticipantRaidingAndClearsThePendingAbandon()
    {
        SoloAbandonCorpseResolution resolution =
            SoloAbandonCorpseResolution.Resolve(false, Abandon, failureSequence: 0);

        Assert.That(resolution.ProceedsToAborted, Is.False);
        // A cleared cause is what lets the next Abandon press start a fresh attempt.
        Assert.That(
            resolution.FinalizationCause,
            Is.EqualTo(ExpeditionProgressionFinalizationCause.None));
    }

    [Test]
    public void CorpseFailure_AdvancesTheFailureSequenceSoThePlayerIsNotified()
    {
        SoloAbandonCorpseResolution first =
            SoloAbandonCorpseResolution.Resolve(false, Abandon, failureSequence: 0);
        SoloAbandonCorpseResolution second =
            SoloAbandonCorpseResolution.Resolve(false, Abandon, first.FailureSequence);

        Assert.That(first.FailureSequence, Is.EqualTo(1));
        Assert.That(second.FailureSequence, Is.EqualTo(2));
    }

    [Test]
    public void CorpseFailure_AtTheCounterLimit_StillProducesADistinctSequence()
    {
        SoloAbandonCorpseResolution resolution =
            SoloAbandonCorpseResolution.Resolve(false, Abandon, int.MaxValue);

        Assert.That(resolution.FailureSequence, Is.Not.EqualTo(int.MaxValue));
    }
}

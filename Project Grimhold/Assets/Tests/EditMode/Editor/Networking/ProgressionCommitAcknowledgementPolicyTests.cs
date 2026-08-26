#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK143")]
public sealed class ProgressionCommitAcknowledgementPolicyTests
{
    [TestCase(ProgressionCommitResult.Success, true, false)]
    [TestCase(ProgressionCommitResult.AlreadyApplied, true, false)]
    [TestCase(ProgressionCommitResult.PersistenceFailed, false, true)]
    [TestCase(ProgressionCommitResult.Stale, false, false)]
    [TestCase(ProgressionCommitResult.Conflict, false, false)]
    [TestCase(ProgressionCommitResult.Invalid, false, false)]
    public void CommitResult_HasExactAckAndRetryPolicy(
        ProgressionCommitResult result,
        bool expectedAck,
        bool expectedRetry)
    {
        Assert.That(
            NetworkRaidParticipant.ShouldAcknowledgeProgressionCommit(result),
            Is.EqualTo(expectedAck));
        Assert.That(
            NetworkRaidParticipant.ShouldRetryProgressionCommit(result),
            Is.EqualTo(expectedRetry));
    }
}
#endif

using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidSessionRulesTests
{
    [Test]
    public void MaxParticipants_IsSixteen()
    {
        Assert.That(RaidSessionRules.MaxParticipants, Is.EqualTo(16));
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(15)]
    [TestCase(16)]
    public void ParticipantCohort_AcceptsSupportedCounts(int count)
    {
        ProfileId[] profiles = CreateProfiles(count);

        Assert.That(RaidSessionRules.IsValidParticipantCohort(profiles[0], profiles), Is.True);
    }

    [Test]
    public void ParticipantCohort_RejectsSeventeen()
    {
        ProfileId[] profiles = CreateProfiles(17);

        Assert.That(RaidSessionRules.IsValidParticipantCohort(profiles[0], profiles), Is.False);
    }

    [Test]
    public void ParticipantCohort_RejectsInvalidDuplicateAndMissingHostProfiles()
    {
        var host = new ProfileId("host");

        Assert.That(RaidSessionRules.IsValidParticipantCohort(host, new[] { host, default(ProfileId) }), Is.False);
        Assert.That(RaidSessionRules.IsValidParticipantCohort(host, new[] { host, host }), Is.False);
        Assert.That(
            RaidSessionRules.IsValidParticipantCohort(host, new[] { new ProfileId("client") }),
            Is.False);
    }

    private static ProfileId[] CreateProfiles(int count)
    {
        var profiles = new ProfileId[count];
        for (int index = 0; index < count; index++)
        {
            profiles[index] = new ProfileId($"profile-{index}");
        }

        return profiles;
    }
}

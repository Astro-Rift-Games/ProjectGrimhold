using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyPresentationTests
{
    [Test]
    public void Solo_ShowsLocalNameAndHidesCompanionAction()
    {
        var local = new ProfileId("local-profile");
        var party = new TownPartySnapshot(1, local, new[] { local }, 1);
        Assert.That(TownPartyPresentation.TryCreate(party, local, "Dani", null, out var presentation), Is.True);
        Assert.That(presentation.LocalDisplayName, Is.EqualTo("Dani"));
        Assert.That(presentation.HasCompanion, Is.False);
    }

    [Test]
    public void Duo_ShowsCompanionAndFallsBackToProfileId()
    {
        var local = new ProfileId("local-profile");
        var companion = new ProfileId("companion-profile");
        var party = new TownPartySnapshot(1, local, new[] { local, companion }, 2);
        Assert.That(TownPartyPresentation.TryCreate(party, local, null, null, out var presentation), Is.True);
        Assert.That(presentation.LocalDisplayName, Is.EqualTo(local.Value));
        Assert.That(presentation.CompanionDisplayName, Is.EqualTo(companion.Value));
        Assert.That(presentation.HasCompanion, Is.True);
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidAdmissionRulesTests
{
    [Test]
    public void Admission_AcceptsFrozenProfilesAndRejectsOutsiderAndWrongCode()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        RaidCode.TryParse("654321", out RaidCode wrongCode);
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        RaidLaunchContext.TryCreate(code, host, new[] { host, client }, host, 1, out RaidLaunchContext context);

        var valid = new RaidAdmissionData(code, client, "reservation", new List<LootEntry>());
        var outsider = new RaidAdmissionData(code, new ProfileId("outsider"), "reservation", new List<LootEntry>());
        var wrong = new RaidAdmissionData(wrongCode, client, "reservation", new List<LootEntry>());

        Assert.That(RaidAdmissionRules.IsAdmitted(context, valid), Is.True);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, outsider), Is.False);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, wrong), Is.False);
    }
}

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

        var loadout = new[] { new LootEntry(new LootId("training_sword"), 1) };
        var valid = new RaidAdmissionData(code, client, "reservation", loadout, new[] { 1, 0, 0, 0, 0, 0 });
        var outsider = new RaidAdmissionData(code, new ProfileId("outsider"), "reservation", loadout, new[] { 1, 0, 0, 0, 0, 0 });
        var wrong = new RaidAdmissionData(wrongCode, client, "reservation", loadout, new[] { 1, 0, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionRules.IsAdmitted(context, valid), Is.True);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, outsider), Is.False);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, wrong), Is.False);
    }
}

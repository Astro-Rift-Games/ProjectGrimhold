using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

[Category("TASK180")]
public sealed class RaidAdmissionRulesTests
{
    [Test]
    public void Admission_AcceptsFrozenProfilesAndRejectsOutsiderAndWrongCode()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        RaidCode.TryParse("654321", out RaidCode wrongCode);
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        RaidLaunchContext.TryCreate(
            code,
            host,
            new[]
            {
                new RaidLaunchParticipant(host, teamId),
                new RaidLaunchParticipant(client, teamId)
            },
            host,
            1,
            out RaidLaunchContext context);

        var loadout = new[] { new LootEntry(new LootId("arming_sword"), 1) };
        CharacterAttributeState attributes =
            ProgressionBalanceDefaults.InitialCharacterAttributeState;
        var valid = new RaidAdmissionData(
            code, client, "reservation", loadout, attributes, new[] { 1, 0, 0, 0, 0, 0 });
        var outsider = new RaidAdmissionData(
            code, new ProfileId("outsider"), "reservation", loadout, attributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        var wrong = new RaidAdmissionData(
            wrongCode, client, "reservation", loadout, attributes,
            new[] { 1, 0, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionRules.IsAdmitted(context, valid), Is.True);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, outsider), Is.False);
        Assert.That(RaidAdmissionRules.IsAdmitted(context, wrong), Is.False);
    }

    [Test]
    public void PreparedAbilities_ValidateCatalogAndAdmittedAttributeRequirements()
    {
        AbilityDefinition charge = AbilityTestFactory.CreateDefinition(
            "charge",
            requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        AbilityDefinition trap = AbilityTestFactory.CreateDefinition("trap");
        AbilityDefinitionCatalog catalog = AbilityTestFactory.CreateCatalog(charge, trap);
        try
        {
            CharacterAttributeState validAttributes =
                AbilityTestFactory.CreateAttributes(strength: 10);
            var valid = new PreparedAbilityLoadout(
                new AbilityId("charge"),
                new AbilityId("trap"));
            var unknown = new PreparedAbilityLoadout(new AbilityId("missing"), default);
            CharacterAttributeState insufficientAttributes =
                AbilityTestFactory.CreateAttributes(strength: 9);

            Assert.That(
                PreparedAbilityLoadout.TryValidateAdmission(
                    valid,
                    validAttributes,
                    catalog,
                    out _),
                Is.True);
            Assert.That(
                PreparedAbilityLoadout.TryValidateAdmission(
                    unknown,
                    validAttributes,
                    catalog,
                    out string unknownError),
                Is.False);
            Assert.That(unknownError, Does.Contain("unknown"));
            Assert.That(
                PreparedAbilityLoadout.TryValidateAdmission(
                    valid,
                    insufficientAttributes,
                    catalog,
                    out string requirementError),
                Is.False);
            Assert.That(requirementError, Does.Contain("requirements"));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(charge);
            Object.DestroyImmediate(trap);
        }
    }
}

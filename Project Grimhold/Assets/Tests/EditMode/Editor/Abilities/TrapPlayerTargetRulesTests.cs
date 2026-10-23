using NUnit.Framework;
using UnityEngine;

public sealed class TrapPlayerTargetRulesTests
{
    [TestCase(false, true, true, false, false, true)]
    [TestCase(true, true, true, false, false, false)]
    [TestCase(false, false, true, false, false, false)]
    [TestCase(false, true, false, false, false, false)]
    [TestCase(false, true, false, true, false, false)]
    [TestCase(false, true, true, true, false, false)]
    [TestCase(false, true, true, false, true, false)]
    public void Eligibility_RequiresConfirmedLivingUnprotectedRival(bool self, bool canReceive,
        bool known, bool teammate, bool protectedState, bool expected)
    {
        Assert.That(TrapPlayerTargetRules.IsEligible(self, canReceive, known, teammate, protectedState),
            Is.EqualTo(expected));
    }

    [Test]
    public void UnspawnedEffect_ReportsInactiveAndRefusesMutationWithoutReadingNetworkState()
    {
        var owner = new GameObject("UnspawnedImmobilizationTest");
        try
        {
            var effect = owner.AddComponent<PlayerImmobilizationNetworkEffect>();
            Assert.That(effect.IsActive, Is.False);
            Assert.That(effect.IsReadyToApply, Is.False);
            Assert.That(effect.TryApply(3f), Is.False);
            Assert.That(effect.TryPurify(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}

using NUnit.Framework;

public sealed class ChargeRulesTests
{
    private static ChargeHitCandidate Candidate(int id, float distance) =>
        new ChargeHitCandidate(new EntityId(id), distance, id);

    // ---- Duration ------------------------------------------------------------------------

    [Test]
    public void TryComputeDuration_ValidDistanceAndSpeed_ReturnsDistanceOverSpeed()
    {
        Assert.That(ChargeRules.TryComputeDuration(4f, 16f, out float seconds), Is.True);
        Assert.That(seconds, Is.EqualTo(0.25f).Within(1e-6f));
    }

    [TestCase(0f, 10f)]
    [TestCase(-1f, 10f)]
    [TestCase(4f, 0f)]
    [TestCase(4f, -3f)]
    [TestCase(float.NaN, 10f)]
    [TestCase(4f, float.NaN)]
    [TestCase(float.PositiveInfinity, 10f)]
    [TestCase(4f, float.PositiveInfinity)]
    public void TryComputeDuration_InvalidInput_IsRejected(float distance, float speed)
    {
        Assert.That(ChargeRules.TryComputeDuration(distance, speed, out float seconds), Is.False);
        Assert.That(seconds, Is.Zero);
    }

    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(ChargeRules.TryValidateConfiguration(4f, 18f, 0f, 0f, 1 << 7, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_ZeroDamageAndZeroKnockback_AreAllowed()
    {
        Assert.That(ChargeRules.TryValidateConfiguration(4f, 18f, 0f, 0f, 1, out _), Is.True);
    }

    [TestCase(0f, 18f, 0f, 0f, 1)]
    [TestCase(4f, 0f, 0f, 0f, 1)]
    [TestCase(4f, 18f, -1f, 0f, 1)]
    [TestCase(4f, 18f, float.NaN, 0f, 1)]
    [TestCase(4f, 18f, 0f, -1f, 1)]
    [TestCase(4f, 18f, 0f, float.PositiveInfinity, 1)]
    [TestCase(4f, 18f, 0f, 0f, 0)]
    public void TryValidateConfiguration_InvalidValues_ReportAnError(
        float distance, float speed, float damage, float knockback, int mask)
    {
        Assert.That(ChargeRules.TryValidateConfiguration(distance, speed, damage, knockback, mask, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    // ---- First hit -----------------------------------------------------------------------

    [Test]
    public void SelectFirstHit_NoCandidates_ReturnsMinusOne()
    {
        Assert.That(ChargeRules.SelectFirstHit(new ChargeHitCandidate[4], 0), Is.EqualTo(-1));
        Assert.That(ChargeRules.SelectFirstHit(null, 0), Is.EqualTo(-1));
    }

    [Test]
    public void SelectFirstHit_PicksTheSmallestDistance()
    {
        var candidates = new[] { Candidate(5, 2f), Candidate(2, 0.5f), Candidate(9, 1f) };
        Assert.That(ChargeRules.SelectFirstHit(candidates, candidates.Length), Is.EqualTo(1));
    }

    [Test]
    public void SelectFirstHit_TiedDistance_PicksTheSmallestEntityId()
    {
        var candidates = new[] { Candidate(8, 1f), Candidate(3, 1f), Candidate(6, 1f) };
        Assert.That(ChargeRules.SelectFirstHit(candidates, candidates.Length), Is.EqualTo(1));
    }

    [Test]
    public void SelectFirstHit_IsIndependentOfCandidateOrder()
    {
        var forward = new[] { Candidate(1, 1f), Candidate(2, 1f), Candidate(3, 0.2f) };
        var reversed = new[] { forward[2], forward[1], forward[0] };
        Assert.That(forward[ChargeRules.SelectFirstHit(forward, 3)].Id,
            Is.EqualTo(reversed[ChargeRules.SelectFirstHit(reversed, 3)].Id));
    }

    [Test]
    public void SelectFirstHit_IgnoresStaleEntriesBeyondCount()
    {
        var candidates = new[] { Candidate(4, 3f), Candidate(1, 0f) };
        Assert.That(ChargeRules.SelectFirstHit(candidates, 1), Is.EqualTo(0));
    }

    // ---- Completion decision -------------------------------------------------------------

    [Test]
    public void Decide_NothingHappened_Continues()
    {
        Assert.That(ChargeRules.Decide(true, false, false, false), Is.EqualTo(ChargeOutcome.Continue));
    }

    [Test]
    public void Decide_PhaseDeadlineExpired_Completes()
    {
        Assert.That(ChargeRules.Decide(true, false, false, true), Is.EqualTo(ChargeOutcome.Expired));
    }

    [Test]
    public void Decide_EnvironmentBlocked_Completes()
    {
        Assert.That(ChargeRules.Decide(true, false, true, false), Is.EqualTo(ChargeOutcome.Blocked));
    }

    [Test]
    public void Decide_EnemyHit_Completes()
    {
        Assert.That(ChargeRules.Decide(true, true, false, false), Is.EqualTo(ChargeOutcome.EnemyHit));
    }

    [Test]
    public void Decide_DisplacementNoLongerActive_WinsOverEverythingElse()
    {
        Assert.That(ChargeRules.Decide(false, true, true, true), Is.EqualTo(ChargeOutcome.DisplacementEnded));
    }

    [Test]
    public void Decide_EnemyHitAndBlockedSameStep_ResolvesTheEnemy()
    {
        Assert.That(ChargeRules.Decide(true, true, true, true), Is.EqualTo(ChargeOutcome.EnemyHit));
    }

    [Test]
    public void Decide_BlockedAndExpiredSameStep_ReportsBlocked()
    {
        Assert.That(ChargeRules.Decide(true, false, true, true), Is.EqualTo(ChargeOutcome.Blocked));
    }

    // ---- Damage pipeline routing ---------------------------------------------------------

    [Test]
    public void UsesDamagePipeline_OnlyForPositiveDamage()
    {
        Assert.That(ChargeRules.UsesDamagePipeline(10f), Is.True);
        Assert.That(ChargeRules.UsesDamagePipeline(0f), Is.False);
    }
}

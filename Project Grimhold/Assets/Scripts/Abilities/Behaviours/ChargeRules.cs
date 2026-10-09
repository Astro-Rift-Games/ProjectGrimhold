/// <summary>One enemy found on the path travelled by the charge during a simulation step.</summary>
internal readonly struct ChargeHitCandidate
{
    public EntityId Id { get; }
    /// <summary>Distance along the swept path at which the cast first touched the enemy.</summary>
    public float Distance { get; }
    /// <summary>Position of the cast hit this candidate was built from, in the owner's hit buffer.</summary>
    public int HitIndex { get; }

    public ChargeHitCandidate(EntityId id, float distance, int hitIndex)
    {
        Id = id;
        Distance = distance;
        HitIndex = hitIndex;
    }
}

/// <summary>Why a charge step ends (or does not end) the execution.</summary>
internal enum ChargeOutcome
{
    Continue,
    DisplacementEnded,
    EnemyHit,
    Blocked,
    Expired
}

/// <summary>Pure, deterministic rules of the charge. No Unity or Fusion state is read here.</summary>
internal static class ChargeRules
{
    /// <summary>Seconds needed to cover <paramref name="distance"/> at <paramref name="speed"/>.</summary>
    public static bool TryComputeDuration(float distance, float speed, out float seconds)
    {
        seconds = 0f;
        if (!IsPositiveFinite(distance) || !IsPositiveFinite(speed)) return false;
        seconds = distance / speed;
        return IsPositiveFinite(seconds);
    }

    /// <summary>Damage and knockback may be zero; distance, speed and the target layers may not.</summary>
    public static bool TryValidateConfiguration(float distance, float speed, float damage, float knockbackForce,
        int targetLayerMask, out string error)
    {
        if (!TryComputeDuration(distance, speed, out _))
            error = "Charge distance and speed must be finite and greater than zero.";
        else if (!IsNonNegativeFinite(damage))
            error = "Charge damage must be finite and not negative.";
        else if (!IsNonNegativeFinite(knockbackForce))
            error = "Charge knockback force must be finite and not negative.";
        else if (targetLayerMask == 0)
            error = "Charge requires at least one target layer.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Index of the first enemy along the path: smallest distance, then smallest EntityId.
    /// Returns -1 when there is none. Only the first <paramref name="count"/> entries are read.
    /// </summary>
    public static int SelectFirstHit(ChargeHitCandidate[] candidates, int count)
    {
        if (candidates == null) return -1;
        int best = -1;
        int limit = count < candidates.Length ? count : candidates.Length;
        for (int i = 0; i < limit; i++)
        {
            if (best < 0 || IsEarlier(candidates[i], candidates[best])) best = i;
        }
        return best;
    }

    /// <summary>
    /// A displacement that is no longer active (Downed, death, external end) wins over everything, so a
    /// stopped charge never resolves a hit. A resolved enemy is reported even when the wall or the deadline
    /// also ended the step, because the enemy was reached before them.
    /// </summary>
    public static ChargeOutcome Decide(bool displacementActive, bool enemyHit, bool environmentBlocked, bool phaseExpired)
    {
        if (!displacementActive) return ChargeOutcome.DisplacementEnded;
        if (enemyHit) return ChargeOutcome.EnemyHit;
        if (environmentBlocked) return ChargeOutcome.Blocked;
        return phaseExpired ? ChargeOutcome.Expired : ChargeOutcome.Continue;
    }

    private static bool IsEarlier(in ChargeHitCandidate candidate, in ChargeHitCandidate current)
    {
        if (candidate.Distance != current.Distance) return candidate.Distance < current.Distance;
        return candidate.Id.Value < current.Id.Value;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}

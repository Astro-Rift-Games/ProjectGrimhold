using UnityEngine;

/// <summary>Original knockback PE and eligibility captured before the impact changes the target's state.</summary>
public readonly struct KnockbackRequest
{
    /// <summary>Normalized push direction; positive direction preserves the movement motor convention.</summary>
    public Vector2 Direction { get; }

    /// <summary>Nonnegative original PE, before RE mitigation; non-finite input becomes zero.</summary>
    public float OriginalPower { get; }

    /// <summary>Whether the target was eligible before this impact applied damage.</summary>
    public bool WasEligibleAtImpact { get; }

    /// <summary>Captures an impact without rechecking eligibility after damage.</summary>
    public KnockbackRequest(Vector2 direction, float originalPower, bool wasEligibleAtImpact)
    {
        Direction = PlayerAimMath.TryNormalizeDirection(direction, out Vector2 normalized)
            ? normalized : Vector2.zero;
        OriginalPower = float.IsNaN(originalPower) || float.IsInfinity(originalPower)
            ? 0f : Mathf.Max(0f, originalPower);
        WasEligibleAtImpact = wasEligibleAtImpact;
    }
}

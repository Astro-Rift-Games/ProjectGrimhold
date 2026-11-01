/// <summary>
/// Read-only Downed facts the local post-process observes. Implemented over the replicated
/// Downed state and recovery session; also the deterministic seam for presenter tests.
/// </summary>
public interface IDownedPostProcessSource
{
    /// <summary>False once the observed avatar is gone; the presenter then drops the effect.</summary>
    bool IsAvailable { get; }

    bool IsDowned { get; }

    /// <summary>Downed reserve normalized to [0, 1].</summary>
    float ReserveFraction { get; }

    /// <summary>True while an assisted or self revive session runs, with its progress in [0, 1].</summary>
    bool TryGetReviveProgress(out float progress);
}

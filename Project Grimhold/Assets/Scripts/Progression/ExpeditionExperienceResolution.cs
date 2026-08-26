using System;

/// <summary>Immutable one-shot resolution of provisional expedition experience.</summary>
public readonly struct ExpeditionExperienceResolution : IEquatable<ExpeditionExperienceResolution>
{
    public bool IsResolved { get; }
    public ExpeditionExperienceResolutionOutcome Outcome { get; }
    public ExpeditionExperienceSnapshot ProvisionalExperience { get; }
    public int RetentionBasisPoints { get; }
    public long ConsolidatedExperience { get; }

    public long ProvisionalExperienceTotal => ProvisionalExperience.TotalExperience;

    internal ExpeditionExperienceResolution(
        ExpeditionExperienceResolutionOutcome outcome,
        in ExpeditionExperienceSnapshot provisionalExperience,
        int retentionBasisPoints,
        long consolidatedExperience)
    {
        IsResolved = true;
        Outcome = outcome;
        ProvisionalExperience = provisionalExperience;
        RetentionBasisPoints = retentionBasisPoints;
        ConsolidatedExperience = consolidatedExperience;
    }

    public bool Equals(ExpeditionExperienceResolution other) =>
        IsResolved == other.IsResolved &&
        Outcome == other.Outcome &&
        ProvisionalExperience.Equals(other.ProvisionalExperience) &&
        RetentionBasisPoints == other.RetentionBasisPoints &&
        ConsolidatedExperience == other.ConsolidatedExperience;

    public override bool Equals(object obj) =>
        obj is ExpeditionExperienceResolution other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsResolved.GetHashCode();
            hash = (hash * 397) ^ Outcome.GetHashCode();
            hash = (hash * 397) ^ ProvisionalExperience.GetHashCode();
            hash = (hash * 397) ^ RetentionBasisPoints;
            return (hash * 397) ^ ConsolidatedExperience.GetHashCode();
        }
    }

    public static bool operator ==(
        ExpeditionExperienceResolution left,
        ExpeditionExperienceResolution right) => left.Equals(right);

    public static bool operator !=(
        ExpeditionExperienceResolution left,
        ExpeditionExperienceResolution right) => !left.Equals(right);
}

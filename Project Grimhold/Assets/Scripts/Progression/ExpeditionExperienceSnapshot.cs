using System;

/// <summary>Immutable provisional experience breakdown for one raid participation.</summary>
public readonly struct ExpeditionExperienceSnapshot : IEquatable<ExpeditionExperienceSnapshot>
{
    public static ExpeditionExperienceSnapshot Empty => default;

    public long KillExperience { get; }
    public long AssistExperience { get; }
    public long ExplorationExperience { get; }
    public long ExtractedLootExperience { get; }

    /// <summary>Total provisional experience derived from the category accumulators.</summary>
    public long TotalExperience => checked(
        KillExperience +
        AssistExperience +
        ExplorationExperience +
        ExtractedLootExperience);

    internal ExpeditionExperienceSnapshot(
        long killExperience,
        long assistExperience,
        long explorationExperience,
        long extractedLootExperience)
    {
        KillExperience = killExperience;
        AssistExperience = assistExperience;
        ExplorationExperience = explorationExperience;
        ExtractedLootExperience = extractedLootExperience;
    }

    public bool Equals(ExpeditionExperienceSnapshot other) =>
        KillExperience == other.KillExperience &&
        AssistExperience == other.AssistExperience &&
        ExplorationExperience == other.ExplorationExperience &&
        ExtractedLootExperience == other.ExtractedLootExperience;

    public override bool Equals(object obj) =>
        obj is ExpeditionExperienceSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = KillExperience.GetHashCode();
            hash = (hash * 397) ^ AssistExperience.GetHashCode();
            hash = (hash * 397) ^ ExplorationExperience.GetHashCode();
            return (hash * 397) ^ ExtractedLootExperience.GetHashCode();
        }
    }

    public static bool operator ==(
        ExpeditionExperienceSnapshot left,
        ExpeditionExperienceSnapshot right) => left.Equals(right);

    public static bool operator !=(
        ExpeditionExperienceSnapshot left,
        ExpeditionExperienceSnapshot right) => !left.Equals(right);
}

using System;

/// <summary>Outcome of a Town ability mutation, with the persistence result when one was produced.</summary>
public readonly struct TownAbilityMutationResult : IEquatable<TownAbilityMutationResult>
{
    public TownAbilityMutationOutcome Outcome { get; }

    /// <summary>
    /// The underlying preparation result. Meaningful for Success and Rejected; a blocked attempt
    /// never reaches the store and carries <see cref="AbilityPreparationResult.Success"/> only as
    /// the default placeholder, so always read <see cref="Outcome"/> first.
    /// </summary>
    public AbilityPreparationResult Preparation { get; }

    public bool IsSuccess => Outcome == TownAbilityMutationOutcome.Success;

    private TownAbilityMutationResult(TownAbilityMutationOutcome outcome, AbilityPreparationResult preparation)
    {
        Outcome = outcome;
        Preparation = preparation;
    }

    public static TownAbilityMutationResult Blocked() =>
        new(TownAbilityMutationOutcome.BlockedByReadyState, default);

    public static TownAbilityMutationResult From(AbilityPreparationResult preparation) =>
        new(
            preparation == AbilityPreparationResult.Success
                ? TownAbilityMutationOutcome.Success
                : TownAbilityMutationOutcome.Rejected,
            preparation);

    public bool Equals(TownAbilityMutationResult other) =>
        Outcome == other.Outcome && Preparation == other.Preparation;

    public override bool Equals(object obj) => obj is TownAbilityMutationResult other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Outcome, Preparation);
}

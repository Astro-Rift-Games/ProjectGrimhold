using System;

/// <summary>
/// Initial playtest balance data for persistent character progression.
/// </summary>
public static class ProgressionBalanceDefaults
{
    public const int InitialAttributeValue = 5;
    public const int InitialAvailableAttributePoints = 10;

    public static CharacterAttributeState InitialCharacterAttributeState { get; } =
        CreateInitialCharacterAttributeState();

    public static ExperienceCurve InitialExperienceCurve { get; } = CreateInitialExperienceCurve();

    public static ExpeditionExperienceRetentionPolicy InitialExpeditionExperienceRetentionPolicy { get; } =
        CreateInitialExpeditionExperienceRetentionPolicy();

    private static CharacterAttributeState CreateInitialCharacterAttributeState()
    {
        if (!CharacterAttributeState.TryCreate(
                InitialAttributeValue,
                InitialAttributeValue,
                InitialAttributeValue,
                InitialAttributeValue,
                InitialAttributeValue,
                InitialAttributeValue,
                InitialAvailableAttributePoints,
                out CharacterAttributeState state))
        {
            throw new InvalidOperationException("Initial character attribute balance is invalid.");
        }

        return state;
    }

    private static ExperienceCurve CreateInitialExperienceCurve()
    {
        long[] requirements =
        {
            100, 105, 110, 115, 120, 126, 132, 138, 144, 151,
            158, 165, 173, 181, 190, 199, 208, 218, 228, 239,
            250, 262, 275, 288, 302, 317, 332, 348, 365
        };

        if (!ExperienceCurve.TryCreate(requirements, out ExperienceCurve curve))
        {
            throw new InvalidOperationException("Initial progression balance is invalid.");
        }

        return curve;
    }

    private static ExpeditionExperienceRetentionPolicy CreateInitialExpeditionExperienceRetentionPolicy()
    {
        if (!ExpeditionExperienceRetentionPolicy.TryCreate(
                extractedBasisPoints: 10_000,
                defeatedBasisPoints: 2_000,
                abandonedBasisPoints: 0,
                definitivelyDisconnectedBasisPoints: 0,
                out ExpeditionExperienceRetentionPolicy policy))
        {
            throw new InvalidOperationException("Initial expedition experience retention policy is invalid.");
        }

        return policy;
    }
}

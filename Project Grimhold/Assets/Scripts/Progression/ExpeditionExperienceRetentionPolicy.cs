/// <summary>Immutable retention percentages for definitive expedition outcomes.</summary>
public sealed class ExpeditionExperienceRetentionPolicy
{
    public int ExtractedBasisPoints { get; }
    public int DefeatedBasisPoints { get; }
    public int AbandonedBasisPoints { get; }
    public int DefinitivelyDisconnectedBasisPoints { get; }

    private ExpeditionExperienceRetentionPolicy(
        int extractedBasisPoints,
        int defeatedBasisPoints,
        int abandonedBasisPoints,
        int definitivelyDisconnectedBasisPoints)
    {
        ExtractedBasisPoints = extractedBasisPoints;
        DefeatedBasisPoints = defeatedBasisPoints;
        AbandonedBasisPoints = abandonedBasisPoints;
        DefinitivelyDisconnectedBasisPoints = definitivelyDisconnectedBasisPoints;
    }

    public static bool TryCreate(
        int extractedBasisPoints,
        int defeatedBasisPoints,
        int abandonedBasisPoints,
        int definitivelyDisconnectedBasisPoints,
        out ExpeditionExperienceRetentionPolicy policy)
    {
        policy = null;
        if (!IsValidPercentage(extractedBasisPoints) ||
            !IsValidPercentage(defeatedBasisPoints) ||
            !IsValidPercentage(abandonedBasisPoints) ||
            !IsValidPercentage(definitivelyDisconnectedBasisPoints))
        {
            return false;
        }

        policy = new ExpeditionExperienceRetentionPolicy(
            extractedBasisPoints,
            defeatedBasisPoints,
            abandonedBasisPoints,
            definitivelyDisconnectedBasisPoints);
        return true;
    }

    internal int GetRetentionBasisPoints(ExpeditionExperienceResolutionOutcome outcome) => outcome switch
    {
        ExpeditionExperienceResolutionOutcome.Extracted => ExtractedBasisPoints,
        ExpeditionExperienceResolutionOutcome.Defeated => DefeatedBasisPoints,
        ExpeditionExperienceResolutionOutcome.Abandoned => AbandonedBasisPoints,
        _ => DefinitivelyDisconnectedBasisPoints
    };

    private static bool IsValidPercentage(int basisPoints) =>
        basisPoints >= 0 &&
        basisPoints <= ExtractedLootExperienceCalculator.BasisPointsDenominator;
}

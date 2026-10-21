/// <summary>Immutable retention percentages for definitive expedition outcomes.</summary>
public sealed class ExpeditionExperienceRetentionPolicy
{
    public int ExtractedBasisPoints { get; }
    public int DefeatedBasisPoints { get; }
    public int AbandonedBasisPoints { get; }
    public int DefinitivelyDisconnectedBasisPoints { get; }
    public int DefeatedWhileDisconnectedBasisPoints { get; }

    private ExpeditionExperienceRetentionPolicy(
        int extractedBasisPoints,
        int defeatedBasisPoints,
        int abandonedBasisPoints,
        int definitivelyDisconnectedBasisPoints,
        int defeatedWhileDisconnectedBasisPoints)
    {
        ExtractedBasisPoints = extractedBasisPoints;
        DefeatedBasisPoints = defeatedBasisPoints;
        AbandonedBasisPoints = abandonedBasisPoints;
        DefinitivelyDisconnectedBasisPoints = definitivelyDisconnectedBasisPoints;
        DefeatedWhileDisconnectedBasisPoints = defeatedWhileDisconnectedBasisPoints;
    }

    public static bool TryCreate(
        int extractedBasisPoints,
        int defeatedBasisPoints,
        int abandonedBasisPoints,
        int definitivelyDisconnectedBasisPoints,
        out ExpeditionExperienceRetentionPolicy policy)
    {
        return TryCreate(
            extractedBasisPoints,
            defeatedBasisPoints,
            abandonedBasisPoints,
            definitivelyDisconnectedBasisPoints,
            defeatedWhileDisconnectedBasisPoints: 0,
            out policy);
    }

    public static bool TryCreate(
        int extractedBasisPoints,
        int defeatedBasisPoints,
        int abandonedBasisPoints,
        int definitivelyDisconnectedBasisPoints,
        int defeatedWhileDisconnectedBasisPoints,
        out ExpeditionExperienceRetentionPolicy policy)
    {
        policy = null;
        if (!IsValidPercentage(extractedBasisPoints) ||
            !IsValidPercentage(defeatedBasisPoints) ||
            !IsValidPercentage(abandonedBasisPoints) ||
            !IsValidPercentage(definitivelyDisconnectedBasisPoints) ||
            !IsValidPercentage(defeatedWhileDisconnectedBasisPoints))
        {
            return false;
        }

        policy = new ExpeditionExperienceRetentionPolicy(
            extractedBasisPoints,
            defeatedBasisPoints,
            abandonedBasisPoints,
            definitivelyDisconnectedBasisPoints,
            defeatedWhileDisconnectedBasisPoints);
        return true;
    }

    internal int GetRetentionBasisPoints(ExpeditionExperienceResolutionOutcome outcome) => outcome switch
    {
        ExpeditionExperienceResolutionOutcome.Extracted => ExtractedBasisPoints,
        ExpeditionExperienceResolutionOutcome.Defeated => DefeatedBasisPoints,
        ExpeditionExperienceResolutionOutcome.Abandoned => AbandonedBasisPoints,
        ExpeditionExperienceResolutionOutcome.DefeatedWhileDisconnected => DefeatedWhileDisconnectedBasisPoints,
        _ => DefinitivelyDisconnectedBasisPoints
    };

    private static bool IsValidPercentage(int basisPoints) =>
        basisPoints >= 0 &&
        basisPoints <= ExtractedLootExperienceCalculator.BasisPointsDenominator;
}

/// <summary>Pure deterministic rules for resolving provisional expedition experience once.</summary>
public static class ExpeditionExperienceResolutionRules
{
    public static bool TryResolve(
        in ExpeditionExperienceResolution previous,
        in ExpeditionExperienceSnapshot snapshot,
        ExpeditionExperienceResolutionOutcome outcome,
        ExpeditionExperienceRetentionPolicy policy,
        out ExpeditionExperienceResolution candidate,
        out ExpeditionExperienceResolutionFailure failure)
    {
        candidate = previous;
        failure = ExpeditionExperienceResolutionFailure.None;

        if (previous.IsResolved)
        {
            failure = ExpeditionExperienceResolutionFailure.AlreadyResolved;
            return false;
        }

        if (policy == null)
        {
            failure = ExpeditionExperienceResolutionFailure.MissingPolicy;
            return false;
        }

        if (!IsKnownOutcome(outcome))
        {
            failure = ExpeditionExperienceResolutionFailure.InvalidOutcome;
            return false;
        }

        if (!ExpeditionExperienceRules.TryCalculateTotal(snapshot, out long totalExperience))
        {
            failure = ExpeditionExperienceResolutionFailure.InvalidSnapshot;
            return false;
        }

        int basisPoints = policy.GetRetentionBasisPoints(outcome);
        long quotient = totalExperience / ExtractedLootExperienceCalculator.BasisPointsDenominator;
        long remainder = totalExperience % ExtractedLootExperienceCalculator.BasisPointsDenominator;
        long consolidatedExperience =
            quotient * basisPoints +
            remainder * basisPoints / ExtractedLootExperienceCalculator.BasisPointsDenominator;

        candidate = new ExpeditionExperienceResolution(
            outcome,
            snapshot,
            basisPoints,
            consolidatedExperience);
        return true;
    }

    private static bool IsKnownOutcome(ExpeditionExperienceResolutionOutcome outcome) => outcome switch
    {
        ExpeditionExperienceResolutionOutcome.Extracted => true,
        ExpeditionExperienceResolutionOutcome.Defeated => true,
        ExpeditionExperienceResolutionOutcome.Abandoned => true,
        ExpeditionExperienceResolutionOutcome.DefinitivelyDisconnected => true,
        _ => false
    };
}

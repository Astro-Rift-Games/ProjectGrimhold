using UnityEngine;

/// <summary>
/// Static configuration boundary that prepares and applies extracted-Loot rewards.
/// It owns no authoritative or replicated runtime state.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExtractedLootExperienceProducer : MonoBehaviour
{
    public const int DefaultExperienceRateBasisPoints = 1_000;

    [SerializeField]
    private RaidLootValueCatalog _valueCatalog;

    [SerializeField, Range(1, ExtractedLootExperienceCalculator.BasisPointsDenominator)]
    private int _experienceRateBasisPoints = DefaultExperienceRateBasisPoints;

    public bool TryPrepare(
        int resultSequence,
        RaidParticipantId extractorId,
        PlayerExpeditionLootSnapshot lootSnapshot,
        RaidInitialAffiliationSnapshot affiliations,
        out ExtractedLootExperienceCandidate candidate,
        out string error)
    {
        candidate = default;
        error = null;
        if (resultSequence <= 0)
        {
            error = "Extraction result sequence must be positive.";
            return false;
        }

        if (!RaidLootEligibilityResolver.TryResolve(
                extractorId,
                lootSnapshot,
                affiliations,
                out RaidLootEligibilitySnapshot eligibility,
                out error))
        {
            return false;
        }

        if (!ExtractedLootExperienceCalculator.TryCalculate(
                eligibility,
                _valueCatalog,
                _experienceRateBasisPoints,
                out ExtractedLootExperienceCalculation calculation,
                out ExtractedLootExperienceCalculationFailure failure))
        {
            error = $"Extracted loot Experience calculation failed: {failure}.";
            return false;
        }

        candidate = new ExtractedLootExperienceCandidate(resultSequence, calculation);
        return candidate.IsValid;
    }

    /// <summary>Builds the Results grid for an extraction: only XP-eligible quantities.</summary>
    public bool TryPrepareExtractedResultsSummary(
        RaidParticipantId extractorId,
        PlayerExpeditionLootSnapshot lootSnapshot,
        RaidInitialAffiliationSnapshot affiliations,
        out RaidResultsLootSummary summary,
        out string error)
    {
        summary = null;
        return RaidLootEligibilityResolver.TryResolve(
                   extractorId,
                   lootSnapshot,
                   affiliations,
                   out RaidLootEligibilitySnapshot eligibility,
                   out error) &&
               RaidResultsLootSummaryBuilder.TryBuildExtracted(
                   eligibility,
                   _valueCatalog,
                   out summary,
                   out error);
    }

    /// <summary>Builds the Lost-loot summary for Defeat or voluntary Abandon: everything carried.</summary>
    public bool TryPrepareLostResultsSummary(
        PlayerExpeditionLootSnapshot lootSnapshot,
        out RaidResultsLootSummary summary,
        out string error)
    {
        summary = null;
        error = "Expedition loot snapshot is required.";
        return lootSnapshot != null &&
               RaidResultsLootSummaryBuilder.TryBuildLost(
                   lootSnapshot.Combined,
                   out summary,
                   out error);
    }

    public ExtractedLootExperienceRegistrationStatus TryApplyConfirmed(
        NetworkRaidParticipant participant,
        in ExtractedLootExperienceCandidate candidate,
        out ExpeditionExperienceLedgerFailure failure)
    {
        if (participant == null || !candidate.IsValid ||
            !candidate.Matches(participant.ResultSequence))
        {
            failure = ExpeditionExperienceLedgerFailure.ResultSequenceMismatch;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        PlayerExpeditionExperienceLedger ledger =
            participant.GetComponent<PlayerExpeditionExperienceLedger>();
        if (ledger == null)
        {
            failure = ExpeditionExperienceLedgerFailure.MissingLedger;
            return ExtractedLootExperienceRegistrationStatus.Failed;
        }

        return ledger.TryRegisterConfirmedExtractedLootReward(
            candidate.ResultSequence,
            candidate.AwardedExperience,
            out failure);
    }
}

using Fusion;

/// <summary>Trap-only rival targeting; ordinary ability enemy areas remain creature-only.</summary>
public static class TrapPlayerTargetRules
{
    public static bool IsEligible(bool isSelf, bool canReceiveStatusEffects, bool affiliationKnown,
        bool isInitialTeammate, bool isProtected) =>
        !isSelf && canReceiveStatusEffects && affiliationKnown && !isInitialTeammate && !isProtected;

    public static bool IsValidTarget(PlayerCharacter caster, PlayerCharacter candidate)
    {
        if (caster == null || candidate == null || caster == candidate) return false;
        NetworkRunner runner = caster.Runner;
        if (runner == null || runner != candidate.Runner) return false;
        if (!candidate.TryGetComponent(out PlayerImmobilizationNetworkEffect effect) ||
            !effect.IsReadyToApply) return false;

        NetworkSpawnManager spawnManager = runner.GetComponent<NetworkSpawnManager>();
        bool teammates = false;
        bool known = spawnManager != null &&
            spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
            TryGetParticipantId(caster, out RaidParticipantId casterId) &&
            TryGetParticipantId(candidate, out RaidParticipantId candidateId) &&
            affiliations.TryAreInitialTeammates(casterId, candidateId, out teammates);
        // An unresolved affiliation is not evidence of hostility.
        if (!known) return false;
        return IsEligible(false, candidate.CanReceiveStatusEffects, true, teammates,
            effect.IsProtectedFromApplication);
    }

    private static bool TryGetParticipantId(PlayerCharacter character, out RaidParticipantId id)
    {
        id = default;
        if (!character.TryGetComponent(out RaidAvatarParticipantLink link) ||
            !link.TryResolveParticipant(out NetworkRaidParticipant participant)) return false;
        id = participant.RaidParticipantId;
        return id.IsValid;
    }
}

using Fusion;

/// <summary>
/// Answers whether two players are initial teammates through the runner's <see cref="NetworkSpawnManager"/>.
/// Same contract the Downed revive uses (<see cref="RaidInitialAffiliationSnapshot.TryAreInitialTeammates"/>);
/// fails closed: any unresolved piece means "not teammates".
/// </summary>
internal static class AbilityAllyAffiliation
{
    public static bool AreInitialTeammates(PlayerCharacter left, PlayerCharacter right)
    {
        if (left == null || right == null) return false;
        NetworkRunner runner = left.Runner;
        if (runner == null || runner != right.Runner) return false;
        NetworkSpawnManager spawnManager = runner.GetComponent<NetworkSpawnManager>();
        return spawnManager != null &&
               spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
               TryGetParticipantId(left, out RaidParticipantId leftId) &&
               TryGetParticipantId(right, out RaidParticipantId rightId) &&
               affiliations.TryAreInitialTeammates(leftId, rightId, out bool areTeammates) &&
               areTeammates;
    }

    private static bool TryGetParticipantId(PlayerCharacter character, out RaidParticipantId participantId)
    {
        participantId = default;
        if (!character.TryGetComponent(out RaidAvatarParticipantLink link) ||
            !link.TryResolveParticipant(out NetworkRaidParticipant participant)) return false;
        participantId = participant.RaidParticipantId;
        return participantId.IsValid;
    }
}

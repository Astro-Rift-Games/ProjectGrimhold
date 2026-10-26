using Fusion;
using UnityEngine;

/// <summary>
/// Authoritatively evaluates whether an interactor or any active member of their team
/// is carrying TRE_CHAOS_COIN (in inventory, in active or Downed state; corpses do not count).
/// </summary>
public static class ChaosCoinCarrierEvaluator
{
    public static readonly LootId ChaosCoinLootId = new("TRE_CHAOS_COIN");

    /// <summary>
    /// Checks if the interactor or their teammate carries TRE_CHAOS_COIN.
    /// Works in Solo (single player) and Duo (two-player teams).
    /// </summary>
    public static bool HasActiveChaosCoinCarrier(PlayerCharacter interactor)
    {
        if (interactor == null)
        {
            return false;
        }

        // 1. Direct check on interactor (alive or Downed)
        if (IsActiveCarrier(interactor))
        {
            return true;
        }

        // 2. Check team partner if in a network session
        NetworkRunner runner = interactor.Runner;
        if (runner == null)
        {
            return false;
        }

        NetworkSpawnManager spawnManager = runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null ||
            !spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations))
        {
            return false;
        }

        if (!interactor.TryGetComponent(out RaidAvatarParticipantLink interactorLink) ||
            !interactorLink.TryResolveParticipant(out NetworkRaidParticipant interactorParticipant) ||
            !interactorParticipant.RaidParticipantId.IsValid)
        {
            return false;
        }

        if (!affiliations.TryGetTeammateProfileId(interactorParticipant.ProfileId, out ProfileId teammateProfileId) ||
            !teammateProfileId.IsValid)
        {
            return false;
        }

        foreach (PlayerRef playerRef in runner.ActivePlayers)
        {
            NetworkObject playerObj = runner.GetPlayerObject(playerRef);
            if (playerObj == null)
            {
                continue;
            }

            if (playerObj.TryGetBehaviour(out NetworkRaidParticipant candidateParticipant) &&
                candidateParticipant.ProfileId == teammateProfileId &&
                candidateParticipant.TryResolveCurrentAvatar(out NetworkObject avatarObj) &&
                avatarObj != null &&
                avatarObj.TryGetComponent(out PlayerCharacter teammateCharacter))
            {
                if (IsActiveCarrier(teammateCharacter))
                {
                    return true;
                }
            }
            else if (playerObj.TryGetComponent(out PlayerCharacter candidateCharacter) &&
                     candidateCharacter != interactor &&
                     candidateCharacter.TryGetComponent(out RaidAvatarParticipantLink candidateLink) &&
                     candidateLink.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                     participant.ProfileId == teammateProfileId)
            {
                if (IsActiveCarrier(candidateCharacter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates if an individual player is an active carrier:
    /// must be alive (normal or Downed; corpses do not count) and carry TRE_CHAOS_COIN in inventory.
    /// </summary>
    public static bool IsActiveCarrier(PlayerCharacter character)
    {
        if (character == null || !character.IsAlive)
        {
            return false;
        }

        PlayerLootReceiver lootReceiver = character.GetComponent<PlayerLootReceiver>();
        if (lootReceiver == null)
        {
            return false;
        }

        return lootReceiver.GetLootAmount(ChaosCoinLootId) > 0;
    }
}

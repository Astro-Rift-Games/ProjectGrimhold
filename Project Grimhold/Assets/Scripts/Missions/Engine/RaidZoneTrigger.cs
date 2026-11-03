using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Trigger volume that fires an authoritative ObjectiveFamily.Exploracion contribution
/// when a player character enters the zone. Supports Duo companion credit within proximity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RaidZoneTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("Unique identifier for this room or zone matching mission objectives.")]
    private string _zoneId = "Catacombs_Main";

    [SerializeField, Tooltip("Proximity radius in world units to grant shared exploration to Duo teammates.")]
    private float _duoProximityRadius = 35f;

    private readonly HashSet<string> _triggeredProfileIds = new();

    public string ZoneId
    {
        get => _zoneId;
        set => _zoneId = value;
    }

    public float DuoProximityRadius
    {
        get => _duoProximityRadius;
        set => _duoProximityRadius = value;
    }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        var player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null) return;

        TryTriggerExploration(player);
    }

    public bool TryTriggerExploration(PlayerCharacter player)
    {
        if (player == null) return false;

        var link = player.GetComponent<RaidAvatarParticipantLink>();
        if (link == null || !link.TryResolveParticipant(out var participant) || participant == null)
        {
            return false;
        }

        // Host/State Authority checks
        if (!participant.HasStateAuthority)
        {
            return false;
        }

        string profileIdStr = participant.ProfileId.Value;
        if (string.IsNullOrEmpty(profileIdStr) || _triggeredProfileIds.Contains(profileIdStr))
        {
            return false;
        }

        _triggeredProfileIds.Add(profileIdStr);

        var ledger = participant.GetComponent<PlayerMissionContributionLedger>();
        if (ledger != null)
        {
            ledger.RecordContribution(
                ObjectiveFamily.Exploracion,
                1,
                null,
                _zoneId,
                isCompanionContribution: false);
        }

        // Duo companion shared exploration credit
        TryAwardCompanionExploration(participant, player);

        return true;
    }

    private void TryAwardCompanionExploration(NetworkRaidParticipant participant, PlayerCharacter player)
    {
        if (participant.Runner == null) return;
        var spawnManager = participant.Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null || !spawnManager.TryGetRaidInitialAffiliations(out var affiliations)) return;

        if (!affiliations.TryGetTeam(participant.RaidParticipantId, out var teamId)) return;

        Vector2 playerPos = player.transform.position;

        for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
        {
            if (!RaidParticipantId.TryCreate(i + 1, out var teammateId) || teammateId == participant.RaidParticipantId) continue;
            if (!affiliations.TryGetTeam(teammateId, out var candidateTeam) || candidateTeam != teamId) continue;

            if (spawnManager.TryGetRaidParticipant(teammateId, out var teammate) &&
                teammate.State == RaidParticipantState.Raiding &&
                teammate.TryResolveCurrentAvatar(out var avatarObj) &&
                avatarObj != null)
            {
                float distance = Vector2.Distance(playerPos, avatarObj.transform.position);
                if (distance <= _duoProximityRadius)
                {
                    string teamProfile = teammate.ProfileId.Value;
                    if (!string.IsNullOrEmpty(teamProfile))
                    {
                        _triggeredProfileIds.Add(teamProfile);
                    }

                    var teammateLedger = teammate.GetComponent<PlayerMissionContributionLedger>();
                    if (teammateLedger != null)
                    {
                        teammateLedger.RecordContribution(
                            ObjectiveFamily.Exploracion,
                            1,
                            null,
                            _zoneId,
                            isCompanionContribution: true);
                    }
                }
            }
        }
    }
}

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
    private static readonly List<RaidZoneTrigger> ActiveTriggers = new();
    public static IReadOnlyList<RaidZoneTrigger> AllActiveTriggers => ActiveTriggers;

    [SerializeField, Tooltip("Unique identifier for this room or zone matching mission objectives.")]
    private string _zoneId = "Catacombs_Main";

    [SerializeField, Tooltip("Proximity radius in world units to grant shared exploration to Duo teammates.")]
    private float _duoProximityRadius = 35f;

    private readonly HashSet<string> _triggeredProfileIds = new();
    private Collider2D _collider;
    private readonly ContactFilter2D _contactFilter = new ContactFilter2D().NoFilter();
    private readonly List<Collider2D> _overlapBuffer = new(8);

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

    private void Reset()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (!ActiveTriggers.Contains(this))
        {
            ActiveTriggers.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveTriggers.Remove(this);
    }

    private void Start()
    {
        EvaluateOverlappingPlayers();
    }

    private void FixedUpdate()
    {
        if (_triggeredProfileIds.Count >= RaidSessionRules.MaxParticipants)
        {
            return;
        }

        EvaluateOverlappingPlayers();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        var player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null) return;

        TryTriggerExploration(player);
    }

    /// <summary>
    /// Statically and spatially evaluates all colliders currently overlapping this trigger.
    /// Fulfills acceptance criteria for players already standing inside the zone volume.
    /// </summary>
    public void EvaluateOverlappingPlayers()
    {
        if (_collider == null) _collider = GetComponent<Collider2D>();
        if (_collider == null) return;

        int count = _collider.Overlap(_contactFilter, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var other = _overlapBuffer[i];
            if (other == null) continue;
            var player = other.GetComponentInParent<PlayerCharacter>();
            if (player != null)
            {
                TryTriggerExploration(player);
            }
        }
        _overlapBuffer.Clear();
    }

    /// <summary>
    /// Checks whether the specified player is spatially inside this trigger volume.
    /// </summary>
    public bool IsPlayerInside(PlayerCharacter player)
    {
        if (player == null) return false;
        if (_collider == null) _collider = GetComponent<Collider2D>();
        if (_collider == null) return false;

        Vector2 playerPos = player.transform.position;
        if (_collider.OverlapPoint(playerPos) || _collider.bounds.Contains(playerPos))
        {
            return true;
        }

        var playerColliders = player.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null && _collider.IsTouching(playerColliders[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Triggers exploration credit if the player is currently inside the volume.
    /// </summary>
    public bool TryTriggerExplorationIfInside(PlayerCharacter player)
    {
        if (player == null) return false;
        if (IsPlayerInside(player))
        {
            return TryTriggerExploration(player);
        }
        return false;
    }

    /// <summary>
    /// Statically evaluates all active zone triggers for a given player character.
    /// </summary>
    public static void EvaluateAllForPlayer(PlayerCharacter player)
    {
        if (player == null) return;
        for (int i = 0; i < ActiveTriggers.Count; i++)
        {
            ActiveTriggers[i].TryTriggerExplorationIfInside(player);
        }
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

using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned ledger for tracking mission contributions during a raid.
/// Events are replicated to the Input Authority via RPC, who evaluates them locally 
/// against the MissionProgressEngine and commits them to the LocalProfileStore.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkRaidParticipant))]
public sealed class PlayerMissionContributionLedger : NetworkBehaviour
{
    private NetworkRaidParticipant _participant;

    private void Awake()
    {
        _participant = GetComponent<NetworkRaidParticipant>();
    }

    /// <summary>
    /// Called by the State Authority (e.g. DamageResolver) when a valid contribution event occurs.
    /// </summary>
    public void RecordContribution(
        ObjectiveFamily family,
        int amount,
        string targetId,
        string zoneId,
        bool isCompanionContribution = false,
        string eventId = null)
    {
        if (!HasStateAuthority) return;

        if (string.IsNullOrEmpty(eventId))
        {
            eventId = System.Guid.NewGuid().ToString("N");
        }

        Rpc_ReplicateContribution(
            (int)family,
            amount,
            targetId ?? string.Empty,
            zoneId ?? string.Empty,
            isCompanionContribution,
            eventId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void Rpc_ReplicateContribution(
        int familyInt,
        int amount,
        NetworkString<_32> targetId,
        NetworkString<_32> zoneId,
        NetworkBool isCompanion,
        NetworkString<_32> eventId)
    {
        if (_participant == null || string.IsNullOrEmpty(_participant.ProfileId.Value)) return;

        var profileId = new ProfileId(_participant.ProfileId.Value);
        var ev = new MissionContributionEvent(
            (ObjectiveFamily)familyInt,
            amount,
            profileId,
            targetId.Value,
            zoneId.Value,
            isCompanion,
            eventId.Value);

        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null && context.Store != null && context.Store.ProfileId == profileId)
        {
            context.Store.TryApplyMissionProgress(ev);
        }
    }
}

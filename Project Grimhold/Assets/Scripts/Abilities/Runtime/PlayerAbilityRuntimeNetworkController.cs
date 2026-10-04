using Fusion;
using UnityEngine;

/// <summary>Owns the authoritative initialization of the productive avatar's ability slots.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAbilityRuntimeNetworkController : NetworkBehaviour
{
    [SerializeField] private RaidAvatarParticipantLink _participantLink;
    [SerializeField] private AbilityDefinitionCatalog _catalog;

    [Networked] private NetworkBool InitializationConfirmed { get; set; }

    private AbilityRuntimeSlots _slots;
    private NetworkRaidParticipant _boundParticipant;
    private NetworkString<_32> _boundGeneration;
    private NetworkSpawnManager _spawnManager;
    private bool _spawned;
    private bool _restoreSpawn;
    private bool _ended;
    private bool _invalid;

    /// <summary>True only for the confirmed, resolved runtime of the current productive avatar.</summary>
    public bool IsInitialized => isActiveAndEnabled && _spawned && Object != null && Object.IsValid &&
        InitializationConfirmed && !_ended && !_invalid && _slots != null &&
        TryResolveCurrentParticipant(out _);

    public override void Spawned()
    {
        _spawned = true;
        _restoreSpawn = HostMigrationRestoreUtility.IsRestoreSpawn(this);
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _ended = false;
        _invalid = false;
        ClearLocalBinding();
        if (_participantLink == null || _catalog == null)
        {
            RejectConfiguration("Raid ability runtime requires a participant link and authorized catalog.");
        }
    }

    public override void FixedUpdateNetwork()
    {
        RefreshBinding();
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            RefreshBinding();
        }
    }

    public bool TryGetSlot(UniversalAbilitySlot slot, out AbilityRuntimeSlot state)
    {
        state = default;
        return IsInitialized && _slots.TryGetSlot(slot, out state);
    }

    /// <summary>Reports prepared-slot availability, not activation eligibility or affordability.</summary>
    public bool IsSlotAvailable(UniversalAbilitySlot slot) =>
        TryGetSlot(slot, out var state) && state.IsPrepared;

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        ClearLocalBinding();
    }

    private void OnDisable()
    {
        _slots = null;
        _boundParticipant = null;
    }
    private void OnDestroy() => ClearLocalBinding();

    private void RefreshBinding()
    {
        if (!isActiveAndEnabled || !_spawned || Object == null || !Object.IsValid || _ended || _invalid)
        {
            return;
        }

        NetworkMatchController match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match != null && (match.Phase == NetworkMatchController.MatchPhase.Closing ||
                              match.Phase == NetworkMatchController.MatchPhase.Finished))
        {
            EndParticipation();
            return;
        }

        if (_participantLink == null || !_participantLink.TryResolveParticipant(out var participant))
        {
            // Missing references may be temporary during restore fixup; never reset copied state.
            _slots = null;
            return;
        }

        NetworkString<_32> generation = participant.RaidGenerationId;
        if (participant.State != RaidParticipantState.Raiding ||
            (_boundGeneration.Length > 0 && !_boundGeneration.Equals(generation)) ||
            (_boundGeneration.Length > 0 && participant.CurrentAvatarId != Object.Id))
        {
            EndParticipation();
            return;
        }

        if (generation.Length == 0 || participant.CurrentAvatarId != Object.Id)
        {
            _slots = null;
            return;
        }

        if (_slots != null && _boundParticipant == participant)
        {
            return;
        }

        if (!InitializationConfirmed && (!HasStateAuthority || _restoreSpawn))
        {
            return;
        }

        if (!participant.TryGetPreparedAbilityLoadout(out var prepared))
        {
            return;
        }

        if (!AbilityRuntimeSlots.TryCreate(prepared, _catalog, out var resolved, out string error))
        {
            RejectConfiguration(error);
            return;
        }

        _slots = resolved;
        _boundParticipant = participant;
        _boundGeneration = generation;
        if (HasStateAuthority && !_restoreSpawn && !InitializationConfirmed)
        {
            InitializationConfirmed = true;
        }
    }

    private bool TryResolveCurrentParticipant(out NetworkRaidParticipant participant)
    {
        participant = null;
        return _participantLink != null && _participantLink.TryResolveParticipant(out participant) &&
            participant == _boundParticipant && participant.State == RaidParticipantState.Raiding &&
            participant.CurrentAvatarId == Object.Id &&
            participant.RaidGenerationId.Equals(_boundGeneration);
    }

    private void EndParticipation()
    {
        if (HasStateAuthority)
        {
            InitializationConfirmed = false;
        }
        _ended = true;
        ClearLocalBinding();
    }

    private void RejectConfiguration(string error)
    {
        _invalid = true;
        ClearLocalBinding();
        Debug.LogError(error, this);
    }

    private void ClearLocalBinding()
    {
        _slots = null;
        _boundParticipant = null;
        _boundGeneration = default;
    }
}

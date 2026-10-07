using Fusion;
using UnityEngine;

/// <summary>Owns expedition-local Mana; maximum is derived from effective player statistics.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-12)]
public sealed class PlayerManaNetworkController : NetworkBehaviour
{
    [SerializeField] private RaidAvatarParticipantLink _participantLink;
    [SerializeField] private PlayerCharacter _playerCharacter;

    [Networked] public float CurrentMana { get; private set; }
    [Networked] private NetworkBool InitializationConfirmed { get; set; }

    private bool _spawned;
    private bool _restoreSpawn;
    private bool _ended;
    private NetworkString<_32> _boundGeneration;
    private NetworkSpawnManager _spawnManager;

    public bool IsInitialized => CanReadState() && !_ended && InitializationConfirmed &&
        TryResolveCurrentParticipation(out _) && TryGetMaximumMana(out _);

    public override void Spawned()
    {
        _spawned = true;
        _restoreSpawn = HostMigrationRestoreUtility.IsRestoreSpawn(this);
        _ended = false;
        _boundGeneration = default;
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (_participantLink == null || _playerCharacter == null)
            Debug.LogError("Raid Mana requires participant and character references.", this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMutateState() || _ended) return;
        if (HasParticipationEnded())
        {
            CurrentMana = 0f;
            InitializationConfirmed = false;
            _ended = true;
            return;
        }
        if (!TryResolveCurrentParticipation(out var participant) || !TryGetMaximumMana(out float maximum)) return;
        _boundGeneration = participant.RaidGenerationId;
        if (!InitializationConfirmed)
        {
            if (_restoreSpawn) return;
            CurrentMana = maximum;
            InitializationConfirmed = true;
            return;
        }
        CurrentMana = ManaResourceRules.ClampCurrent(CurrentMana, maximum);
    }

    public bool TryGetMaximumMana(out float maximum)
    {
        maximum = 0f;
        if (!CanReadState() || _playerCharacter == null ||
            !_playerCharacter.TryGetRuntimeStatistics(out var statistics)) return false;
        maximum = statistics.MaximumMana;
        return ManaResourceRules.IsValidAmount(maximum);
    }

    /// <summary>Read-only affordability query; zero costs still require a valid resource owner.</summary>
    public bool CanSpend(float amount) => IsInitialized && TryGetMaximumMana(out float maximum) &&
        ManaResourceRules.CanSpend(ManaResourceRules.ClampCurrent(CurrentMana, maximum), amount);

    /// <summary>State Authority commits a complete payment in forward simulation only.</summary>
    public bool TrySpend(float amount)
    {
        if (!CanMutateState() || !IsInitialized || !TryGetMaximumMana(out float maximum)) return false;
        // Maximum maintenance is independent of payment, including a rejected cost.
        CurrentMana = ManaResourceRules.ClampCurrent(CurrentMana, maximum);
        if (!ManaResourceRules.TrySpend(CurrentMana, amount, out float remaining)) return false;
        CurrentMana = remaining;
        return true;
    }

    /// <summary>Explicit instant restoration; no passive regeneration or persistent resource mutation.</summary>
    public bool TryRestore(float amount)
    {
        if (!CanMutateState() || !IsInitialized || !TryGetMaximumMana(out float maximum)) return false;
        CurrentMana = ManaResourceRules.ClampCurrent(CurrentMana, maximum);
        if (!ManaResourceRules.TryRestore(CurrentMana, maximum, amount, out float restored)) return false;
        CurrentMana = restored;
        return true;
    }

    private bool CanReadState() => isActiveAndEnabled && _spawned && Object != null && Object.IsValid && Runner != null;
    private bool CanMutateState() => CanReadState() && HasStateAuthority && Runner.IsSimulationUpdating && Runner.IsForward;

    private bool TryResolveCurrentParticipation(out NetworkRaidParticipant participant)
    {
        participant = null;
        return _participantLink != null && _participantLink.TryResolveParticipant(out participant) &&
            participant.State == RaidParticipantState.Raiding && participant.CurrentAvatarId == Object.Id &&
            participant.RaidGenerationId.Length > 0 &&
            (_boundGeneration.Length == 0 || _boundGeneration.Equals(participant.RaidGenerationId)) &&
            !HasParticipationEnded();
    }

    private bool HasParticipationEnded()
    {
        var match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match != null && (match.Phase == NetworkMatchController.MatchPhase.Closing ||
                              match.Phase == NetworkMatchController.MatchPhase.Finished)) return true;
        if (_participantLink == null || !_participantLink.TryResolveParticipant(out var participant)) return false;
        return participant.State != RaidParticipantState.Raiding ||
            (_boundGeneration.Length > 0 && (!participant.RaidGenerationId.Equals(_boundGeneration) ||
                                            participant.CurrentAvatarId != Object.Id));
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        _spawnManager = null;
        _boundGeneration = default;
    }
}

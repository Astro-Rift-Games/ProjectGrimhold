using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned one-shot Kill Experience reward for one networked target.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class KillExperienceSource : NetworkBehaviour, IKillExperienceSource
{
    private const float AssistRetryIntervalSeconds = 1f;

    [SerializeField, Min(0)]
    private long _killExperience;

    [Networked]
    public NetworkBool IsGranted { get; private set; }

    [Networked]
    public NetworkBool IsAssistResolutionCompleted { get; private set; }

    [Networked]
    public NetworkBool IsAssistInitialized { get; private set; }

    [Networked]
    public int EligibleAssistMask { get; private set; }

    [Networked]
    public int GrantedAssistMask { get; private set; }

    [Networked]
    private TickTimer AssistRetryTimer { get; set; }

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    private NetworkSpawnManager _spawnManager;

    public long KillExperience => _killExperience;

    public bool IsAvailable => _killExperience > 0 && !IsGranted;
    bool IKillExperienceSource.IsAssistResolutionCompleted => IsAssistResolutionCompleted;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public override void Spawned()
    {
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsGranted = false;
            IsAssistResolutionCompleted = false;
            IsAssistInitialized = false;
            EligibleAssistMask = 0;
            GrantedAssistMask = 0;
        }

        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _spawnManager = Runner != null ? Runner.GetComponent<NetworkSpawnManager>() : null;
        if (_registry == null || _killExperience < 0)
        {
            Debug.LogError(
                $"{nameof(KillExperienceSource)} requires a runner registry and non-negative Kill Experience.",
                this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterKillExperienceSource(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError(
                $"{nameof(KillExperienceSource)} could not register entity {_registeredId}.",
                this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterSource();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsAssistInitialized || IsAssistResolutionCompleted)
        {
            return;
        }

        if (AssistRetryTimer.IsRunning)
        {
            return;
        }
        
        AssistRetryTimer = TickTimer.CreateFromSeconds(Runner, AssistRetryIntervalSeconds);

        if (_spawnManager != null)
        {
            for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
            {
                int bit = 1 << i;
                if ((EligibleAssistMask & bit) != 0 && (GrantedAssistMask & bit) == 0)
                {
                    if (RaidParticipantId.TryCreate(i + 1, out RaidParticipantId id) &&
                        _spawnManager.TryGetRaidParticipant(id, out NetworkRaidParticipant participant))
                    {
                        TryGrantAssistTo(id, participant.ExperienceLedger);
                    }
                }
            }
        }
    }

    public bool TryGrantTo(PlayerExpeditionExperienceLedger ledger)
    {
        if (!HasStateAuthority || !IsAvailable || ledger == null)
        {
            return false;
        }

        if (!ledger.TryRegisterNormalReward(
                ExpeditionExperienceSource.PveKill,
                _killExperience,
                out _))
        {
            return false;
        }

        IsGranted = true;
        return true;
    }

    public void InitializeAssistCandidates(int eligibleMask)
    {
        if (!HasStateAuthority || IsAssistInitialized)
        {
            return;
        }

        EligibleAssistMask = eligibleMask;
        IsAssistInitialized = true;

        if (EligibleAssistMask == 0)
        {
            IsAssistResolutionCompleted = true;
        }
    }

    public bool TryGrantAssistTo(RaidParticipantId id, PlayerExpeditionExperienceLedger ledger)
    {
        if (!HasStateAuthority || IsAssistResolutionCompleted || ledger == null || !id.IsValid)
        {
            return false;
        }

        NetworkRaidParticipant ledgerParticipant = ledger.GetComponent<NetworkRaidParticipant>();
        if (ledgerParticipant == null || ledgerParticipant.RaidParticipantId != id)
        {
            return false;
        }

        int bit = 1 << (id.Value - 1);
        if ((EligibleAssistMask & bit) == 0 || (GrantedAssistMask & bit) != 0)
        {
            return false; // Not eligible or already granted
        }

        if (ledgerParticipant.State != RaidParticipantState.Raiding)
        {
            GrantedAssistMask |= bit;
            CheckResolutionCompletion();
            return false;
        }

        long assistExperience = _killExperience / 2;
        if (assistExperience <= 0)
        {
            // Nothing to grant, just mark it so we don't try again
            GrantedAssistMask |= bit;
            CheckResolutionCompletion();
            return false;
        }

        if (!ledger.TryRegisterNormalReward(
                ExpeditionExperienceSource.PveAssist,
                assistExperience,
                out _))
        {
            return false;
        }

        GrantedAssistMask |= bit;
        CheckResolutionCompletion();
        return true;
    }

    private void CheckResolutionCompletion()
    {
        if (GrantedAssistMask == EligibleAssistMask)
        {
            IsAssistResolutionCompleted = true;
        }
    }

    private void UnregisterSource()
    {
        if (!_isRegistered)
        {
            return;
        }

        _registry?.TryUnregisterKillExperienceSource(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterSource();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_killExperience < 0)
        {
            _killExperience = 0;
        }
    }
#endif
}

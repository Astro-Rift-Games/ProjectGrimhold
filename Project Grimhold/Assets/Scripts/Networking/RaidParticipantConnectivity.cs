using Fusion;
using UnityEngine;

/// <summary>
/// Maintains connectivity status and reconnection budget for a Raid participant.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidParticipantConnectivity : NetworkBehaviour
{
    [Networked]
    public NetworkBool IsDisconnected { get; set; }

    [Networked]
    public int RemainingBudgetTicks { get; set; }

    [Networked]
    public NetworkBool IsAbsenceConfirmed { get; set; }

    [Networked]
    private NetworkBool IsInitialized { get; set; }

    public bool IsBudgetExhausted => RemainingBudgetTicks <= 0;
    public bool IsDecisionAvailable => IsDisconnected && IsBudgetExhausted && _participant != null && _participant.State == RaidParticipantState.Raiding;

    private NetworkRaidParticipant _participant;

    public override void Spawned()
    {
        if (HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            return;
        }
    }

    private void Awake()
    {
        _participant = GetComponent<NetworkRaidParticipant>();
    }

    /// <summary>
    /// Initializes the budget from the configuration. This is only called once.
    /// </summary>
    public void InitializeBudget(ReconnectBudgetConfig config)
    {
        if (!HasStateAuthority || IsInitialized)
        {
            return;
        }

        if (config == null)
        {
            Debug.LogError($"[RaidParticipantConnectivity] Cannot initialize budget: Config is null");
            RemainingBudgetTicks = 0;
            IsInitialized = true;
            return;
        }

        if (!config.TryValidate(out string error))
        {
            Debug.LogError($"[RaidParticipantConnectivity] Cannot initialize budget: {error}");
            RemainingBudgetTicks = 0;
            IsInitialized = true;
            return;
        }

        int tickRate = Runner.TickRate;
        RemainingBudgetTicks = ReconnectBudgetRules.CalculateInitialTicks(config.DurationSeconds, tickRate);
        IsInitialized = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_participant == null)
        {
            return;
        }

        RemainingBudgetTicks = ReconnectBudgetRules.ConsumeTick(RemainingBudgetTicks, IsDisconnected, _participant.State);
    }

    /// <summary>
    /// Creates a read-only snapshot of the current connectivity state.
    /// </summary>
    public RaidConnectivitySnapshot CreateSnapshot()
    {
        return new RaidConnectivitySnapshot(
            IsDisconnected,
            RemainingBudgetTicks,
            _participant != null ? _participant.State : RaidParticipantState.Raiding);
    }
}

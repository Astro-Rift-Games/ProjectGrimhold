using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative, purifiable player Stun. The avatar owns the copied deadline; reapplication replaces it, never
/// stacks. While active the player cannot move voluntarily, sprint, attack, defend, use consumables, interact or
/// start abilities (consumers read <see cref="PlayerCharacter.IsStunned"/> through <see cref="PlayerStunGate"/>).
/// Applying it interrupts the active ability executions with <see cref="AbilityExecutionStopReason.Stun"/> without
/// refund and ends the other running actions; nothing resumes on expiry. Forced displacement is not blocked.
/// Purification protection refuses new applications; Downed or definitive defeat ends it on the next forward tick.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStunNetworkEffect : NetworkBehaviour, IStunStatus
{
    [SerializeField] private PlayerCharacter _character;
    [SerializeField] private PurificationNetworkEffect _purificationEffect;
    [SerializeField] private PlayerAbilityRuntimeNetworkController _abilityRuntime;

    [Networked] private TickTimer ExpiryTimer { get; set; }

    private bool _hasSpawned;

    public bool IsOwnerConfigured => _character != null && _character.gameObject == gameObject &&
        _purificationEffect != null && _purificationEffect.gameObject == gameObject &&
        _purificationEffect.IsOwnerConfigured &&
        _abilityRuntime != null && _abilityRuntime.gameObject == gameObject;

    private bool HasReadableState => _hasSpawned && Object != null && Object.IsValid && Runner != null;
    private bool CanMutate => HasReadableState && HasStateAuthority && Runner.IsSimulationUpdating && Runner.IsForward;

    public bool IsReadyToApply => IsOwnerConfigured && CanMutate && _character.CanReceiveStatusEffects;
    public bool IsProtectedFromApplication => IsOwnerConfigured && HasReadableState &&
        _purificationEffect.IsProtectedFromPurifiableStates;

    /// <summary>
    /// Observable on every peer (HUD, presentation, simulation gates). Safe before Spawned and after Despawned;
    /// false while Downed or defeated.
    /// </summary>
    public bool IsActive => IsOwnerConfigured && HasReadableState && _character.CanReceiveStatusEffects &&
        !ExpiryTimer.ExpiredOrNotRunning(Runner);

    /// <summary>Remaining seconds of the active Stun, or zero. Read-only presentation query.</summary>
    public float RemainingSeconds => IsActive ? ExpiryTimer.RemainingTime(Runner) ?? 0f : 0f;

    public bool IsStunned => IsActive;
    public float RemainingStunSeconds => RemainingSeconds;

    public override void Spawned()
    {
        // CopyStateFrom already restored the deadline. Only local readiness is rebuilt here.
        _hasSpawned = true;
        if (!IsOwnerConfigured)
            Debug.LogError($"{nameof(PlayerStunNetworkEffect)} requires same-root {nameof(PlayerCharacter)}, {nameof(PurificationNetworkEffect)} and {nameof(PlayerAbilityRuntimeNetworkController)} references.", this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => _hasSpawned = false;

    /// <summary>
    /// State Authority, forward simulation only. Starts or replaces the Stun and interrupts the running actions.
    /// Returns false without any change when the owner cannot receive status effects, is protected by
    /// Purification or the duration is invalid.
    /// </summary>
    public bool TryApply(float durationSeconds)
    {
        if (!IsReadyToApply || IsProtectedFromApplication) return false;
        if (!(durationSeconds > 0f) || float.IsInfinity(durationSeconds))
        {
            Debug.LogError($"{nameof(PlayerStunNetworkEffect)} duration must be finite and greater than zero.", this);
            return false;
        }

        ExpiryTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        _abilityRuntime.InterruptActiveExecutions(AbilityExecutionStopReason.Stun);
        _character.InterruptActionsOnIncapacitation();
        return true;
    }

    public bool TryPurify()
    {
        if (!CanMutate || !IsActive) return false;
        ExpiryTimer = TickTimer.None;
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMutate || !ExpiryTimer.IsRunning) return;
        if (!IsOwnerConfigured || !_character.CanReceiveStatusEffects || ExpiryTimer.Expired(Runner))
            ExpiryTimer = TickTimer.None;
    }
}

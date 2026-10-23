using Fusion;
using UnityEngine;

/// <summary>
/// Trap-only, purifiable player immobilization. The avatar owns the copied deadline; reapplication refreshes it,
/// never stacks. Consumers restrict voluntary movement/sprint only, not attacks, casts or forced displacement.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerImmobilizationNetworkEffect : NetworkBehaviour
{
    [SerializeField] private PlayerCharacter _character;
    [SerializeField] private PurificationNetworkEffect _purificationEffect;

    [Networked] private TickTimer ExpiryTimer { get; set; }

    private bool _hasSpawned;

    public bool IsOwnerConfigured => _character != null && _character.gameObject == gameObject &&
        _purificationEffect != null && _purificationEffect.gameObject == gameObject &&
        _purificationEffect.IsOwnerConfigured;

    private bool HasReadableState => _hasSpawned && Object != null && Object.IsValid && Runner != null;
    private bool CanMutate => HasReadableState && HasStateAuthority && Runner.IsSimulationUpdating && Runner.IsForward;

    public bool IsReadyToApply => IsOwnerConfigured && CanMutate && _character.CanReceiveStatusEffects;
    public bool IsProtectedFromApplication => IsOwnerConfigured && HasReadableState &&
        _purificationEffect.IsProtectedFromPurifiableStates;

    /// <summary>Safe before Spawned and after Despawned; also clears the observable restriction on Downed/death.</summary>
    public bool IsActive => IsOwnerConfigured && HasReadableState && _character.CanReceiveStatusEffects &&
        !ExpiryTimer.ExpiredOrNotRunning(Runner);

    public override void Spawned()
    {
        // CopyStateFrom already restored the deadline. Only local readiness is rebuilt here.
        _hasSpawned = true;
        if (!IsOwnerConfigured)
            Debug.LogError($"{nameof(PlayerImmobilizationNetworkEffect)} requires same-root PlayerCharacter and PurificationNetworkEffect references.", this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => _hasSpawned = false;

    public bool TryApply(float durationSeconds)
    {
        if (!IsReadyToApply || IsProtectedFromApplication) return false;
        if (!(durationSeconds > 0f) || float.IsInfinity(durationSeconds))
        {
            Debug.LogError($"{nameof(PlayerImmobilizationNetworkEffect)} duration must be finite and greater than zero.", this);
            return false;
        }

        ExpiryTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
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

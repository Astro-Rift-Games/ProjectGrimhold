using Fusion;
using UnityEngine;

/// <summary>
/// Avatar-owned Purification state, independent of the casting execution and the caster. Applying it removes the
/// purifiable states of its owner and grants a temporary protection that prevents NEW purifiable states.
/// Re-applying refreshes the protection (set, never stacked). It does not revert effects already resolved.
///
/// Contract for future purifiable player states: before applying themselves they MUST consult
/// <see cref="IsProtectedFromPurifiableStates"/> and refuse while it is true; non-purifiable states ignore it.
/// They must also report themselves through <see cref="HasPurifiableState"/> and end in
/// <see cref="PurgePurifiableStates"/>.
///
/// The protection timer is [Networked], so a Host Migration restore resumes the same protection in the resumed
/// tick domain; nothing is initialized in <see cref="Spawned"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PurificationNetworkEffect : NetworkBehaviour
{
    [SerializeField] private PlayerCharacter _character;

    [Networked] private TickTimer ProtectionTimer { get; set; }

    /// <summary>Static ownership only; never reads or changes network state.</summary>
    public bool IsOwnerConfigured => _character != null && _character.gameObject == gameObject;

    /// <summary>Read-only preflight for authoritative application in the current forward simulation tick.</summary>
    public bool IsReadyToApply => IsOwnerConfigured && Object != null && Object.IsValid && Runner != null &&
        HasStateAuthority && Runner.IsSimulationUpdating && Runner.IsForward && CanReceiveProtection;

    /// <summary>True while NEW purifiable states must be refused for the owner.</summary>
    public bool IsProtectedFromPurifiableStates =>
        IsOwnerConfigured && Object != null && Object.IsValid && Runner != null && !ProtectionTimer.ExpiredOrNotRunning(Runner);

    /// <summary>
    /// Whether the owner has at least one active purifiable state. No player state is purifiable yet (the trap
    /// Immobilize only affects enemies and no player Stun exists), so this is always false. It is the query a
    /// future purifiable player state plugs into together with <see cref="PurgePurifiableStates"/>.
    /// </summary>
    public bool HasPurifiableState => false;

    /// <summary>Whether the owner can receive the temporary protection: it is alive and can receive status effects.</summary>
    public bool CanReceiveProtection => IsOwnerConfigured && _character.CanReceiveStatusEffects;

    public override void Spawned()
    {
        // There is deliberately NO fresh-state initialization here: a fresh Fusion allocation has no protection
        // and CopyStateFrom supplies a restored timer before Spawned, which must not be overwritten.
        if (!IsOwnerConfigured)
            Debug.LogError($"{nameof(PurificationNetworkEffect)} requires the same-root {nameof(PlayerCharacter)} to be assigned.", this);
    }

    /// <summary>
    /// Removes every purifiable state of the owner and starts or refreshes the protection. Returns false without
    /// any change when this peer has no State Authority, the duration is invalid or the owner cannot receive the
    /// protection.
    /// </summary>
    public bool TryApply(float protectionSeconds)
    {
        if (!IsReadyToApply) return false;

        if (!(protectionSeconds > 0f) || float.IsInfinity(protectionSeconds))
        {
            Debug.LogError($"{nameof(PurificationNetworkEffect)} protection seconds must be finite and greater than zero.", this);
            return false;
        }

        if (!CanReceiveProtection) return false;

        PurgePurifiableStates();
        ProtectionTimer = TickTimer.CreateFromSeconds(Runner, protectionSeconds);
        return true;
    }

    /// <summary>
    /// The single extension point where every purifiable state of the owner is ended. Nothing is purifiable on
    /// players today, so there is nothing to purge; do not add placeholder states.
    /// </summary>
    private void PurgePurifiableStates()
    {
    }
}

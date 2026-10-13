using Fusion;
using UnityEngine;

/// <summary>
/// Avatar-owned temporary basic weapon attack damage bonus, independent of the casting execution and the caster.
/// It only raises the damage of basic weapon attacks (melee and ranged) through
/// <see cref="GetBasicAttackDamageMultiplier"/>; abilities, consumables and persistent effects never read it.
/// It is not consumed by attacking or missing and no weapon change removes it. A new application refreshes it:
/// the expiry restarts and the bonus is set, never accumulated.
///
/// Both the expiry timer and the bonus are [Networked], so a Host Migration restore resumes the same effect in
/// the resumed tick domain; nothing is initialized in <see cref="Spawned"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class EmpowerNetworkEffect : NetworkBehaviour
{
    [SerializeField] private PlayerCharacter _character;

    [Networked] private TickTimer ExpiryTimer { get; set; }
    [Networked] private float BonusFraction { get; set; }

    /// <summary>Static ownership only; never reads or changes network state.</summary>
    public bool IsOwnerConfigured => _character != null && _character.gameObject == gameObject;

    /// <summary>Read-only preflight for authoritative application in the current forward simulation tick.</summary>
    public bool IsReadyToApply => IsOwnerConfigured && CanReadState() && HasStateAuthority &&
        Runner.IsSimulationUpdating && Runner.IsForward && _character.IsAlive;

    /// <summary>True while the bonus is running.</summary>
    public bool IsActive => CanReadState() && !ExpiryTimer.ExpiredOrNotRunning(Runner);

    public override void Spawned()
    {
        // There is deliberately NO fresh-state initialization here: a fresh Fusion allocation is inactive
        // (no timer) and CopyStateFrom supplies a restored effect before Spawned, which must not be overwritten.
        if (!IsOwnerConfigured)
            Debug.LogError($"{nameof(EmpowerNetworkEffect)} requires the same-root {nameof(PlayerCharacter)} to be assigned.", this);
    }

    /// <summary>
    /// Starts or refreshes the bonus. Returns false without any change when this peer has no State Authority, the
    /// values are invalid or the owner is not alive.
    /// </summary>
    public bool TryApply(float bonusFraction, float durationSeconds)
    {
        if (!IsReadyToApply) return false;

        if (!EmpowerRules.TryValidateEffect(bonusFraction, durationSeconds, out string error))
        {
            Debug.LogError($"{nameof(EmpowerNetworkEffect)} rejected its configuration: {error}", this);
            return false;
        }

        BonusFraction = EmpowerRules.ResolveRefreshedBonus(BonusFraction, bonusFraction);
        ExpiryTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        return true;
    }

    /// <summary>
    /// Multiplier for the damage of a basic weapon attack: 1 plus the bonus while the effect runs, otherwise 1.
    /// A read-only query, valid on any peer.
    /// </summary>
    public float GetBasicAttackDamageMultiplier() => EmpowerRules.ResolveDamageMultiplier(IsActive, BonusFraction);

    private bool CanReadState() => IsOwnerConfigured && Object != null && Object.IsValid && Runner != null;
}

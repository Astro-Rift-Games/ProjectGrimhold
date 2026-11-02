using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority register of indirect responsibility for environmental hazard exposure (Doc 18 §6.5,
/// Doc 19 §8). Accepted knockbacks name a pending pusher; hazards report presence every tick. Hazard damage
/// then resolves the linked pusher as Downed credit causer while the direct damage source stays environmental.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHazardExposureCausalityNetworkController : NetworkBehaviour
{
    [Networked] public HazardExposureCausalityState State { get; private set; }

    private PlayerCharacter _character;
    private PlayerMovementNetworkController _movement;
    private bool _configurationValid;

    public override void Spawned()
    {
        _character = GetComponent<PlayerCharacter>();
        _movement = GetComponent<PlayerMovementNetworkController>();
        _configurationValid = _character != null && _movement != null;
        if (!_configurationValid)
        {
            Debug.LogError($"{nameof(PlayerHazardExposureCausalityNetworkController)} requires co-located " +
                $"{nameof(PlayerCharacter)} and {nameof(PlayerMovementNetworkController)}.", this);
        }

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            State = default;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_configurationValid)
        {
            return;
        }

        HazardExposureCausalityState state = State;
        int tick = Runner.Tick;
        if (!_character.IsAlive || _character.IsDowned)
        {
            // Credit is frozen at Downed entry; a recovered target starts a new causal register.
            if (!HazardExposureCausalityRules.HasCausalState(in state))
            {
                return;
            }

            HazardExposureCausalityRules.Reset(ref state, HazardCausalityBreakReason.TargetLeftActiveState, tick);
        }
        else
        {
            HazardExposureCausalityRules.Tick(ref state, _movement.IsKnockbackActive, tick);
        }

        State = state;
    }

    /// <summary>Records a Started or Replaced knockback; a default pusher is a non-player displacement.</summary>
    public void RecordAcceptedKnockback(EntityId pusherId)
    {
        if (!CanMutate() || !_movement.TryGetKnockbackSequence(out int sequence))
        {
            return;
        }

        HazardExposureCausalityState state = State;
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref state, pusherId.Value, sequence, Runner.Tick);
        State = state;
    }

    /// <summary>Called by an authoritative hazard each tick the target is inside its exposure area.</summary>
    public void ReportHazardPresence(EntityId hazardId)
    {
        if (!CanMutate() || _character.IsDowned || !_character.IsAlive)
        {
            return;
        }

        HazardExposureCausalityState state = State;
        HazardExposureCausalityRules.ReportPresence(ref state, hazardId.Value, Runner.Tick);
        State = state;
    }

    /// <summary>Resolves the pusher whose knockback still sustains exposure to <paramref name="hazardId"/>.</summary>
    public bool TryResolveHazardCauser(EntityId hazardId, out EntityId pusherId)
    {
        pusherId = default;
        if (!CanMutate() ||
            !HazardExposureCausalityRules.TryResolveCauser(State, hazardId.Value, Runner.Tick, out int pusher))
        {
            return false;
        }

        pusherId = new EntityId(pusher);
        return true;
    }

    private bool CanMutate() => _configurationValid && Object != null && Object.IsValid && HasStateAuthority;
}

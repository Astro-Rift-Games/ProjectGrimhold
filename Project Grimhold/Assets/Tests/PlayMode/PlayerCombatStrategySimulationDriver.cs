#if UNITY_INCLUDE_TESTS
using Fusion;
using UnityEngine;

/// <summary>
/// Applies authoritative player-combat mutations from a Fusion simulation tick.
/// </summary>
public sealed class PlayerCombatStrategySimulationDriver : SimulationBehaviour
{
    private enum RequestedOperation
    {
        None,
        SetStrategy,
        ClearStrategy,
        SetEnabled,
        DefeatCharacter
    }

    private RequestedOperation _operation;
    private PlayerCombatNetworkController _target;
    private MonoBehaviour _attackSource;
    private PlayerCharacter _character;
    private bool _enabled;

    public int CompletionSequence { get; private set; }
    public bool LastResult { get; private set; }

    public void RequestSetStrategy(
        PlayerCombatNetworkController target,
        MonoBehaviour attackSource)
    {
        _target = target;
        _attackSource = attackSource;
        _operation = RequestedOperation.SetStrategy;
    }

    public void RequestClearStrategy(PlayerCombatNetworkController target)
    {
        _target = target;
        _attackSource = null;
        _operation = RequestedOperation.ClearStrategy;
    }

    public void RequestSetEnabled(PlayerCombatNetworkController target, bool enabled)
    {
        _target = target;
        _enabled = enabled;
        _operation = RequestedOperation.SetEnabled;
    }

    public void RequestDefeatCharacter(PlayerCharacter character)
    {
        _character = character;
        _operation = RequestedOperation.DefeatCharacter;
    }

    public override void FixedUpdateNetwork()
    {
        if (_operation == RequestedOperation.None ||
            (_operation == RequestedOperation.DefeatCharacter
                ? _character == null
                : _target == null))
        {
            return;
        }

        RequestedOperation operation = _operation;
        _operation = RequestedOperation.None;
        switch (operation)
        {
            case RequestedOperation.SetStrategy:
                LastResult = _target.TrySetActiveAttack(_attackSource);
                break;
            case RequestedOperation.ClearStrategy:
                LastResult = _target.TryClearActiveAttack();
                break;
            case RequestedOperation.SetEnabled:
                LastResult = _target.TrySetAttackEnabled(_enabled);
                break;
            case RequestedOperation.DefeatCharacter:
                var damage = new DamageRequest(
                    new EntityId(int.MaxValue),
                    _character.Id,
                    1000f,
                    DamageType.TrueDamage,
                    Vector2.down,
                    _character.transform.position,
                    Runner.Tick);
                LastResult = _character.ApplyDamage(damage).IsFatal;
                break;
        }

        CompletionSequence++;
    }
}
#endif

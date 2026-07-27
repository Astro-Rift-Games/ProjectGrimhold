using UnityEngine;
using Fusion;

public enum TrapState
{
    Ready,
    Telegraphing,
    Active,
    InCooldown,
}

[CreateAssetMenu(fileName = "NewTrapInfo", menuName = "Grimhold/TrapInfo")]
public class TrapInfo : ScriptableObject
{
    [SerializeField] private float _activationTime;
    public float activationTime => _activationTime;

    [SerializeField] private float _resetTime;
    public float resetTime => _resetTime;

    [SerializeField] private float _cooldown;
    public float cooldown => _cooldown;

    [SerializeField] private float _damage;
    public float damage => _damage;
    [SerializeField] private DamageType _damageType;
    public DamageType DamageType => _damageType;

    [Networked]
    public bool IsReady { get; private set; }

    [Networked]
    public TrapState State { get; private set; }
    public void SetState(TrapState newState)
    {
        State = newState;
        IsReady = newState == TrapState.Ready;
    }

}

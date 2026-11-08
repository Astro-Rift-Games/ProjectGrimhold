using Fusion;
using UnityEngine;

/// <summary>
/// Creature-owned Stun deadline. Reapplication replaces the deadline; forced displacement is unaffected.
/// Only State Authority in forward simulation may apply it. Spawned preserves snapshot state.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStunNetworkEffect : NetworkBehaviour, IStunStatus
{
    [Networked] private TickTimer ExpiryTimer { get; set; }

    private EnemyCharacter _character;
    private EnemyCombatAIController _combat;
    private ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController _specter;
    private bool _hasSpawned;

    private bool HasReadableState => _hasSpawned && Object != null && Object.IsValid && Runner != null;
    private bool CanMutate => HasReadableState && HasStateAuthority && Runner.IsSimulationUpdating && Runner.IsForward;

    public bool IsActive => HasReadableState && _character != null && _character.IsAlive &&
        !ExpiryTimer.ExpiredOrNotRunning(Runner);
    public float RemainingSeconds => IsActive ? ExpiryTimer.RemainingTime(Runner) ?? 0f : 0f;

    public bool IsStunned => IsActive;
    public float RemainingStunSeconds => RemainingSeconds;

    public override void Spawned()
    {
        _character = GetComponent<EnemyCharacter>();
        _combat = GetComponent<EnemyCombatAIController>();
        _specter = GetComponent<ProjectGrimhold.Gameplay.Enemies.Specter.SpecterAIController>();
        _hasSpawned = true;
        if (_character == null)
            Debug.LogError($"{nameof(EnemyStunNetworkEffect)} requires a same-root {nameof(EnemyCharacter)}.", this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => _hasSpawned = false;

    public bool TryApply(float seconds)
    {
        if (!CanMutate || _character == null || !_character.IsAlive || !_character.CanReceiveDamage ||
            !(seconds > 0f) || float.IsInfinity(seconds)) return false;
        ExpiryTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        _combat?.CancelAttackForStun();
        _specter?.InterruptChannelForStun();
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMutate || !ExpiryTimer.IsRunning) return;
        if (_character == null || !_character.IsAlive || ExpiryTimer.Expired(Runner))
            ExpiryTimer = TickTimer.None;
    }
}

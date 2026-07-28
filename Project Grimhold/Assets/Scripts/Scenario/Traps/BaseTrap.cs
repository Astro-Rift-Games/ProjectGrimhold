using Fusion;
using UnityEngine;

/// <summary>
/// Componente de red base para trampas de escenario.
/// Administra la máquina de estados de la trampa en FixedUpdateNetwork impulsada por State Authority.
/// </summary>
public class BaseTrap : NetworkBehaviour
{
    [SerializeField] protected TrapInfo trapInfo;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    [Networked] public TrapState State { get; private set; }
    [Networked] private TickTimer PhaseTimer { get; set; }

    private bool _triggerEntered;

    public override void Spawned()
    {
        if (trapInfo == null)
        {
            Debug.LogError($"{nameof(BaseTrap)}: Falta la configuración TrapInfo en {gameObject.name}.", this);
            return;
        }

        if (HasStateAuthority)
        {
            State = TrapState.Ready;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Únicamente la autoridad de estado (Host) procesa la activación del trigger
        if (!HasStateAuthority || State != TrapState.Ready) return;
        _triggerEntered = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || trapInfo == null) return;

        switch (State)
        {
            case TrapState.Ready:
                if (_triggerEntered)
                {
                    _triggerEntered = false;
                    EnterPhase(TrapState.Telegraphing, trapInfo.activationTime);
                    OnEnterTelegraphing();
                }
                break;

            case TrapState.Telegraphing:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                {
                    EnterPhase(TrapState.Active, trapInfo.resetTime);
                    OnEnterActive();
                }
                break;

            case TrapState.Active:
                UpdateActive();
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                {
                    EnterPhase(TrapState.InCooldown, trapInfo.cooldown);
                    OnEnterCooldown();
                }
                break;

            case TrapState.InCooldown:
                if (PhaseTimer.ExpiredOrNotRunning(Runner))
                {
                    State = TrapState.Ready;
                    OnEnterReady();
                }
                break;
        }
    }

    private void EnterPhase(TrapState newState, float duration)
    {
        State = newState;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    protected virtual void OnEnterTelegraphing() { }
    protected virtual void OnEnterActive() { }
    protected virtual void UpdateActive() { }
    protected virtual void OnEnterCooldown() { }
    protected virtual void OnEnterReady() { }

    public override void Render()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.color = State switch
        {
            TrapState.Telegraphing => Color.yellow,
            TrapState.Active       => Color.red,
            TrapState.InCooldown   => Color.gray,
            _                      => Color.green
        };
    }
}

using Fusion;
using UnityEngine;

/// <summary>
/// Networked entity representing a persistent smoke cloud deployed by CON_SMOKE_BOMB (Doc 14 TASK-233).
/// Impairs player line-of-sight and AI visual perception for 8.0 seconds across a 3.0m radius.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class NetworkSmokeZone : NetworkBehaviour, ISmokeZone
{
    [SerializeField, Min(0.1f)]
    private float _radius = 3.0f;

    [SerializeField, Min(0.1f)]
    private float _durationSeconds = 8.0f;

    [SerializeField]
    private SpriteRenderer _visualRenderer;

    [Networked]
    private TickTimer LifetimeTimer { get; set; }

    [Networked]
    private float NetworkedRadius { get; set; }

    public Vector2 Position => transform.position;
    public float Radius => NetworkedRadius > 0f ? NetworkedRadius : _radius;
    public bool IsActive => Object != null && Object.IsValid && (!LifetimeTimer.IsRunning || !LifetimeTimer.Expired(Runner));

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkedRadius = _radius;
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, _durationSeconds);
        }

        SmokeZoneRegistry.Register(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward) return;

        if (LifetimeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        SmokeZoneRegistry.Unregister(this);
    }

    private void OnDestroy()
    {
        SmokeZoneRegistry.Unregister(this);
    }

    public bool ContainsPoint(Vector2 point)
    {
        return ((Vector2)transform.position - point).sqrMagnitude <= Radius * Radius;
    }

    public bool BlocksLine(Vector2 start, Vector2 end, float closeVisionDistance = 1.2f)
    {
        return SmokeZoneRegistry.CircleBlocksLine(transform.position, Radius, start, end, closeVisionDistance);
    }

    internal void Configure(float radius, float durationSeconds)
    {
        _radius = radius;
        _durationSeconds = durationSeconds;
        if (HasStateAuthority && Object != null && Object.IsValid)
        {
            NetworkedRadius = radius;
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}

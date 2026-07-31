using Fusion;
using UnityEngine;

/// <summary>
/// Concrete implementation of IDamageResolver as a NetworkBehaviour to operate authoritatively
/// within the Photon Fusion session.
/// Works for both Player and Enemy entities since it resolves damage through the EntityRegistry
/// which registers all IDamageable entities regardless of type.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageResolver : NetworkBehaviour, IDamageResolver
{
    private EntityRegistry _registry;
    private IResolvedDamageFeedbackSink _feedbackSink;

    private void Awake()
    {
        CacheFeedbackSink();
    }

    public override void Spawned()
    {
        CacheFeedbackSink();
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(DamageResolver)}: EntityRegistry component was not found on the NetworkRunner GameObject.", this);
        }
    }

    /// <summary>
    /// Resolves a damage request by locating the entity, applying general validations,
    /// and delegating the actual application to the entity authoritatively.
    /// Works for any IDamageable entity (Player, Enemy, NPC, etc.) registered in the EntityRegistry.
    /// </summary>
    public DamageResult Resolve(in DamageRequest request)
    {
        // 1. Validate self-damage
        if (request.AttackerId == request.TargetId)
        {
            return CompleteResolution(request, new DamageResult(
                request.TargetId,
                false,
                0f,
                0f,
                false,
                DamageFailureReason.SelfDamageRejected
            ));
        }

        if (_registry == null)
        {
            return CompleteResolution(request, new DamageResult(
                request.TargetId,
                false,
                0f,
                0f,
                false,
                DamageFailureReason.TargetUnavailable
            ));
        }

        // 2. Locate target entity (works for any IDamageable: Player, Enemy, etc.)
        if (!_registry.TryGetDamageable(request.TargetId, out IDamageable target))
        {
            return CompleteResolution(request, new DamageResult(
                request.TargetId,
                false,
                0f,
                0f,
                false,
                DamageFailureReason.InvalidTarget
            ));
        }

        // 3. Verify target can receive damage
        if (!target.CanReceiveDamage)
        {
            return CompleteResolution(request, new DamageResult(
                request.TargetId,
                false,
                0f,
                0f,
                false,
                DamageFailureReason.TargetUnavailable
            ));
        }

        // 4. Delegate damage application and handle authority validation within IDamageable
        DamageResult result = target.ApplyDamage(request);
        return CompleteResolution(request, result);
    }

    private DamageResult CompleteResolution(in DamageRequest request, in DamageResult result)
    {
        _feedbackSink?.RecordResolvedDamage(new DamageResolvedEvent(request, result));
        return result;
    }

    private void CacheFeedbackSink()
    {
        _feedbackSink = GetComponent<IResolvedDamageFeedbackSink>();
    }
}

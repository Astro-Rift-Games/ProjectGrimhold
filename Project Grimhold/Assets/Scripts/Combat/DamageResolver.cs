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

    private void Awake()
    {
    }

    public override void Spawned()
    {
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

        // 5. Apply knockback to the target if the damage was successfully applied.
        //    IKnockbackReceiver is implemented by CharacterBase, which delegates to
        //    the movement motor. Breakables do not implement IKnockbackReceiver.
        if (result.IsApplied &&
            request.KnockbackForce > 0f &&
            target is IKnockbackReceiver knockbackReceiver)
        {
            knockbackReceiver.ReceiveKnockback(request.Direction, request.KnockbackForce);
        }

        // 6. Alert enemy targets aggro'd by a player hit.
        //    IAggroReceiver is implemented by EnemyMovementAIController (a sibling component).
        //    Only player-sourced damage triggers the pursuit bypass; traps and
        //    breakable objects do not because they are not registered as PlayerCharacter.
        if (result.IsApplied &&
            target is MonoBehaviour mb &&
            mb.TryGetComponent(out IAggroReceiver aggroReceiver) &&
            _registry != null &&
            _registry.IsPlayerEntity(request.AttackerId) &&
            _registry.TryGetTransform(request.AttackerId, out Transform attackerTransform))
        {
            aggroReceiver.ReceiveAggroAlert(request.AttackerId, attackerTransform);
        }

        TryAwardFatalProgress(request, result);
        TryAwardFatalKillExperience(request, result);
        return CompleteResolution(request, result);
    }

    private void TryAwardFatalKillExperience(in DamageRequest request, in DamageResult result)
    {
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal || _registry == null ||
            !_registry.TryGetKillExperienceSource(request.TargetId, out IKillExperienceSource source) ||
            !source.IsAvailable ||
            !_registry.TryGetDamageable(request.AttackerId, out IDamageable attacker) ||
            attacker is not PlayerCharacter player)
        {
            return;
        }

        RaidAvatarParticipantLink participantLink = player.GetComponent<RaidAvatarParticipantLink>();
        if (participantLink == null ||
            !participantLink.TryResolveParticipant(out NetworkRaidParticipant participant) ||
            !participant.TryResolveCurrentAvatar(out NetworkObject currentAvatar) ||
            currentAvatar != player.Object)
        {
            return;
        }

        PlayerExpeditionExperienceLedger ledger =
            participant.GetComponent<PlayerExpeditionExperienceLedger>();
        source.TryGrantTo(ledger);
    }

    private void TryAwardFatalProgress(in DamageRequest request, in DamageResult result)
    {
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal ||
            request.AttackerId.Value == 0 || request.TargetId.Value == 0 || _registry == null)
        {
            return;
        }

        if (!_registry.TryGetExtractionProgressDefeatSource(
                request.TargetId,
                out IExtractionProgressDefeatSource defeatSource) ||
            defeatSource.DefeatProgressReward <= 0 ||
            !_registry.TryGetExtractionProgressReceiver(
                request.AttackerId,
                out IExtractionProgressReceiver receiver))
        {
            return;
        }

        receiver.TryApplyContribution(new ExtractionProgressContribution(
            ExtractionProgressSourceType.Defeat,
            request.TargetId,
            defeatSource.DefeatProgressReward,
            request.SimulationTick));
    }

    private DamageResult CompleteResolution(in DamageRequest request, in DamageResult result)
    {
        if (_registry != null && _registry.TryGetFeedbackSink(request.AttackerId, out IResolvedDamageFeedbackSink sink))
        {
            sink.RecordResolvedDamage(new DamageResolvedEvent(request, result));
        }

        return result;
    }
}

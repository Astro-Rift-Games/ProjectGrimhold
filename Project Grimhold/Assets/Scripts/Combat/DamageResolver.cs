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
    private NetworkSpawnManager _spawnManager;
    [SerializeField, Min(0f)] private float _allyDamageMultiplier = 1f;

    // Explicit test seams; production composition remains runner-scoped.
    internal System.Func<PlayerCharacter, PlayerCharacter, bool> AreInitialTeammates { get; set; } =
        AbilityAllyAffiliation.AreInitialTeammates;
    internal System.Func<bool> FriendlyFireEnabled { get; set; }

    private bool ResolveFriendlyFireEnabled() => FriendlyFireEnabled != null
        ? FriendlyFireEnabled()
        : _spawnManager != null && _spawnManager.TryGetRaidFriendlyFireEnabled(out bool enabled) && enabled;

    private void Awake()
    {
    }

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        if (_registry == null)
        {
            Debug.LogError($"{nameof(DamageResolver)}: EntityRegistry component was not found on the NetworkRunner GameObject.", this);
        }

        _spawnManager = Runner != null ? Runner.GetComponent<NetworkSpawnManager>() : null;
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

        bool isAlly = request.AttackerId.Value != 0 && target is PlayerCharacter targetPlayer &&
            _registry.TryGetDamageable(request.AttackerId, out IDamageable source) &&
            source is PlayerCharacter attackerPlayer && AreInitialTeammates(attackerPlayer, targetPlayer);
        if (FriendlyFireRules.IsBlocked(isAlly, ResolveFriendlyFireEnabled()))
        {
            return CompleteResolution(request, new DamageResult(request.TargetId, false, 0f, 0f,
                false, DamageFailureReason.FriendlyFireBlocked));
        }

        // Always reconstruct: callers cannot opt into the protected ally path themselves.
        var resolvedRequest = new DamageRequest(request.AttackerId, request.TargetId,
            FriendlyFireRules.ScaleDamage(request.Amount, isAlly, _allyDamageMultiplier),
            request.DamageType, request.Direction, request.HitPoint, request.SimulationTick,
            request.KnockbackForce, isAlly);

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

        // Doc 18 §4.3: capture eligibility before this hit can transition Active to Downed.
        bool knockbackEligibleAtImpact = target is IKnockbackReceiver receiver && receiver.IsKnockbackEligible;

        // 4. Delegate damage application and handle authority validation within IDamageable
        DamageResult result = target.ApplyDamage(resolvedRequest);

        // 5. Apply knockback to the target if the damage was successfully applied.
        //    IKnockbackReceiver is implemented by CharacterBase, which delegates to
        //    the movement motor. Breakables do not implement IKnockbackReceiver.
        if ((result.IsApplied || (resolvedRequest.Amount == 0f &&
                result.FailureReason == DamageFailureReason.InvalidAmount)) &&
            request.KnockbackForce > 0f &&
            target is IKnockbackReceiver knockbackReceiver)
        {
            knockbackReceiver.ReceiveKnockback(new KnockbackRequest(
                request.Direction, request.KnockbackForce, knockbackEligibleAtImpact));
        }

        // 6. Alert enemy targets aggro'd by a player hit.
        //    IAggroReceiver is implemented by EnemyMovementAIController (a sibling component).
        //    Only player-sourced damage triggers the pursuit bypass; traps and
        //    breakable objects do not because they are not registered as PlayerCharacter.
        if (result.IsApplied && result.AppliedDamage > 0f && !isAlly &&
            target is MonoBehaviour mb &&
            mb.TryGetComponent(out IAggroReceiver aggroReceiver) &&
            _registry != null &&
            _registry.IsPlayerEntity(request.AttackerId) &&
            _registry.TryGetTransform(request.AttackerId, out Transform attackerTransform))
        {
            aggroReceiver.ReceiveAggroAlert(request.AttackerId, attackerTransform);
        }

        if (result.IsApplied && result.AppliedDamage > 0f && !isAlly && HasStateAuthority && request.AttackerId.Value != 0 && request.TargetId.Value != 0 && _registry != null)
        {
            if (_registry.TryGetCombatContributionTracker(request.TargetId, out ICombatContributionTracker tracker))
            {
                if (_registry.TryGetDamageable(request.AttackerId, out IDamageable attacker) &&
                    attacker is PlayerCharacter player)
                {
                    RaidAvatarParticipantLink participantLink = player.GetComponent<RaidAvatarParticipantLink>();
                    if (participantLink != null &&
                        participantLink.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                        participant.RaidParticipantId.IsValid &&
                        participant.TryResolveCurrentAvatar(out NetworkObject currentAvatar) &&
                        currentAvatar == player.Object)
                    {
                        tracker.TryRecordContribution(participant.RaidParticipantId, request.SimulationTick);
                    }
                }
            }
        }

        TryAwardFatalProgress(request, result);
        TryAwardFatalAssistExperience(request, result);
        TryAwardFatalKillExperience(request, result);
        TryAwardFatalMissionProgress(request, result);
        TryRecordFatalInstability(request, result);
        return CompleteResolution(request, result);
    }

    private void TryAwardFatalAssistExperience(in DamageRequest request, in DamageResult result)
    {
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal || _registry == null || _spawnManager == null ||
            !_registry.TryGetKillExperienceSource(request.TargetId, out IKillExperienceSource source) ||
            source.IsAssistResolutionCompleted ||
            !_registry.TryGetCombatContributionTracker(request.TargetId, out ICombatContributionTracker tracker))
        {
            return;
        }

        RaidParticipantId killerParticipantId = default;
        if (request.AttackerId.Value != 0 &&
            _registry.TryGetDamageable(request.AttackerId, out IDamageable attacker) &&
            attacker is PlayerCharacter attackerPlayer)
        {
            RaidAvatarParticipantLink link = attackerPlayer.GetComponent<RaidAvatarParticipantLink>();
            if (link != null && link.TryResolveParticipant(out NetworkRaidParticipant p))
            {
                killerParticipantId = p.RaidParticipantId;
            }
        }

        var contributors = new System.Collections.Generic.HashSet<RaidParticipantId>();
        tracker.GetValidContributors(request.SimulationTick, contributors);

        int eligibleMask = 0;
        foreach (RaidParticipantId contributorId in contributors)
        {
            if (contributorId == killerParticipantId) continue;

            if (_spawnManager.TryGetRaidParticipant(contributorId, out NetworkRaidParticipant participant) &&
                participant.RaidParticipantId.IsValid &&
                participant.TryResolveCurrentAvatar(out NetworkObject currentAvatar) &&
                currentAvatar.Id.IsValid)
            {
                eligibleMask |= (1 << (contributorId.Value - 1));
            }
        }

        source.InitializeAssistCandidates(eligibleMask);

        foreach (RaidParticipantId contributorId in contributors)
        {
            if (contributorId == killerParticipantId) continue;

            if (_spawnManager.TryGetRaidParticipant(contributorId, out NetworkRaidParticipant participant))
            {
                PlayerExpeditionExperienceLedger ledger = participant.GetComponent<PlayerExpeditionExperienceLedger>();
                if (ledger != null)
                {
                    source.TryGrantAssistTo(contributorId, ledger);
                }
            }
        }
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

    private void TryAwardFatalMissionProgress(in DamageRequest request, in DamageResult result)
    {
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal || _registry == null ||
            !_registry.TryGetMissionProgressDefeatSource(request.TargetId, out IMissionProgressDefeatSource source) ||
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

        PlayerMissionContributionLedger ledger =
            participant.GetComponent<PlayerMissionContributionLedger>();
        if (ledger != null)
        {
            ledger.RecordContribution(
                ObjectiveFamily.EliminacionPvE,
                source.DefeatProgressAmount,
                source.TargetId,
                source.ZoneId,
                isCompanionContribution: false);
        }
    }

    private void TryRecordFatalInstability(in DamageRequest request, in DamageResult result)
    {
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal || _registry == null)
        {
            return;
        }

        if (_registry.IsPlayerEntity(request.TargetId))
        {
            return;
        }

        if (!_registry.TryGetDamageable(request.TargetId, out IDamageable target) ||
            target is not EnemyCharacter enemy)
        {
            return;
        }

        if (_registry.TryGetDungeonInstabilitySink(out IDungeonInstabilitySink sink))
        {
            sink.TryRecordEnemyDefeat(enemy.CreatureTier, enemy.PopulationOrigin);
        }
    }
}

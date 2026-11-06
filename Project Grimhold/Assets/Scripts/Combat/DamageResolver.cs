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
            request.KnockbackForce, isAlly, request.EmpowerSourceId, request.EnvironmentalHazardId);

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

        // Reserve depletion clears IsDowned before ApplyDamage returns, so capture both sides.
        bool wasDowned = target is PlayerCharacter { IsDowned: true };
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
            KnockbackDecision decision = knockbackReceiver.ReceiveKnockback(new KnockbackRequest(
                request.Direction, request.KnockbackForce, knockbackEligibleAtImpact));
            RecordAcceptedKnockbackCause(request, target, decision);
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

        if (result.IsApplied && result.AppliedDamage > 0f && !isAlly && !wasDowned &&
            target is not PlayerCharacter { IsDowned: true } && HasStateAuthority &&
            request.AttackerId.Value != 0 && request.TargetId.Value != 0 && _registry != null)
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
                        (target is PlayerCharacter ||
                         (participant.TryResolveCurrentAvatar(out NetworkObject currentAvatar) && currentAvatar == player.Object)))
                    {
                        tracker.TryRecordContribution(participant.RaidParticipantId, request.SimulationTick);
                        // Effective empowered basic attacks contribute only to the PvP register, never PvE.
                        if (target is PlayerCharacter && request.EmpowerSourceId.IsValid &&
                            request.EmpowerSourceId != participant.RaidParticipantId)
                        {
                            tracker.TryRecordContribution(request.EmpowerSourceId, request.SimulationTick, CombatContributionKind.Empower);
                        }
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

    /// <summary>Doc 19 §8: an accepted player push may become the indirect cause of hazard exposure.</summary>
    private void RecordAcceptedKnockbackCause(in DamageRequest request, IDamageable target, KnockbackDecision decision)
    {
        if (!HasStateAuthority || target is not PlayerCharacter player || player.HazardExposureCausality == null ||
            (decision != KnockbackDecision.Started && decision != KnockbackDecision.Replaced))
        {
            return;
        }

        // Non-player displacements still replace the previous push, so they clear its pending cause.
        bool isPlayerPush = request.AttackerId.Value != 0 &&
            _registry.TryGetDamageable(request.AttackerId, out IDamageable source) && source is PlayerCharacter;
        player.HazardExposureCausality.RecordAcceptedKnockback(isPlayerPush ? request.AttackerId : default);
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
        if (!HasStateAuthority || !result.IsApplied || !result.IsFatal || _registry == null)
        {
            return;
        }

        if (!_registry.TryGetDamageable(request.AttackerId, out IDamageable attacker) ||
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

        if (_registry.TryGetMissionProgressDefeatSource(request.TargetId, out IMissionProgressDefeatSource source))
        {
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

                if (!string.IsNullOrEmpty(source.TargetId))
                {
                    ledger.RecordContribution(
                        ObjectiveFamily.EliminacionEspecifica,
                        source.DefeatProgressAmount,
                        source.TargetId,
                        source.ZoneId,
                        isCompanionContribution: false);
                }
            }

            TryAwardCompanionDefeatProgress(participant, player, source);
        }
        else if (_registry.IsPlayerEntity(request.TargetId))
        {
            if (_registry.TryGetDamageable(request.TargetId, out IDamageable victimDamageable) &&
                victimDamageable is PlayerCharacter victimPlayer &&
                victimPlayer.GetComponent<PlayerPvpCombatCreditNetworkController>() == null)
            {
                var ledger = participant.GetComponent<PlayerMissionContributionLedger>();
                ledger?.RecordContribution(ObjectiveFamily.PvP, 1, null, null, isCompanionContribution: false);
            }
        }
    }

    private void TryAwardCompanionDefeatProgress(
        NetworkRaidParticipant killer,
        PlayerCharacter killerAvatar,
        IMissionProgressDefeatSource source)
    {
        if (Runner == null) return;
        var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null || !spawnManager.TryGetRaidInitialAffiliations(out var affiliations)) return;

        if (!affiliations.TryGetTeam(killer.RaidParticipantId, out var teamId)) return;

        const float DuoProximityRadius = 35f;
        Vector2 killerPos = killerAvatar.transform.position;

        for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
        {
            if (!RaidParticipantId.TryCreate(i + 1, out var teammateId) || teammateId == killer.RaidParticipantId) continue;
            if (!affiliations.TryGetTeam(teammateId, out var candidateTeam) || candidateTeam != teamId) continue;

            if (spawnManager.TryGetRaidParticipant(teammateId, out var teammate) &&
                teammate.State == RaidParticipantState.Raiding &&
                teammate.TryResolveCurrentAvatar(out var avatarObj) &&
                avatarObj != null)
            {
                float distance = Vector2.Distance(killerPos, avatarObj.transform.position);
                if (distance <= DuoProximityRadius)
                {
                    var teammateLedger = teammate.GetComponent<PlayerMissionContributionLedger>();
                    if (teammateLedger != null)
                    {
                        teammateLedger.RecordContribution(
                            ObjectiveFamily.EliminacionPvE,
                            source.DefeatProgressAmount,
                            source.TargetId,
                            source.ZoneId,
                            isCompanionContribution: true);

                        if (!string.IsNullOrEmpty(source.TargetId))
                        {
                            teammateLedger.RecordContribution(
                                ObjectiveFamily.EliminacionEspecifica,
                                source.DefeatProgressAmount,
                                source.TargetId,
                                source.ZoneId,
                                isCompanionContribution: true);
                        }
                    }
                }
            }
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

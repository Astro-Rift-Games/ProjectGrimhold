using Fusion;
using UnityEngine;

/// <summary>
/// Projects the Input Authority player's replicated raid state into the local HUD.
/// It observes cached gameplay components, performs section-level dirty checking,
/// and never changes simulation or network state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidHudPresenter : MonoBehaviour
{
    [SerializeField]
    private RaidHudView _view;

    private PlayerCharacter _character;
    private PlayerCombatNetworkController _combatController;
    private PlayerLootReceiver _lootReceiver;
    private PlayerExtractionController _extractionController;
    private PlayerExtractionProgressController _extractionProgressController;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private EntityRegistry _entityRegistry;

    [SerializeField]
    [Min(0f)]
    private float _cancellationFeedbackDuration = 1.25f;

    private bool _isBound;

    private bool _hasHealthState;
    private float _observedHealth;
    private float _observedMaxHealth;
    private bool _observedDefeated;

    private bool _hasCombatState;
    private bool _observedAttackAvailable;
    private float _observedCooldownDuration;
    private float _observedCooldownRemaining;
    private float _observedCooldownFill;

    private int _observedLootSequence;

    private bool _hasExtractionState;
    private ExtractionState _observedExtractionState;
    private float _cancellationFeedbackUntil;

    [SerializeField]
    [Min(0f)]
    private float _quotaCompletedFeedbackDuration = 1.25f;

    private bool _hasProgressState;
    private bool _observedQuotaComplete;
    private float _quotaCompletedFeedbackUntil;

    /// <summary>
    /// Binds the local presentation to the current Input Authority player's sources.
    /// Missing runtime sources degrade their own section without affecting gameplay.
    /// </summary>
    /// <param name="extractionController">
    /// Existing network component that supplies the confirmed local extraction snapshot.
    /// </param>
    public void Bind(
        PlayerCharacter character,
        PlayerCombatNetworkController combatController,
        PlayerLootReceiver lootReceiver,
        PlayerExtractionController extractionController,
        PlayerExtractionProgressController extractionProgressController,
        ExtractionSanctuaryAssignmentService assignmentService,
        EntityRegistry entityRegistry)
    {
        Unbind();

        _character = character;
        _combatController = combatController;
        _lootReceiver = lootReceiver;
        _extractionController = extractionController;
        _extractionProgressController = extractionProgressController;
        _assignmentService = assignmentService;
        _entityRegistry = entityRegistry;
        _isBound = true;
        ResetObservedState();
        _view?.Clear();
        RefreshAll();
    }

    /// <summary>
    /// Backwards-compatible binding overload for presentation tests and callers that only
    /// provide the original HUD sources.
    /// </summary>
    public void Bind(
        PlayerCharacter character,
        PlayerCombatNetworkController combatController,
        PlayerLootReceiver lootReceiver,
        PlayerExtractionController extractionController)
    {
        Bind(character, combatController, lootReceiver, extractionController, null, null, null);
    }

    /// <summary>
    /// Clears all local references, pending reads and visual state.
    /// </summary>
    public void Unbind()
    {
        _character = null;
        _combatController = null;
        _lootReceiver = null;
        _extractionController = null;
        _extractionProgressController = null;
        _assignmentService = null;
        _entityRegistry = null;
        _isBound = false;
        ResetObservedState();
        _view?.Clear();
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        ResetObservedState();
        RefreshAll();
    }

    private void OnDisable()
    {
        ResetObservedState();
        _view?.Clear();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (!_isBound)
        {
            return;
        }

        RefreshHealth();
        RefreshCombat();
        RefreshInventoryIfNeeded();
        RefreshExtraction();
    }

    private void RefreshAll()
    {
        RefreshHealth();
        RefreshCombat();
        RefreshInventoryIfNeeded();
        RefreshExtraction();
    }

    private void RefreshHealth()
    {
        if (!IsSpawned(_character))
        {
            if (_hasHealthState)
            {
                _hasHealthState = false;
                _view?.ClearHealth();
                _view?.PresentDefeated(false);
            }
            return;
        }

        float health = _character.Health;
        float maxHealth = _character.MaxHealth;
        bool defeated = !_character.IsAlive;
        if (_hasHealthState &&
            Mathf.Approximately(_observedHealth, health) &&
            Mathf.Approximately(_observedMaxHealth, maxHealth) &&
            _observedDefeated == defeated)
        {
            return;
        }

        _hasHealthState = true;
        _observedHealth = health;
        _observedMaxHealth = maxHealth;
        _observedDefeated = defeated;
        _view?.PresentHealth(health, maxHealth);
        _view?.PresentDefeated(defeated);
    }

    private void RefreshCombat()
    {
        if (_combatController == null ||
            !_combatController.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status))
        {
            if (_hasCombatState)
            {
                _hasCombatState = false;
                _view?.ClearAttack();
            }
            return;
        }

        float duration = SanitizeDuration(status.CooldownDurationSeconds);
        float remaining = SanitizeRemainingTime(status.CooldownRemainingSeconds);
        float fill = NormalizeCooldown(duration, remaining);
        float visibleRemaining = RoundVisibleRemaining(remaining);
        if (_hasCombatState &&
            _observedAttackAvailable == status.IsAvailable &&
            Mathf.Approximately(_observedCooldownDuration, duration) &&
            Mathf.Approximately(_observedCooldownRemaining, visibleRemaining) &&
            Mathf.Approximately(_observedCooldownFill, fill))
        {
            return;
        }

        _hasCombatState = true;
        _observedAttackAvailable = status.IsAvailable;
        _observedCooldownDuration = duration;
        _observedCooldownRemaining = visibleRemaining;
        _observedCooldownFill = fill;
        _view?.PresentAttack(status.IsAvailable, visibleRemaining, fill);
    }

    private void RefreshInventoryIfNeeded()
    {
        if (!IsSpawned(_lootReceiver))
        {
            _view?.ClearInventory();
            return;
        }

        int currentSequence = _lootReceiver.LootChangeSequence;
        if (currentSequence == _observedLootSequence)
        {
            return;
        }

        _view?.PresentInventory(_lootReceiver.OccupiedSlotCount, _lootReceiver.SlotCapacity);
        _observedLootSequence = currentSequence;
    }

    private void RefreshExtraction()
    {
        ExtractionCountdownSnapshot countdown = default;
        bool hasCountdown = IsSpawned(_extractionController) &&
            _extractionController.TryGetProgress(out countdown);
        if (hasCountdown)
        {
            ApplyExtractionSnapshot(countdown);
        }
        else
        {
            _hasExtractionState = false;
            _cancellationFeedbackUntil = 0f;
        }

        ExtractionProgressSnapshot progress = default;
        bool hasProgress = IsSpawned(_extractionProgressController) &&
            _extractionProgressController.TryGetSnapshot(out progress);
        if (hasProgress)
        {
            ApplyProgressSnapshot(progress);
        }
        else
        {
            _hasProgressState = false;
            _quotaCompletedFeedbackUntil = 0f;
        }

        if (hasCountdown && countdown.State == ExtractionState.Extracted)
        {
            _view?.PresentExtractionCompleted();
            return;
        }

        if (hasCountdown && countdown.State == ExtractionState.InProgress)
        {
            _view?.PresentExtractionCountdown(SanitizeExtractionRemaining(countdown.RemainingSeconds));
            return;
        }

        if (_cancellationFeedbackUntil > Time.unscaledTime)
        {
            _view?.PresentExtractionCancelled();
            return;
        }

        bool hasSanctuary = TryGetSanctuaryPresentation(
            out ExtractionRitualState ritualState,
            out ExtractionRitualSnapshot ritual);
        if (hasSanctuary)
        {
            switch (ritualState)
            {
                case ExtractionRitualState.Completed:
                    _view?.PresentSanctuaryEnabled();
                    return;
                case ExtractionRitualState.InProgress:
                    _view?.PresentRitualProgress(SanitizeRitualRemaining(ritual.RemainingSeconds));
                    return;
                case ExtractionRitualState.Cancelled:
                    _view?.PresentRitualCancelled();
                    return;
            }
        }

        if (_quotaCompletedFeedbackUntil > Time.unscaledTime)
        {
            _view?.PresentQuotaCompleted();
            return;
        }

        if (hasSanctuary)
        {
            _view?.PresentSanctuaryAssigned();
            return;
        }

        if (hasProgress)
        {
            if (progress.IsQuotaComplete)
            {
                _view?.PresentQuotaCompleted();
            }
            else
            {
                _view?.PresentExtractionProgress(progress.CurrentProgress, progress.Quota);
            }

            return;
        }

        _view?.PresentExtractionUnavailable();
    }

    private void ApplyExtractionSnapshot(ExtractionCountdownSnapshot snapshot)
    {
        ExtractionState previousState = _observedExtractionState;
        bool hadObservedState = _hasExtractionState;
        _hasExtractionState = true;
        _observedExtractionState = snapshot.State;

        if (hadObservedState &&
            previousState == ExtractionState.InProgress &&
            snapshot.State == ExtractionState.None)
        {
            float duration = SanitizeDuration(_cancellationFeedbackDuration);
            if (duration > 0f)
            {
                _cancellationFeedbackUntil = Time.unscaledTime + duration;
                _view?.PresentExtractionCancelled();
            }
            else
            {
                _cancellationFeedbackUntil = 0f;
                _view?.PresentExtractionUnavailable();
            }

            return;
        }

        if (snapshot.State == ExtractionState.None &&
            _cancellationFeedbackUntil > Time.unscaledTime)
        {
            return;
        }

        _cancellationFeedbackUntil = 0f;
        PresentExtractionSnapshot(snapshot);
    }

    private void ApplyProgressSnapshot(ExtractionProgressSnapshot snapshot)
    {
        if (!_hasProgressState)
        {
            _hasProgressState = true;
            _observedQuotaComplete = snapshot.IsQuotaComplete;
            return;
        }

        if (!_observedQuotaComplete && snapshot.IsQuotaComplete)
        {
            float duration = SanitizeDuration(_quotaCompletedFeedbackDuration);
            _quotaCompletedFeedbackUntil = duration > 0f
                ? Time.unscaledTime + duration
                : 0f;
        }

        _observedQuotaComplete = snapshot.IsQuotaComplete;
    }

    private bool TryGetSanctuaryPresentation(
        out ExtractionRitualState ritualState,
        out ExtractionRitualSnapshot ritualSnapshot)
    {
        ritualState = default;
        ritualSnapshot = default;
        if (!IsSpawned(_extractionProgressController) ||
            _assignmentService == null ||
            _entityRegistry == null ||
            _extractionProgressController.Id.Value == 0)
        {
            return false;
        }

        SanctuaryAssignmentResult assignment = _assignmentService.TryGetAssignment(_extractionProgressController.Id);
        if (!assignment.Success || assignment.SanctuaryId.Value == 0 ||
            !_entityRegistry.TryGetExtractionSanctuary(assignment.SanctuaryId, out IExtractionSanctuary sanctuary) ||
            sanctuary == null || !sanctuary.TryGetRitualProgress(out ritualSnapshot))
        {
            return false;
        }

        ritualState = ritualSnapshot.State;
        return true;
    }

    private void PresentExtractionSnapshot(ExtractionCountdownSnapshot snapshot)
    {
        switch (snapshot.State)
        {
            case ExtractionState.None:
                _view?.PresentExtractionUnavailable();
                break;
            case ExtractionState.InProgress:
                _view?.PresentExtractionCountdown(SanitizeExtractionRemaining(snapshot.RemainingSeconds));
                break;
            case ExtractionState.Extracted:
                _view?.PresentExtractionCompleted();
                break;
            default:
                _view?.PresentExtractionUnavailable();
                break;
        }
    }

    private void ResetObservedState()
    {
        _hasHealthState = false;
        _observedHealth = 0f;
        _observedMaxHealth = 0f;
        _observedDefeated = false;
        _hasCombatState = false;
        _observedAttackAvailable = false;
        _observedCooldownDuration = 0f;
        _observedCooldownRemaining = 0f;
        _observedCooldownFill = 0f;
        _observedLootSequence = int.MinValue;
        _hasExtractionState = false;
        _observedExtractionState = ExtractionState.None;
        _cancellationFeedbackUntil = 0f;
        _hasProgressState = false;
        _observedQuotaComplete = false;
        _quotaCompletedFeedbackUntil = 0f;
    }

    private static float NormalizeCooldown(float durationSeconds, float remainingSeconds)
    {
        if (!IsFinite(durationSeconds) || !IsFinite(remainingSeconds) ||
            durationSeconds <= 0f || remainingSeconds < 0f)
        {
            return 0f;
        }

        float normalized = remainingSeconds / durationSeconds;
        return IsFinite(normalized) ? Mathf.Clamp01(normalized) : 0f;
    }

    private static float SanitizeRemainingTime(float remainingSeconds)
    {
        return IsFinite(remainingSeconds) && remainingSeconds > 0f
            ? remainingSeconds
            : 0f;
    }

    private static float SanitizeDuration(float durationSeconds)
    {
        return IsFinite(durationSeconds) && durationSeconds > 0f
            ? durationSeconds
            : 0f;
    }

    private static float SanitizeExtractionRemaining(float remainingSeconds)
    {
        return IsFinite(remainingSeconds) && remainingSeconds > 0f
            ? Mathf.Ceil(remainingSeconds * 10f) * 0.1f
            : 0f;
    }

    private static float SanitizeRitualRemaining(float remainingSeconds)
    {
        return IsFinite(remainingSeconds) && remainingSeconds > 0f
            ? Mathf.Ceil(remainingSeconds * 10f) * 0.1f
            : 0f;
    }

    private static float RoundVisibleRemaining(float remainingSeconds)
    {
        return remainingSeconds > 0f
            ? Mathf.Ceil(remainingSeconds * 10f) * 0.1f
            : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsSpawned(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.Object != null && behaviour.Object.IsValid;
    }
}

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
    private PlayerStaminaNetworkController _staminaController;
    private PlayerCombatNetworkController _combatController;
    private PlayerWeaponEquipmentNetworkController _weaponEquipmentController;
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

    private bool _hasStaminaState;
    private float _observedCurrentStamina;
    private float _observedMaximumStamina;
    private bool _observedExhausted;

    private bool _hasCombatState;
    private bool _observedAttackAvailable;
    private float _observedCooldownDuration;
    private float _observedCooldownRemaining;
    private float _observedCooldownFill;
    private Sprite _observedWeaponIcon;

    private int _observedLootSequence;

    private enum RitualStatusKind
    {
        Unavailable,
        Extracted,
        Countdown,
        Cancelled,
        RitualCompleted,
        RitualInProgress,
        RitualCancelled,
    }

    private bool _hasExtractionState;
    private ExtractionState _observedExtractionState;
    private float _cancellationFeedbackUntil;

    private bool _hasRitualStatus;
    private RitualStatusKind _observedRitualStatus;
    private float _observedRitualStatusSeconds;

    private bool _hasQuotaState;
    private int _observedQuotaProgress;
    private int _observedQuotaTarget;
    private bool _observedQuotaComplete;

    private bool _hasProgressState;
    private int _observedProgressCurrent;
    private int _observedProgressQuota;

    private bool _hasSanctuaryState;

    /// <summary>
    /// Binds the local presentation to the current Input Authority player's sources.
    /// Missing runtime sources degrade their own section without affecting gameplay.
    /// </summary>
    /// <param name="extractionController">
    /// Existing network component that supplies the confirmed local extraction snapshot.
    /// </param>
    public void Bind(
        PlayerCharacter character,
        PlayerStaminaNetworkController staminaController,
        PlayerCombatNetworkController combatController,
        PlayerWeaponEquipmentNetworkController weaponEquipmentController,
        PlayerLootReceiver lootReceiver,
        PlayerExtractionController extractionController,
        PlayerExtractionProgressController extractionProgressController,
        ExtractionSanctuaryAssignmentService assignmentService,
        EntityRegistry entityRegistry)
    {
        Unbind();

        _character = character;
        _staminaController = staminaController;
        _combatController = combatController;
        _weaponEquipmentController = weaponEquipmentController;
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
        Bind(character, null, combatController, null, lootReceiver, extractionController, null, null, null);
    }

    /// <summary>
    /// Backwards-compatible binding overload for callers that already provide the
    /// extended extraction and assignment sources but predate weapon presentation.
    /// </summary>
    public void Bind(
        PlayerCharacter character,
        PlayerCombatNetworkController combatController,
        PlayerLootReceiver lootReceiver,
        PlayerExtractionController extractionController,
        PlayerExtractionProgressController extractionProgressController,
        ExtractionSanctuaryAssignmentService assignmentService,
        EntityRegistry entityRegistry)
    {
        Bind(
            character,
            null,
            combatController,
            null,
            lootReceiver,
            extractionController,
            extractionProgressController,
            assignmentService,
            entityRegistry);
    }

    /// <summary>
    /// Clears all local references, pending reads and visual state.
    /// </summary>
    public void Unbind()
    {
        _character = null;
        _staminaController = null;
        _combatController = null;
        _weaponEquipmentController = null;
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
        RefreshStamina();
        RefreshCombat();
        RefreshInventoryIfNeeded();
        RefreshExtraction();
    }

    private void RefreshAll()
    {
        RefreshHealth();
        RefreshStamina();
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

    private void RefreshStamina()
    {
        if (!IsSpawned(_staminaController) ||
            !_staminaController.TryGetMaximumStamina(out float maximumStamina))
        {
            if (_hasStaminaState)
            {
                _hasStaminaState = false;
                _view?.ClearStamina();
            }

            return;
        }

        float currentStamina = _staminaController.CurrentStamina;
        bool isExhausted = _staminaController.IsExhausted;
        if (_hasStaminaState &&
            Mathf.Approximately(_observedCurrentStamina, currentStamina) &&
            Mathf.Approximately(_observedMaximumStamina, maximumStamina) &&
            _observedExhausted == isExhausted)
        {
            return;
        }

        _hasStaminaState = true;
        _observedCurrentStamina = currentStamina;
        _observedMaximumStamina = maximumStamina;
        _observedExhausted = isExhausted;
        _view?.PresentStamina(currentStamina, maximumStamina, isExhausted);
    }

    private void RefreshCombat()
    {
        if (_combatController == null ||
            !_combatController.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status))
        {
            if (_hasCombatState)
            {
                _hasCombatState = false;
                _observedWeaponIcon = null;
                _view?.ClearAttack();
            }
            return;
        }

        float duration = SanitizeDuration(status.CooldownDurationSeconds);
        float remaining = SanitizeRemainingTime(status.CooldownRemainingSeconds);
        float fill = NormalizeCooldown(duration, remaining);
        float visibleRemaining = RoundVisibleRemaining(remaining);
        Sprite weaponIcon = null;
        if (_weaponEquipmentController != null &&
            _weaponEquipmentController.TryGetEquippedDefinition(out LootDefinition equippedDefinition))
        {
            weaponIcon = equippedDefinition.Icon ?? equippedDefinition.WorldSprite;
        }
        if (_hasCombatState &&
            _observedAttackAvailable == status.IsAvailable &&
            Mathf.Approximately(_observedCooldownDuration, duration) &&
            Mathf.Approximately(_observedCooldownRemaining, visibleRemaining) &&
            Mathf.Approximately(_observedCooldownFill, fill) &&
            _observedWeaponIcon == weaponIcon)
        {
            return;
        }

        _hasCombatState = true;
        _observedAttackAvailable = status.IsAvailable;
        _observedCooldownDuration = duration;
        _observedCooldownRemaining = visibleRemaining;
        _observedCooldownFill = fill;
        _observedWeaponIcon = weaponIcon;
        _view?.PresentAttack(status.IsAvailable, visibleRemaining, fill, weaponIcon);
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

        ExtractionProgressSnapshot progress = default;
        bool hasProgress = IsSpawned(_extractionProgressController) &&
            _extractionProgressController.TryGetSnapshot(out progress);

        bool hasSanctuary = TryGetSanctuaryPresentation(out ExtractionRitualSnapshot ritual);

        RefreshQuotaSection(hasProgress, progress);
        RefreshProgressSection(hasProgress, progress);
        RefreshSanctuarySection(hasSanctuary);
        RefreshRitualStatusSection(hasCountdown, countdown, hasSanctuary, ritual);
    }

    private void RefreshQuotaSection(bool hasProgress, ExtractionProgressSnapshot progress)
    {
        if (!hasProgress)
        {
            if (_hasQuotaState)
            {
                _hasQuotaState = false;
                _view?.ClearQuota();
            }

            return;
        }

        if (_hasQuotaState &&
            _observedQuotaProgress == progress.CurrentProgress &&
            _observedQuotaTarget == progress.Quota &&
            _observedQuotaComplete == progress.IsQuotaComplete)
        {
            return;
        }

        _hasQuotaState = true;
        _observedQuotaProgress = progress.CurrentProgress;
        _observedQuotaTarget = progress.Quota;
        _observedQuotaComplete = progress.IsQuotaComplete;
        if (progress.IsQuotaComplete)
        {
            _view?.PresentQuotaCompleted();
        }
        else
        {
            _view?.PresentExtractionProgress(progress.CurrentProgress, progress.Quota);
        }
    }

    private void RefreshProgressSection(bool hasProgress, ExtractionProgressSnapshot progress)
    {
        if (!hasProgress ||
            !ExpeditionProgressMath.TryGetFraction(progress.CurrentProgress, progress.Quota, out float fraction))
        {
            if (_hasProgressState)
            {
                _hasProgressState = false;
                _view?.ClearExpeditionProgress();
            }

            return;
        }

        if (_hasProgressState &&
            _observedProgressCurrent == progress.CurrentProgress &&
            _observedProgressQuota == progress.Quota)
        {
            return;
        }

        _hasProgressState = true;
        _observedProgressCurrent = progress.CurrentProgress;
        _observedProgressQuota = progress.Quota;
        _view?.PresentExpeditionProgress(fraction);
    }

    private void RefreshSanctuarySection(bool hasSanctuary)
    {
        if (_hasSanctuaryState == hasSanctuary)
        {
            return;
        }

        _hasSanctuaryState = hasSanctuary;
        if (hasSanctuary)
        {
            _view?.PresentSanctuaryAssigned();
        }
        else
        {
            _view?.ClearSanctuary();
        }
    }

    private void RefreshRitualStatusSection(
        bool hasCountdown,
        ExtractionCountdownSnapshot countdown,
        bool hasSanctuary,
        ExtractionRitualSnapshot ritual)
    {
        if (hasCountdown)
        {
            ObserveExtractionSnapshot(countdown);
        }
        else
        {
            _hasExtractionState = false;
            _cancellationFeedbackUntil = 0f;
        }

        ResolveRitualStatus(
            hasCountdown,
            countdown,
            hasSanctuary,
            ritual,
            out RitualStatusKind status,
            out float seconds);
        if (_hasRitualStatus &&
            _observedRitualStatus == status &&
            Mathf.Approximately(_observedRitualStatusSeconds, seconds))
        {
            return;
        }

        _hasRitualStatus = true;
        _observedRitualStatus = status;
        _observedRitualStatusSeconds = seconds;
        switch (status)
        {
            case RitualStatusKind.Extracted:
                _view?.PresentExtractionCompleted();
                break;
            case RitualStatusKind.Countdown:
                _view?.PresentExtractionCountdown(seconds);
                break;
            case RitualStatusKind.Cancelled:
                _view?.PresentExtractionCancelled();
                break;
            case RitualStatusKind.RitualCompleted:
                _view?.PresentSanctuaryEnabled();
                break;
            case RitualStatusKind.RitualInProgress:
                _view?.PresentRitualProgress(seconds);
                break;
            case RitualStatusKind.RitualCancelled:
                _view?.PresentRitualCancelled();
                break;
            default:
                _view?.PresentExtractionUnavailable();
                break;
        }
    }

    private void ResolveRitualStatus(
        bool hasCountdown,
        ExtractionCountdownSnapshot countdown,
        bool hasSanctuary,
        ExtractionRitualSnapshot ritual,
        out RitualStatusKind status,
        out float seconds)
    {
        seconds = 0f;
        if (hasCountdown && countdown.State == ExtractionState.Extracted)
        {
            status = RitualStatusKind.Extracted;
            return;
        }

        if (hasCountdown && countdown.State == ExtractionState.InProgress)
        {
            status = RitualStatusKind.Countdown;
            seconds = SanitizeExtractionRemaining(countdown.RemainingSeconds);
            return;
        }

        if (_cancellationFeedbackUntil > Time.unscaledTime)
        {
            status = RitualStatusKind.Cancelled;
            return;
        }

        status = RitualStatusKind.Unavailable;
        if (!hasSanctuary)
        {
            return;
        }

        switch (ritual.State)
        {
            case ExtractionRitualState.Completed:
                status = RitualStatusKind.RitualCompleted;
                break;
            case ExtractionRitualState.InProgress:
                status = RitualStatusKind.RitualInProgress;
                seconds = SanitizeRitualRemaining(ritual.RemainingSeconds);
                break;
            case ExtractionRitualState.Cancelled:
                status = RitualStatusKind.RitualCancelled;
                break;
        }
    }

    private void ObserveExtractionSnapshot(ExtractionCountdownSnapshot snapshot)
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
            _cancellationFeedbackUntil = duration > 0f
                ? Time.unscaledTime + duration
                : 0f;
            return;
        }

        if (snapshot.State == ExtractionState.None &&
            _cancellationFeedbackUntil > Time.unscaledTime)
        {
            return;
        }

        _cancellationFeedbackUntil = 0f;
    }

    private bool TryGetSanctuaryPresentation(out ExtractionRitualSnapshot ritualSnapshot)
    {
        ritualSnapshot = default;
        if (!IsSpawned(_extractionProgressController) ||
            _assignmentService == null ||
            _entityRegistry == null ||
            _extractionProgressController.Id.Value == 0)
        {
            return false;
        }

        SanctuaryAssignmentResult assignment = _assignmentService.TryGetAssignment(_extractionProgressController.Id);
        return assignment.Success && assignment.SanctuaryId.Value != 0 &&
            _entityRegistry.TryGetExtractionSanctuary(assignment.SanctuaryId, out IExtractionSanctuary sanctuary) &&
            sanctuary != null && sanctuary.TryGetRitualProgress(out ritualSnapshot);
    }

    private void ResetObservedState()
    {
        _hasHealthState = false;
        _observedHealth = 0f;
        _observedMaxHealth = 0f;
        _observedDefeated = false;
        _hasStaminaState = false;
        _observedCurrentStamina = 0f;
        _observedMaximumStamina = 0f;
        _observedExhausted = false;
        _hasCombatState = false;
        _observedAttackAvailable = false;
        _observedCooldownDuration = 0f;
        _observedCooldownRemaining = 0f;
        _observedCooldownFill = 0f;
        _observedWeaponIcon = null;
        _observedLootSequence = int.MinValue;
        _hasExtractionState = false;
        _observedExtractionState = ExtractionState.None;
        _cancellationFeedbackUntil = 0f;
        _hasRitualStatus = false;
        _observedRitualStatus = RitualStatusKind.Unavailable;
        _observedRitualStatusSeconds = 0f;
        _hasQuotaState = false;
        _observedQuotaProgress = 0;
        _observedQuotaTarget = 0;
        _observedQuotaComplete = false;
        _hasProgressState = false;
        _observedProgressCurrent = 0;
        _observedProgressQuota = 0;
        _hasSanctuaryState = false;
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

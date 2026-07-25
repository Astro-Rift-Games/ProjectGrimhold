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
    private Camera _presentationCamera;

    [SerializeField]
    private Vector3 _cooldownWorldOffset = new(0f, 1.25f, 0f);

    private bool _isBound;
    private bool _classResolved;

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

    /// <summary>
    /// Binds the local presentation to the current Input Authority player's sources.
    /// Missing runtime sources degrade their own section without affecting gameplay.
    /// </summary>
    public void Bind(
        PlayerCharacter character,
        PlayerCombatNetworkController combatController,
        PlayerLootReceiver lootReceiver)
    {
        Unbind();

        _character = character;
        _combatController = combatController;
        _lootReceiver = lootReceiver;
        _presentationCamera = Camera.main;
        _isBound = true;
        ResetObservedState();
        _view?.Clear();
        RefreshAll();
    }

    /// <summary>
    /// Supplies the locally selected class after the main HUD binding exists.
    /// The first supported class resolves presentation for the current binding.
    /// </summary>
    public void SetPlayerClass(PlayerClassId playerClass)
    {
        if (!_isBound || _classResolved ||
            !TryGetPlayerClassDisplayName(playerClass, out string displayName))
        {
            return;
        }

        _classResolved = true;
        _view?.PresentClass(displayName);
    }

    /// <summary>
    /// Clears all local references, pending reads and visual state.
    /// </summary>
    public void Unbind()
    {
        _character = null;
        _combatController = null;
        _lootReceiver = null;
        _presentationCamera = null;
        _isBound = false;
        _classResolved = false;
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
        Unbind();
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
    }

    private void LateUpdate()
    {
        if (!_isBound || _presentationCamera == null || !IsSpawned(_character))
        {
            return;
        }

        Vector3 screenPosition = _presentationCamera.WorldToScreenPoint(
            _character.transform.position + _cooldownWorldOffset);
        if (screenPosition.z >= 0f)
        {
            _view?.SetCooldownScreenPosition(screenPosition);
        }
    }

    private void RefreshAll()
    {
        _view?.PresentExtractionUnavailable();
        RefreshHealth();
        RefreshCombat();
        RefreshInventoryIfNeeded();
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
    }

    private static bool TryGetPlayerClassDisplayName(
        PlayerClassId playerClass,
        out string displayName)
    {
        switch (playerClass)
        {
            case PlayerClassId.Melee:
                displayName = "Caballero";
                return true;
            case PlayerClassId.Ranged:
                displayName = "Mago";
                return true;
            default:
                displayName = null;
                return false;
        }
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

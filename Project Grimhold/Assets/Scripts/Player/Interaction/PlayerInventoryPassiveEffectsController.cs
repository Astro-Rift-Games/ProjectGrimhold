using System;
using UnityEngine;

/// <summary>
/// Framework de efectos pasivos para tesoros en inventario (Doc 14 Sección 10).
/// Observa el inventario replicado del jugador y expone modificadores autoritativos y replicados.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInventoryPassiveEffectsController : MonoBehaviour
{
    public static readonly LootId ChaosCoinId = new("TRE_CHAOS_COIN");
    public static readonly LootId PilgrimLightId = new("TRE_PILGRIM_LIGHT");
    public static readonly LootId SanctuaryHeartId = new("TRE_SANCTUARY_HEART");
    public static readonly LootId VoidBellId = new("TRE_VOID_BELL");
    public static readonly LootId FamineIdolId = new("TRE_FAMINE_IDOL");
    public static readonly LootId WandererEyeId = new("TRE_WANDERER_EYE");

    [Header("Modifiers Configuration")]
    [SerializeField, Range(0.1f, 1f)]
    [Tooltip("Multiplicador de vida máxima mientras se porta la Moneda del Caos (-20%).")]
    private float _chaosCoinMaxHealthMultiplier = 0.8f;

    [SerializeField, Min(1f)]
    [Tooltip("Multiplicador de regeneración de Stamina con la Luz del Peregrino (+35%).")]
    private float _pilgrimLightStaminaRegenMultiplier = 1.35f;

    [SerializeField, Range(0.1f, 1f)]
    [Tooltip("Multiplicador de daño recibido con el Corazón de Santuario (-15%).")]
    private float _sanctuaryHeartDamageTakenMultiplier = 0.85f;

    [SerializeField, Min(1f)]
    [Tooltip("Multiplicador de velocidad de sprint con la Campana del Vacío (+15%).")]
    private float _voidBellSprintSpeedMultiplier = 1.15f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Intervalo en segundos entre campanadas al esprintar.")]
    private float _voidBellChimeIntervalSeconds = 2.0f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Radio audible en metros de la campanada.")]
    private float _voidBellAudibleRadius = 15f;

    [SerializeField, Min(1f)]
    [Tooltip("Multiplicador de rango de detección y agresividad de IA con el Ídolo de la Hambruna (+50%).")]
    private float _famineIdolAggroRangeMultiplier = 1.5f;

    [SerializeField, Range(0.1f, 1f)]
    [Tooltip("Multiplicador de visión con el Ojo del Caminante (-25%).")]
    private float _wandererEyeVisionRadiusMultiplier = 0.75f;

    private bool _hasChaosCoin;
    private bool _hasPilgrimLight;
    private bool _hasSanctuaryHeart;
    private bool _hasVoidBell;
    private bool _hasFamineIdol;
    private bool _hasWandererEye;

    public bool HasChaosCoin { get => GetUpdated(ref _hasChaosCoin); internal set => _hasChaosCoin = value; }
    public bool HasPilgrimLight { get => GetUpdated(ref _hasPilgrimLight); internal set => _hasPilgrimLight = value; }
    public bool HasSanctuaryHeart { get => GetUpdated(ref _hasSanctuaryHeart); internal set => _hasSanctuaryHeart = value; }
    public bool HasVoidBell { get => GetUpdated(ref _hasVoidBell); internal set => _hasVoidBell = value; }
    public bool HasFamineIdol { get => GetUpdated(ref _hasFamineIdol); internal set => _hasFamineIdol = value; }
    public bool HasWandererEye { get => GetUpdated(ref _hasWandererEye); internal set => _hasWandererEye = value; }

    private float _nextVoidBellChimeTime;

    private PlayerLootReceiver _lootReceiver;
    private PlayerCharacter _playerCharacter;
    private PlayerMovementNetworkController _movementController;
    private int _lastObservedLootSequence = -1;

    public float MaxHealthMultiplier => HasChaosCoin ? _chaosCoinMaxHealthMultiplier : 1f;
    public float StaminaRegenMultiplier => HasPilgrimLight ? _pilgrimLightStaminaRegenMultiplier : 1f;
    public float DamageTakenMultiplier => HasSanctuaryHeart ? _sanctuaryHeartDamageTakenMultiplier : 1f;
    public float SprintSpeedMultiplier => HasVoidBell ? _voidBellSprintSpeedMultiplier : 1f;
    public float VoidBellAudibleRadius => _voidBellAudibleRadius;
    public float AggroRangeMultiplier => HasFamineIdol ? _famineIdolAggroRangeMultiplier : 1f;
    public float VisionRadiusMultiplier => HasWandererEye ? _wandererEyeVisionRadiusMultiplier : 1f;
    public bool IsMinimapBlocked => HasWandererEye;

    public event Action OnVoidBellChime;

    private void Awake() => CacheDependencies();

    private void Start()
    {
        CacheDependencies();
        EvaluatePassiveTreasures();
    }

    private void Update()
    {
        UpdatePassivesIfNeeded();

        if (HasVoidBell && _playerCharacter != null && _playerCharacter.IsAlive)
        {
            bool isSprinting = _movementController != null && _movementController.IsSprinting && _movementController.IsMoving;
            if (isSprinting)
            {
                if (Time.time >= _nextVoidBellChimeTime)
                {
                    _nextVoidBellChimeTime = Time.time + _voidBellChimeIntervalSeconds;
                    OnVoidBellChime?.Invoke();
                }
            }
        }
    }

    private bool GetUpdated(ref bool flag)
    {
        UpdatePassivesIfNeeded();
        return flag;
    }

    public void UpdatePassivesIfNeeded()
    {
        if (_lootReceiver == null)
        {
            CacheDependencies();
        }

        if (_lootReceiver != null)
        {
            int currentSequence = _lootReceiver.LootChangeSequence;
            if (currentSequence != _lastObservedLootSequence)
            {
                _lastObservedLootSequence = currentSequence;
                EvaluatePassiveTreasures();
            }
        }
    }

    /// <summary>
    /// Evalúa la presencia de cada tesoro pasivo en el inventario.
    /// No acumula efectos duplicados (presencia binaria > 0).
    /// </summary>
    public void EvaluatePassiveTreasures()
    {
        if (_lootReceiver == null)
        {
            _hasChaosCoin = false;
            _hasPilgrimLight = false;
            _hasSanctuaryHeart = false;
            _hasVoidBell = false;
            _hasFamineIdol = false;
            _hasWandererEye = false;
            return;
        }

        _hasChaosCoin = _lootReceiver.GetLootAmount(ChaosCoinId) > 0;
        _hasPilgrimLight = _lootReceiver.GetLootAmount(PilgrimLightId) > 0;
        _hasSanctuaryHeart = _lootReceiver.GetLootAmount(SanctuaryHeartId) > 0;
        _hasVoidBell = _lootReceiver.GetLootAmount(VoidBellId) > 0;
        _hasFamineIdol = _lootReceiver.GetLootAmount(FamineIdolId) > 0;
        _hasWandererEye = _lootReceiver.GetLootAmount(WandererEyeId) > 0;
    }

    private void CacheDependencies()
    {
        if (_lootReceiver == null) _lootReceiver = GetComponent<PlayerLootReceiver>();
        if (_playerCharacter == null) _playerCharacter = GetComponent<PlayerCharacter>();
        if (_movementController == null) _movementController = GetComponent<PlayerMovementNetworkController>();
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}

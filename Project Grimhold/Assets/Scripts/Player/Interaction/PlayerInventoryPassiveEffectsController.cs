using System;
using System.Collections.Generic;
using Fusion;
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

    private const int MaxAggroOverlapResults = 64;
    private const int PulseRingSegments = 40;
    private const float PulseDurationSeconds = 0.65f;
    private const float PulseStartRadius = 0.4f;
    private const float PulseEndRadius = 4.0f;
    private const float PulseInitialAlpha = 0.85f;
    private static readonly int DefaultVoidBellAggroMask = (1 << 7) | (1 << 10) | (1 << 0);

    private static AudioClip s_provisionalVoidBellClip;
    private static Material s_provisionalPulseMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_provisionalVoidBellClip = null;
        s_provisionalPulseMaterial = null;
        ObserverTransformOverrideForTesting = null;
    }

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

    [Header("Void Bell Aggro & Presentation")]
    [SerializeField]
    [Tooltip("Capas físicas escaneadas al emitir el pulso de aggro de la Campana del Vacío (0 usa Character | Enemies | Default).")]
    private LayerMask _voidBellAggroLayerMask;

    [SerializeField]
    [Tooltip("SFX opcional de la campanada; si no está asignado se emplea un timbre metálico provisional sintetizado.")]
    private CustomClip _voidBellChimeSfx;

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

    private readonly Collider2D[] _aggroOverlapBuffer = new Collider2D[MaxAggroOverlapResults];
    private readonly HashSet<IAggroReceiver> _alertedReceivers = new();
    private Vector3[] _pulsePositions;
    private LineRenderer _pulseRingRenderer;
    private Vector3 _pulseCenter;
    private bool _isPulseActive;
    private float _pulseElapsed;

    private PlayerLootReceiver _lootReceiver;
    private PlayerCharacter _playerCharacter;
    private PlayerMovementNetworkController _movementController;
    private int _lastObservedLootSequence = -1;
    private bool _lastObservedAliveState = true;

    internal bool? AliveOverrideForTesting { get; set; }
    internal static Transform ObserverTransformOverrideForTesting { get; set; }

    public int PresentedVoidBellChimeCount { get; private set; }

    public float MaxHealthMultiplier => HasChaosCoin ? _chaosCoinMaxHealthMultiplier : 1f;
    public float StaminaRegenMultiplier => HasPilgrimLight ? _pilgrimLightStaminaRegenMultiplier : 1f;
    public float DamageTakenMultiplier => HasSanctuaryHeart ? _sanctuaryHeartDamageTakenMultiplier : 1f;
    public float SprintSpeedMultiplier => HasVoidBell ? _voidBellSprintSpeedMultiplier : 1f;
    public float VoidBellChimeIntervalSeconds => _voidBellChimeIntervalSeconds;
    public float VoidBellAudibleRadius => _voidBellAudibleRadius;
    public float AggroRangeMultiplier => HasFamineIdol ? _famineIdolAggroRangeMultiplier : 1f;
    public float VisionRadiusMultiplier => HasWandererEye ? _wandererEyeVisionRadiusMultiplier : 1f;
    public bool IsMinimapBlocked => HasWandererEye;

    public bool IsSprintingWithVoidBell =>
        HasVoidBell &&
        IsOwnerAlive &&
        (_playerCharacter == null || !_playerCharacter.IsDowned) &&
        _movementController != null &&
        _movementController.IsSprinting &&
        _movementController.IsMoving;

    private bool IsOwnerAlive => AliveOverrideForTesting ?? (_playerCharacter == null || _playerCharacter.IsAlive);

    private bool HasSpawnedLootReceiver =>
        _lootReceiver != null && _lootReceiver.Object != null && _lootReceiver.Object.IsValid;

    public event Action OnVoidBellChime;

    private void Awake() => CacheDependencies();

    private void Start()
    {
        CacheDependencies();
        EvaluatePassiveTreasures();
    }

    private void OnDisable()
    {
        StopPulseRingImmediate();
    }

    private void Update()
    {
        UpdatePassivesIfNeeded();
        UpdatePulseRingVisual(Time.deltaTime);
    }

    private bool GetUpdated(ref bool flag)
    {
        UpdatePassivesIfNeeded();
        return flag;
    }

    public void UpdatePassivesIfNeeded()
    {
        if (_lootReceiver == null || _playerCharacter == null || _movementController == null)
        {
            CacheDependencies();
        }

        bool currentAlive = IsOwnerAlive;
        int currentSequence = HasSpawnedLootReceiver ? _lootReceiver.LootChangeSequence : -1;
        if (currentSequence != _lastObservedLootSequence || currentAlive != _lastObservedAliveState)
        {
            _lastObservedLootSequence = currentSequence;
            _lastObservedAliveState = currentAlive;
            EvaluatePassiveTreasures();
        }
    }

    /// <summary>
    /// Evalúa la presencia de cada tesoro pasivo en el inventario.
    /// No acumula efectos duplicados (presencia binaria > 0) y revoca inmediatamente al morir.
    /// </summary>
    public void EvaluatePassiveTreasures()
    {
        if (!HasSpawnedLootReceiver || !IsOwnerAlive)
        {
            _hasChaosCoin = false;
            _hasPilgrimLight = false;
            _hasSanctuaryHeart = false;
            _hasVoidBell = false;
            _hasFamineIdol = false;
            _hasWandererEye = false;
            StopPulseRingImmediate();
            return;
        }

        _hasChaosCoin = _lootReceiver.GetLootAmount(ChaosCoinId) > 0;
        _hasPilgrimLight = _lootReceiver.GetLootAmount(PilgrimLightId) > 0;
        _hasSanctuaryHeart = _lootReceiver.GetLootAmount(SanctuaryHeartId) > 0;
        _hasVoidBell = _lootReceiver.GetLootAmount(VoidBellId) > 0;
        _hasFamineIdol = _lootReceiver.GetLootAmount(FamineIdolId) > 0;
        _hasWandererEye = _lootReceiver.GetLootAmount(WandererEyeId) > 0;

        if (!_hasVoidBell)
        {
            StopPulseRingImmediate();
        }
    }

    /// <summary>
    /// Escanea de forma autoritativa y sin asignaciones por tick a los receptores de aggro dentro
    /// del radio audible de la Campana del Vacío y los alerta hacia el portador.
    /// </summary>
    public int ExecuteAuthoritativeVoidBellAlert(EntityId playerId, Transform originTransform = null)
    {
        Transform sourceTransform = originTransform != null ? originTransform : transform;
        int mask = _voidBellAggroLayerMask.value != 0
            ? _voidBellAggroLayerMask.value
            : DefaultVoidBellAggroMask;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = true
        };

        int hitCount = Physics2D.OverlapCircle(
            (Vector2)sourceTransform.position,
            _voidBellAudibleRadius,
            filter,
            _aggroOverlapBuffer);

        if (hitCount <= 0)
        {
            return 0;
        }

        _alertedReceivers.Clear();
        int alertedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _aggroOverlapBuffer[i];
            _aggroOverlapBuffer[i] = null;
            if (hit == null || hit.transform.IsChildOf(sourceTransform))
            {
                continue;
            }

            IAggroReceiver receiver = hit.GetComponentInParent<IAggroReceiver>();
            if (receiver == null || !_alertedReceivers.Add(receiver))
            {
                continue;
            }

            if (receiver is ICharacter character && !character.IsAlive)
            {
                continue;
            }

            receiver.ReceiveAggroAlert(playerId, sourceTransform);
            alertedCount++;
        }

        _alertedReceivers.Clear();
        return alertedCount;
    }

    /// <summary>
    /// Presenta localmente la campanada replicada cuando el observador se encuentra dentro de 15m.
    /// </summary>
    public void PresentVoidBellChime(NetworkRunner runner = null)
    {
        OnVoidBellChime?.Invoke();

        if (!TryResolveObserverDistance(runner, out float observerDistance) ||
            observerDistance > _voidBellAudibleRadius)
        {
            return;
        }

        PresentedVoidBellChimeCount++;
        PlayChimeAudio(observerDistance);
        TriggerPulseRing();
    }

    private bool TryResolveObserverDistance(NetworkRunner runner, out float distance)
    {
        if (ObserverTransformOverrideForTesting != null)
        {
            distance = Vector2.Distance(transform.position, ObserverTransformOverrideForTesting.position);
            return true;
        }

        if (_playerCharacter != null && _playerCharacter.Object != null && _playerCharacter.Object.IsValid)
        {
            if (_playerCharacter.HasInputAuthority)
            {
                distance = 0f;
                return true;
            }

            if (runner != null && !runner.LocalPlayer.IsNone &&
                runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localPlayerObject) &&
                localPlayerObject != null)
            {
                if (localPlayerObject.TryGetComponent(out NetworkRaidParticipant participant) &&
                    participant.TryResolveCurrentAvatar(out NetworkObject localAvatar) &&
                    localAvatar != null)
                {
                    distance = Vector2.Distance(transform.position, localAvatar.transform.position);
                    return true;
                }

                distance = Vector2.Distance(transform.position, localPlayerObject.transform.position);
                return true;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            distance = Vector2.Distance(transform.position, mainCamera.transform.position);
            return true;
        }

        if (runner == null)
        {
            distance = 0f;
            return true;
        }

        distance = float.PositiveInfinity;
        return false;
    }

    private void PlayChimeAudio(float observerDistance)
    {
        float normalizedCloseness = 1f - Mathf.Clamp01(observerDistance / Mathf.Max(0.01f, _voidBellAudibleRadius));
        float volume = Mathf.Lerp(0.25f, 0.85f, normalizedCloseness);

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            if (_voidBellChimeSfx.IsValid)
            {
                audioManager.PlaySfx(in _voidBellChimeSfx, transform.position);
                return;
            }

            audioManager.PlaySfx(GetOrCreateProvisionalBellClip(), transform.position, volume, 1f);
            return;
        }

        // Direct standalone playback when AudioManager is not initialized. PlayClipAtPoint destroys its
        // temporary source with Destroy, which Unity rejects outside Play Mode.
        if (!Application.isPlaying)
        {
            return;
        }

        AudioClip clip = _voidBellChimeSfx.IsValid ? _voidBellChimeSfx.GetRandomClip() : GetOrCreateProvisionalBellClip();
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }

    private static AudioClip GetOrCreateProvisionalBellClip()
    {
        if (s_provisionalVoidBellClip != null)
        {
            return s_provisionalVoidBellClip;
        }

        const int sampleRate = 22050;
        const float durationSeconds = 0.45f;
        int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-7.5f * t);
            float fundamental = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f;
            float overtone = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f * Mathf.Exp(-11f * t);
            float shimmer = Mathf.Sin(2f * Mathf.PI * 2112f * t) * 0.1f * Mathf.Exp(-16f * t);
            samples[i] = (fundamental + overtone + shimmer) * envelope;
        }

        s_provisionalVoidBellClip = AudioClip.Create(
            "ProvisionalVoidBellChime",
            sampleCount,
            1,
            sampleRate,
            false);
        s_provisionalVoidBellClip.hideFlags = HideFlags.HideAndDontSave;
        s_provisionalVoidBellClip.SetData(samples, 0);
        return s_provisionalVoidBellClip;
    }

    private void TriggerPulseRing()
    {
        EnsurePulseRingRenderer();
        if (_pulseRingRenderer == null)
        {
            return;
        }

        _pulseCenter = transform.position;
        _pulseElapsed = 0f;
        _isPulseActive = true;
        _pulseRingRenderer.enabled = true;
        UpdatePulseRingGeometry(0f);
    }

    private void EnsurePulseRingRenderer()
    {
        if (_pulseRingRenderer != null)
        {
            return;
        }

        var ringObject = new GameObject("VoidBellChimePulseVfx");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.zero;

        _pulseRingRenderer = ringObject.AddComponent<LineRenderer>();
        _pulseRingRenderer.useWorldSpace = true;
        _pulseRingRenderer.loop = true;
        _pulseRingRenderer.positionCount = PulseRingSegments;
        _pulseRingRenderer.widthMultiplier = 0.12f;
        _pulseRingRenderer.numCapVertices = 0;
        _pulseRingRenderer.numCornerVertices = 2;
        _pulseRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _pulseRingRenderer.receiveShadows = false;
        _pulseRingRenderer.sortingLayerName = "VFX";
        _pulseRingRenderer.sortingOrder = 15;

        if (s_provisionalPulseMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Particles/Standard Unlit");

            if (shader != null)
            {
                s_provisionalPulseMaterial = new Material(shader)
                {
                    name = "VoidBellPulseRingMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        if (s_provisionalPulseMaterial != null)
        {
            _pulseRingRenderer.sharedMaterial = s_provisionalPulseMaterial;
        }

        _pulsePositions = new Vector3[PulseRingSegments];
        _pulseRingRenderer.enabled = false;
    }

    private void UpdatePulseRingVisual(float deltaTime)
    {
        if (!_isPulseActive || _pulseRingRenderer == null)
        {
            return;
        }

        _pulseElapsed += Mathf.Max(0f, deltaTime);
        float normalized = Mathf.Clamp01(_pulseElapsed / PulseDurationSeconds);
        if (normalized >= 1f)
        {
            StopPulseRingImmediate();
            return;
        }

        UpdatePulseRingGeometry(normalized);
    }

    private void UpdatePulseRingGeometry(float normalizedProgress)
    {
        if (_pulseRingRenderer == null || _pulsePositions == null)
        {
            return;
        }

        float eased = 1f - (1f - normalizedProgress) * (1f - normalizedProgress);
        float radius = Mathf.Lerp(PulseStartRadius, PulseEndRadius, eased);
        float alpha = Mathf.Lerp(PulseInitialAlpha, 0f, normalizedProgress);
        var ringColor = new Color(0.85f, 0.45f, 1f, alpha);

        _pulseRingRenderer.startColor = ringColor;
        _pulseRingRenderer.endColor = ringColor;

        float step = Mathf.PI * 2f / PulseRingSegments;
        for (int i = 0; i < PulseRingSegments; i++)
        {
            float angle = step * i;
            _pulsePositions[i] = _pulseCenter + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        _pulseRingRenderer.SetPositions(_pulsePositions);
    }

    private void StopPulseRingImmediate()
    {
        _isPulseActive = false;
        _pulseElapsed = 0f;
        if (_pulseRingRenderer != null)
        {
            _pulseRingRenderer.enabled = false;
        }
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

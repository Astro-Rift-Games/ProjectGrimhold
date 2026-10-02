using Fusion;
using UnityEngine;

/// <summary>
/// Presents one Sanctuary's confirmed reservation and ritual state.
/// The local owner identity is resolved afresh on every presentation update and
/// is never retained as a private presentation source.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExtractionSanctuaryPresenter : MonoBehaviour
{
    private const float DefaultPulseFrequency = 1.5f;

    [System.Flags]
    private enum ObservedCues
    {
        None = 0,
        Assigned = 1,
        Started = 2,
        Cancelled = 4,
        Completed = 8
    }

    [SerializeField]
    private ExtractionSanctuary _sanctuary;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    [Min(0f)]
    private float _pulseFrequency = DefaultPulseFrequency;

    [Header("Confirmed Ritual Cues")]
    [SerializeField] private CustomClip _assignedCue;
    [SerializeField] private CustomClip _ritualStartedCue;
    [SerializeField] private CustomClip _ritualCancelledCue;
    [SerializeField] private CustomClip _ritualCompletedCue;
    [SerializeField] private ParticleSystem _ritualCompletedParticles;

    private Sprite _originalSprite;
    private Color _originalColor;
    private bool _originalStateCaptured;
    private bool _hasObservedSnapshot;
    private ExtractionRitualState _previousState;
    private bool _previouslyReserved;

    private void Awake()
    {
        CacheDependencies();
        CaptureOriginalState();
    }

    private void OnEnable()
    {
        if (!_originalStateCaptured)
        {
            CacheDependencies();
            CaptureOriginalState();
        }

        _hasObservedSnapshot = false;
        PresentCurrentState();
    }

    private void LateUpdate()
    {
        PresentCurrentState();
    }

    private void OnDisable()
    {
        _hasObservedSnapshot = false;
        RestoreOriginalState();
    }

    private void OnDestroy()
    {
        RestoreOriginalState();
    }

    private void CacheDependencies()
    {
        if (_sanctuary == null)
        {
            _sanctuary = GetComponent<ExtractionSanctuary>();
        }

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void CaptureOriginalState()
    {
        if (_originalStateCaptured || _spriteRenderer == null)
        {
            return;
        }

        _originalSprite = _spriteRenderer.sprite;
        _originalColor = _spriteRenderer.color;
        _originalStateCaptured = true;
    }

    private void PresentCurrentState()
    {
        if (_spriteRenderer == null || _sanctuary == null)
        {
            _hasObservedSnapshot = false;
            return;
        }

        NetworkObject networkObject = _sanctuary.Object;
        if (networkObject == null || !networkObject.IsValid || !networkObject.IsInSimulation ||
            !_sanctuary.TryGetRitualProgress(out ExtractionRitualSnapshot snapshot))
        {
            _hasObservedSnapshot = false;
            _spriteRenderer.color = CreateColor(0.9f, 0.2f, 0.2f);
            return;
        }

        IExtractionSanctuary sanctuary = _sanctuary;
        bool localOwnerResolved = TryResolveLocalOwner(out EntityId localOwnerId);
        bool isLocalOwner = localOwnerResolved && sanctuary.IsOwnedBy(localOwnerId);
        ObserveTransition(snapshot.State, sanctuary.IsReserved, isLocalOwner);
        PresentSnapshot(snapshot, sanctuary.IsReserved, isLocalOwner);
    }

    private void ObserveTransition(ExtractionRitualState currentState, bool isReserved, bool isLocalOwner)
    {
        ObservedCues cues = ResolveObservedCues(
            _hasObservedSnapshot,
            _previousState,
            currentState,
            _previouslyReserved,
            isReserved,
            isLocalOwner);
        if (!_hasObservedSnapshot)
        {
            _hasObservedSnapshot = true;
            _previousState = currentState;
            _previouslyReserved = isReserved;
            return;
        }

        if ((cues & ObservedCues.Started) != 0)
        {
            PlayCue(_ritualStartedCue);
        }
        if ((cues & ObservedCues.Cancelled) != 0)
        {
            PlayCue(_ritualCancelledCue);
        }
        if ((cues & ObservedCues.Completed) != 0)
        {
            PlayCue(_ritualCompletedCue, _ritualCompletedParticles);
        }
        if ((cues & ObservedCues.Assigned) != 0)
        {
            PlayCue(_assignedCue);
        }

        _previousState = currentState;
        _previouslyReserved = isReserved;
    }

    private static ObservedCues ResolveObservedCues(
        bool hasPreviousSnapshot,
        ExtractionRitualState previousState,
        ExtractionRitualState currentState,
        bool previouslyReserved,
        bool currentlyReserved,
        bool isLocalOwner)
    {
        if (!hasPreviousSnapshot)
        {
            return ObservedCues.None;
        }

        ObservedCues cues = ObservedCues.None;
        if (previousState != currentState)
        {
            switch (currentState)
            {
                case ExtractionRitualState.InProgress:
                    cues |= ObservedCues.Started;
                    break;
                case ExtractionRitualState.Cancelled when isLocalOwner:
                    cues |= ObservedCues.Cancelled;
                    break;
                case ExtractionRitualState.Completed:
                    cues |= ObservedCues.Completed;
                    break;
            }
        }

        if (!previouslyReserved && currentlyReserved && isLocalOwner)
        {
            cues |= ObservedCues.Assigned;
        }

        return cues;
    }

    private void PresentSnapshot(ExtractionRitualSnapshot snapshot, bool isReserved, bool isLocalOwner)
    {
        switch (snapshot.State)
        {
            case ExtractionRitualState.Completed:
                _spriteRenderer.color = CreateColor(0.2f, 0.9f, 0.4f);
                return;
            case ExtractionRitualState.InProgress:
                float progress = Mathf.Clamp01(snapshot.Progress);
                float frequency = Mathf.Max(0f, _pulseFrequency) * Mathf.Lerp(0.7f, 1.6f, progress);
                float phase = frequency > 0f
                    ? (Mathf.Sin(Time.unscaledTime * frequency * Mathf.PI * 2f) + 1f) * 0.5f
                    : 0f;
                Color pulse = Color.Lerp(
                    CreateColor(0.2f, 0.65f, 1f),
                    CreateColor(0.2f, 0.9f, 1f),
                    phase);
                Color intensity = Color.Lerp(
                    CreateColor(0.2f, 0.65f, 1f),
                    CreateColor(0.5f, 1f, 1f),
                    progress * 0.45f);
                _spriteRenderer.color = Color.Lerp(pulse, intensity, progress * 0.45f);
                return;
            case ExtractionRitualState.Cancelled when isLocalOwner:
                _spriteRenderer.color = CreateColor(0.65f, 0.25f, 0.25f);
                return;
            case ExtractionRitualState.NotStarted when isReserved && isLocalOwner:
                _spriteRenderer.color = CreateColor(0.2f, 0.65f, 1f);
                return;
            default:
                _spriteRenderer.color = CreateColor(0.9f, 0.2f, 0.2f);
                return;
        }
    }

    private bool TryResolveLocalOwner(out EntityId localOwnerId)
    {
        localOwnerId = default;
        NetworkRunner runner = _sanctuary != null ? _sanctuary.Runner : null;
        if (runner == null || runner.LocalPlayer.IsNone ||
            !runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null || !playerObject.IsValid)
        {
            return false;
        }

        localOwnerId = new EntityId(unchecked((int)playerObject.Id.Raw));
        return localOwnerId.Value != 0;
    }

    private void PlayCue(CustomClip clip, ParticleSystem particles = null)
    {
        if (clip.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(clip, transform.position);
        }

        GameObject effect = ParticleEffectPlayer.InstantiateAndPlay(particles, transform.position);
        if (effect != null && effect.TryGetComponent(out ParticleSystem instance))
        {
            ParticleSystem.MainModule main = instance.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
        }
    }

    private void RestoreOriginalState()
    {
        if (!_originalStateCaptured || _spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.sprite = _originalSprite;
        _spriteRenderer.color = _originalColor;
    }

    private Color CreateColor(float red, float green, float blue)
    {
        float alpha = _originalStateCaptured ? _originalColor.a : _spriteRenderer.color.a;
        return new Color(red, green, blue, alpha);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
        _pulseFrequency = Mathf.Max(0f, _pulseFrequency);
    }
#endif
}

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

    [SerializeField]
    private ExtractionSanctuary _sanctuary;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    [Min(0f)]
    private float _pulseFrequency = DefaultPulseFrequency;

    private Sprite _originalSprite;
    private Color _originalColor;
    private bool _originalStateCaptured;

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

        PresentCurrentState();
    }

    private void LateUpdate()
    {
        PresentCurrentState();
    }

    private void OnDisable()
    {
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
            return;
        }

        NetworkObject networkObject = _sanctuary.Object;
        if (networkObject == null || !networkObject.IsValid || !networkObject.IsInSimulation)
        {
            _spriteRenderer.color = CreateColor(0.9f, 0.2f, 0.2f);
            return;
        }

        IExtractionSanctuary sanctuary = _sanctuary;
        if (sanctuary.RitualState == ExtractionRitualState.Completed)
        {
            _spriteRenderer.color = CreateColor(0.2f, 0.9f, 0.4f);
            return;
        }

        if (sanctuary.RitualState == ExtractionRitualState.InProgress)
        {
            float frequency = Mathf.Max(0f, _pulseFrequency);
            float phase = frequency > 0f
                ? (Mathf.Sin(Time.unscaledTime * frequency * Mathf.PI * 2f) + 1f) * 0.5f
                : 0f;
            _spriteRenderer.color = Color.Lerp(
                CreateColor(0.2f, 0.65f, 1f),
                CreateColor(0.2f, 0.9f, 1f),
                phase);
            return;
        }

        bool localOwnerResolved = TryResolveLocalOwner(out EntityId localOwnerId);
        bool isLocalOwner = localOwnerResolved && sanctuary.IsOwnedBy(localOwnerId);

        if (sanctuary.RitualState == ExtractionRitualState.Cancelled && isLocalOwner)
        {
            _spriteRenderer.color = CreateColor(0.65f, 0.25f, 0.25f);
            return;
        }

        if (sanctuary.RitualState == ExtractionRitualState.NotStarted &&
            sanctuary.IsReserved && isLocalOwner)
        {
            _spriteRenderer.color = CreateColor(0.2f, 0.65f, 1f);
            return;
        }

        _spriteRenderer.color = CreateColor(0.9f, 0.2f, 0.2f);
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

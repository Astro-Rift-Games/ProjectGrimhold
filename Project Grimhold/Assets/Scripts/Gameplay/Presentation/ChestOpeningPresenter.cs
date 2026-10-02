using UnityEngine;
using Fusion;

/// <summary>Chest-only local presentation of confirmed first opening.</summary>
[DisallowMultipleComponent]
public sealed class ChestOpeningPresenter : NetworkBehaviour
{
    [SerializeField] private NetworkLootContainerInteractable _interactable;
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Sprite[] _openingFrames;
    [SerializeField, Min(1f)] private float _framesPerSecond = 12f;
    [SerializeField] private CustomClip _openingSound;

    private readonly ChestOpeningPresentationState _state = new();
    private Vector3 _closedPosition;
    private bool _initialized;
    private bool _configurationValid;
    private bool _reportedMissingAudio;

    public bool IsOpening => isActiveAndEnabled && _configurationValid && _initialized &&
        _interactable != null && _renderer != null && _interactable.Object != null &&
        _interactable.Object.IsValid && _state.IsOpening;

    private void Awake()
    {
        _configurationValid = _interactable != null && _renderer != null &&
            _openingFrames != null && _openingFrames.Length > 0 &&
            _framesPerSecond >= 1f;
        if (_configurationValid)
        {
            for (int index = 0; index < _openingFrames.Length; index++)
            {
                if (_openingFrames[index] == null)
                {
                    _configurationValid = false;
                    break;
                }
            }
        }

        if (!_configurationValid)
        {
            Debug.LogError($"{nameof(ChestOpeningPresenter)} has invalid serialized presentation configuration.", this);
            return;
        }

        _closedPosition = _renderer.transform.localPosition;
        ApplyFrame(0);
    }

    public override void Spawned()
    {
        if (!_configurationValid || _interactable == null || _renderer == null)
        {
            return;
        }

        // Baked after the interactable: fresh initialization and snapshot restore
        // are complete before capturing the baseline, without waiting for a frame.
        _state.Initialize(_interactable.FirstOpenResolved);
        _initialized = true;
        ApplyFrame(_state.Advance(0f, _openingFrames.Length, _framesPerSecond));
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _state.Complete();
        _initialized = false;
    }

    private bool CanObserve()
    {
        if (!_configurationValid || !isActiveAndEnabled || !_initialized ||
            _interactable == null || _renderer == null ||
            _interactable.Object == null || !_interactable.Object.IsValid)
        {
            _state.Complete();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called only for a confirmed successful container interaction. Converges with
    /// replicated observation even when the interaction result arrives first.
    /// </summary>
    public void ObserveConfirmedOpen()
    {
        if (CanObserve())
        {
            ObserveOpen(true);
        }
    }

    private void LateUpdate()
    {
        if (!CanObserve())
        {
            return;
        }

        ObserveOpen(_interactable.FirstOpenResolved);
        ApplyFrame(_state.Advance(Time.unscaledDeltaTime, _openingFrames.Length, _framesPerSecond));
    }

    private void ObserveOpen(bool opened)
    {
        if (!_state.Observe(opened))
        {
            return;
        }

        ApplyFrame(0);
        if (_openingSound.IsValid && !_openingSound.Loop && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(_openingSound, transform.position);
        }
        else if (!_reportedMissingAudio)
        {
            _reportedMissingAudio = true;
            Debug.LogWarning($"{nameof(ChestOpeningPresenter)} omitted opening sound: configure a non-looping clip and an available AudioManager.", this);
        }
    }

    private void ApplyFrame(int index)
    {
        Sprite frame = _openingFrames[index];
        _renderer.sprite = frame;
        Vector3 position = _closedPosition;
        position.y += (_openingFrames[0].bounds.min.y - frame.bounds.min.y) * _renderer.transform.localScale.y;
        _renderer.transform.localPosition = position;
    }

    private void OnDisable()
    {
        _state.Complete();
        if (_initialized && _configurationValid && _renderer != null)
        {
            ApplyFrame(_state.Advance(0f, _openingFrames.Length, _framesPerSecond));
        }
    }
}

using UnityEngine;

/// <summary>
/// Plays local-only rejection motion on a pickup visual while leaving its
/// authoritative root, collider, and network transform unchanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class LootPickupRejectionPresenter : MonoBehaviour
{
    [SerializeField]
    private Transform _visualTransform;

    [SerializeField, Min(0.01f)]
    private float _duration = 0.45f;

    [SerializeField, Min(0f)]
    private float _jumpHeight = 0.35f;

    [SerializeField]
    private float _rotationDegrees = 360f;

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private float _elapsed;
    private bool _isPlaying;
    private bool _hasBasePose;

    private void Awake()
    {
        CaptureBasePose();
    }

    private void OnEnable()
    {
        CaptureBasePose();
    }

    private void OnDisable()
    {
        RestoreBasePose();
    }

    private void Update()
    {
        if (!_isPlaying || _visualTransform == null)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);
        float height = Mathf.Sin(progress * Mathf.PI) * _jumpHeight;
        _visualTransform.localPosition = _baseLocalPosition + Vector3.up * height;
        _visualTransform.localRotation = _baseLocalRotation *
            Quaternion.Euler(0f, 0f, _rotationDegrees * progress);

        if (progress >= 1f)
        {
            RestoreBasePose();
        }
    }

    /// <summary>Restarts the local rejection motion from the configured base pose.</summary>
    public void PlayRejectedPickup()
    {
        if (_visualTransform == null)
        {
            return;
        }

        RestoreBasePose();
        _isPlaying = true;
    }

    private void CaptureBasePose()
    {
        if (_hasBasePose || _visualTransform == null)
        {
            return;
        }

        _baseLocalPosition = _visualTransform.localPosition;
        _baseLocalRotation = _visualTransform.localRotation;
        _hasBasePose = true;
    }

    private void RestoreBasePose()
    {
        _isPlaying = false;
        _elapsed = 0f;
        if (!_hasBasePose || _visualTransform == null)
        {
            return;
        }

        _visualTransform.localPosition = _baseLocalPosition;
        _visualTransform.localRotation = _baseLocalRotation;
    }
}

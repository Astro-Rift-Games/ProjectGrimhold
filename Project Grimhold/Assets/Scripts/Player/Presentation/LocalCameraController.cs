using UnityEngine;

/// <summary>
/// Controls the local camera to smoothly follow a player target in 2D space.
/// This component belongs to the presentation layer and does not participate in network simulation.
///
/// When a <see cref="CameraShakeController"/> is present on the same GameObject,
/// its <see cref="CameraShakeController.ShakeOffset"/> is applied on top of the
/// smooth-follow desired position every frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalCameraController : MonoBehaviour
{
    /// <summary>
    /// Singleton reference to the active local camera controller.
    /// </summary>
    public static LocalCameraController Instance { get; private set; }

    [Header("Tracking Configuration")]
    [SerializeField]
    private Vector2 _offset;

    [SerializeField, Min(0f)]
    private float _smoothTime = 0.1f;

    [SerializeField]
    private float _zDepth = -10f;

    [SerializeField]
    private bool _snapOnTargetAssigned = true;

    private Transform _target;
    private Vector3 _followVelocity;
    private CameraShakeController _shakeController;

    private void Awake()
    {
        _shakeController = GetComponent<CameraShakeController>();
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Another active instance of {nameof(LocalCameraController)} was detected on {Instance.gameObject.name}. " +
                             $"Replacing reference with the instance on {gameObject.name}.", this);
        }

        Instance = this;
        _followVelocity = Vector3.zero;

        // If the local player binder is already spawned, attach immediately.
        if (LocalPlayerCameraBinder.LocalPlayerInstance != null)
        {
            SetTarget(LocalPlayerCameraBinder.LocalPlayerInstance.transform);
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        _followVelocity = Vector3.zero;
    }

    /// <summary>
    /// Sets the tracking target for the camera.
    /// </summary>
    /// <param name="target">The transform to follow.</param>
    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(SetTarget)} received a null target reference.", this);
            return;
        }

        if (_target == target)
        {
            return;
        }

        _target = target;
        _followVelocity = Vector3.zero;

        if (_snapOnTargetAssigned)
        {
            SnapToTarget();
        }
    }

    /// <summary>
    /// Clears the tracking target if it matches the specified target.
    /// </summary>
    /// <param name="target">The target to clear.</param>
    public void ClearTarget(Transform target)
    {
        if (_target != target)
        {
            return;
        }

        _target = null;
        _followVelocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 desiredPosition = new(
            _target.position.x + _offset.x,
            _target.position.y + _offset.y,
            _zDepth
        );

        if (_smoothTime <= 0f)
        {
            transform.position = desiredPosition;
            _followVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _followVelocity,
                _smoothTime
            );
        }

        // Apply shake offset on top of the smooth-follow result.
        // Z is preserved from desiredPosition so the camera depth is unaffected.
        if (_shakeController != null)
        {
            Vector3 shakeOffset = _shakeController.ShakeOffset;
            transform.position = new Vector3(
                transform.position.x + shakeOffset.x,
                transform.position.y + shakeOffset.y,
                transform.position.z
            );
        }
    }

    private void SnapToTarget()
    {
        if (_target == null)
        {
            return;
        }

        transform.position = new Vector3(
            _target.position.x + _offset.x,
            _target.position.y + _offset.y,
            _zDepth
        );
        _followVelocity = Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _smoothTime = Mathf.Max(0f, _smoothTime);
    }
#endif
}

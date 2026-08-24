using UnityEngine;

/// <summary>
/// Concrete animator view component for player entities, inheriting core animation logic from <see cref="CharacterAnimatorView"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimatorView : CharacterAnimatorView
{
    private const float DefaultLocomotionPlaybackRate = 1f;

    private static readonly int LocomotionPlaybackRateHash =
        Animator.StringToHash("LocomotionPlaybackRate");

    [SerializeField, Min(0f)]
    private float _referenceMovementSpeed = 4f;

    private Vector2 _previousVisualPosition;
    private bool _hasPreviousVisualPosition;

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetVisualPositionSample();
    }

    private void LateUpdate()
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        float playbackRate = SampleLocomotionPlaybackRate(
            transform.position,
            Time.deltaTime);

        AnimatorInstance.SetFloat(
            LocomotionPlaybackRateHash,
            playbackRate);
    }

    private float SampleLocomotionPlaybackRate(
        Vector2 currentVisualPosition,
        float deltaTime)
    {
        if (!IsFinite(currentVisualPosition))
        {
            ResetVisualPositionSample();
            return DefaultLocomotionPlaybackRate;
        }

        if (!_hasPreviousVisualPosition)
        {
            _previousVisualPosition = currentVisualPosition;
            _hasPreviousVisualPosition = true;
            return DefaultLocomotionPlaybackRate;
        }

        Vector2 previousVisualPosition = _previousVisualPosition;
        _previousVisualPosition = currentVisualPosition;

        return CalculateLocomotionPlaybackRate(
            previousVisualPosition,
            currentVisualPosition,
            deltaTime,
            _referenceMovementSpeed);
    }

    private static float CalculateLocomotionPlaybackRate(
        Vector2 previousVisualPosition,
        Vector2 currentVisualPosition,
        float deltaTime,
        float referenceMovementSpeed)
    {
        if (!IsFinite(previousVisualPosition) ||
            !IsFinite(currentVisualPosition) ||
            !IsFinite(deltaTime) ||
            deltaTime <= 0f ||
            !IsFinite(referenceMovementSpeed) ||
            referenceMovementSpeed <= 0f)
        {
            return DefaultLocomotionPlaybackRate;
        }

        float distance = Vector2.Distance(
            previousVisualPosition,
            currentVisualPosition);

        if (!IsFinite(distance))
        {
            return DefaultLocomotionPlaybackRate;
        }

        float visualMovementSpeed = distance / deltaTime;
        if (!IsFinite(visualMovementSpeed))
        {
            return DefaultLocomotionPlaybackRate;
        }

        float playbackRate = visualMovementSpeed / referenceMovementSpeed;
        return IsFinite(playbackRate)
            ? playbackRate
            : DefaultLocomotionPlaybackRate;
    }

    private void ResetVisualPositionSample()
    {
        _previousVisualPosition = default;
        _hasPreviousVisualPosition = false;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

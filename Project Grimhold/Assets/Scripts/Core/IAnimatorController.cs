using UnityEngine;

/// <summary>
/// Abstraction for character animator views, allowing presenters to direct
/// animation state overrides (temporal facing direction and defeat poses)
/// without coupling to specific player/enemy presentation components.
/// </summary>
public interface IAnimatorController
{
    /// <summary>
    /// Sets the defeated visual state of the animator.
    /// </summary>
    void SetDefeated(bool defeated);

    /// <summary>
    /// Applies a temporal facing direction that overrides locomotion facing direction.
    /// </summary>
    void ApplyTemporalFacingDirection(Vector2 direction);

    /// <summary>
    /// Clears any temporal facing direction, returning to normal locomotion state.
    /// </summary>
    void ClearTemporalFacingDirection();

    /// <summary>
    /// Fires the OnAttack trigger on the Animator to start a controller-driven attack animation.
    /// </summary>
    void TriggerAttack();
}

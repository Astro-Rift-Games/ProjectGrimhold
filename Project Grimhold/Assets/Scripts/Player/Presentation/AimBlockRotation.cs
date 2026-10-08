using UnityEngine;

/// <summary>
/// Rigid turn of the aim-stance arm about a per-weapon torso pivot: the left hand, the weapon and the right hand
/// rotate together by the residual as one block, like an extended arm aiming, so the weapon stays outward toward
/// the aim. An optional outward offset along the aim, ramped with the draw, extends the arm; at 0 it is pure rotation.
/// </summary>
internal static class AimBlockRotation
{
    /// <summary>
    /// Rotates a block member's position about the pivot by the residual, adds the outward offset and adds the
    /// residual to its rotation. All values are in the visual root space.
    /// </summary>
    internal static void Apply(
        Vector2 position,
        float rotationDegrees,
        Vector2 pivot,
        float residualDegrees,
        Vector2 outward,
        out Vector2 newPosition,
        out float newRotationDegrees)
    {
        newPosition = PointRotation.About(position, pivot, residualDegrees) + outward;
        newRotationDegrees = rotationDegrees + residualDegrees;
    }

    /// <summary>The block's extension along the aim: the offset scaled by the draw blend, clamped to [0, 1].</summary>
    internal static Vector2 Outward(Vector2 aim, float offset, float blend)
    {
        if (offset <= 0f || aim.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return aim.normalized * (offset * Mathf.Clamp01(blend));
    }
}

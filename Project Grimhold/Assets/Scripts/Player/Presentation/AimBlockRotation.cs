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

/// <summary>
/// Pins the drawn string hand to the weapon's nock point. The point is authored once in weapon space; it follows the
/// posed weapon, and mirrors with it for left facings.
/// </summary>
internal static class StringHandPin
{
    /// <summary>The visual-root offset that moves the hand grip toward the nock by the weight, clamped to [0, 1].</summary>
    internal static Vector2 Offset(Vector2 handGrip, Vector2 nock, float weight)
    {
        return (nock - handGrip) * Mathf.Clamp01(weight);
    }

    /// <summary>The nock in weapon space for the facing: the weapon art mirrors across its axis in left facings.</summary>
    internal static Vector2 NockForFacing(Vector2 nockPoint, bool mirrored)
    {
        return new Vector2(mirrored ? -nockPoint.x : nockPoint.x, nockPoint.y);
    }
}

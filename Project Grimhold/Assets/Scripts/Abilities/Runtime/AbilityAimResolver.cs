using UnityEngine;

/// <summary>Resolves the direction an accepted execution captures, reusing the shared aim math.</summary>
internal static class AbilityAimResolver
{
    /// <summary>
    /// Abilities follow the character facing, like melee attacks; the cursor aim is ignored (only bows and
    /// crossbows follow it). Returns false, with a zero direction, when the facing is unusable.
    /// </summary>
    internal static bool TryResolve(Vector2 facingDirection, out Vector2 direction) =>
        PlayerAimMath.TryNormalizeDirection(facingDirection, out direction);
}

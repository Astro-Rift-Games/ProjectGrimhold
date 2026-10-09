using UnityEngine;

/// <summary>Resolves the direction an accepted execution captures, reusing the shared aim math.</summary>
internal static class AbilityAimResolver
{
    /// <summary>
    /// Prefers the continuous aim and falls back to the character facing when the aim is unusable.
    /// Returns false, with a zero direction, only when neither is usable.
    /// </summary>
    internal static bool TryResolve(Vector2 facingDirection, Vector2 aimDirection, out Vector2 direction) =>
        PlayerAimMath.TryResolveAttackDirection(true, facingDirection, aimDirection, out direction);
}

using UnityEngine;

/// <summary>
/// Chooses the facing the body presents while a free-aim weapon is equipped. The body keeps the bucket of the
/// movement (or contextual attack) facing while the aim stays inside a free arc around that bucket's canonical
/// direction, and switches to the aim's own bucket when the aim leaves the arc. A small hysteresis keeps the choice
/// from flickering at the arc border. Presentation only: the simulation facing is untouched.
/// </summary>
internal static class FreeAimFacingSelection
{
    internal static bool TrySelect(
        WeaponDefinition weapon,
        bool hasAim,
        Vector2 aim,
        Vector2 movementFacing,
        float arcHalfWidthDegrees,
        float hysteresisDegrees,
        ref bool showingAim,
        out Vector2 facing)
    {
        facing = default;
        if (!hasAim || weapon == null || weapon.Presentation.AimMode != WeaponAimMode.FreeAim)
        {
            showingAim = false;
            return false;
        }

        Vector2 bucketAxis = CharacterVisualDirectionResolver.GetCanonicalVector(
            CharacterVisualDirectionResolver.Resolve(movementFacing));
        float offset = Mathf.Abs(FreeAimResidual.AngleDegrees(bucketAxis, aim));
        // Leaving the arc switches to the aim bucket; coming back needs to be inside by the hysteresis.
        showingAim = showingAim
            ? offset > arcHalfWidthDegrees - hysteresisDegrees
            : offset > arcHalfWidthDegrees;
        facing = showingAim ? aim : movementFacing;
        return true;
    }
}

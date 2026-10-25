using UnityEngine;

/// <summary>
/// Provides deterministic, presentation-only calculations for the player's
/// weapon presentation.
/// </summary>
internal static class PlayerWeaponPresentationMath
{
    internal static Vector2 CanonicalFacing(int direction) => direction switch
    {
        0 => CharacterVisualDirectionResolver.CanonicalNorth,
        1 => CharacterVisualDirectionResolver.CanonicalNorthEast,
        2 => CharacterVisualDirectionResolver.CanonicalNorthWest,
        3 => CharacterVisualDirectionResolver.CanonicalSouth,
        4 => CharacterVisualDirectionResolver.CanonicalSouthEast,
        5 => CharacterVisualDirectionResolver.CanonicalSouthWest,
        _ => throw new System.ArgumentOutOfRangeException(nameof(direction))
    };

    internal static Vector3 ReflectAcrossFacing(Vector3 value, int direction)
    {
        Vector3 f = CanonicalFacing(direction);
        Vector3 reflected = 2 * Vector3.Dot(value, f) * f - value;
        reflected.z = value.z;
        return reflected;
    }

    internal static Quaternion ReflectedRotation(Quaternion rotation, int direction)
    {
        Vector2 f = CanonicalFacing(direction);
        return Quaternion.AngleAxis(2 * Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg, Vector3.forward) * Quaternion.Inverse(rotation);
    }

    internal static float CalculateFacingAngleDegrees(Vector2 facing)
    {
        return Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
    }

    internal static bool ShouldMirror(Vector2 facing)
    {
        return facing.x < 0f;
    }

    /// <summary>
    /// Angle correction the held visual uses under the facing pivot. A left facing mirrors the pivot across the
    /// facing axis; the weapon must instead mirror across its own art axis (sprite +Y), so the mirrored visual
    /// takes -180 - correction. Art laid along the facing (-90) is unchanged, while art held across it keeps its
    /// side of the facing instead of flipping to the other one.
    /// </summary>
    internal static float ResolveAngleCorrection(float weaponAngleCorrection, bool mirrored)
    {
        return mirrored ? -180f - weaponAngleCorrection : weaponAngleCorrection;
    }

    internal static Vector2 CalculateGripAlignedWeaponPosition(
        Vector2 weaponGripPoint,
        Vector2 weaponScale,
        float weaponAngleCorrection)
    {
        Vector2 scaledGrip = Vector2.Scale(weaponGripPoint, weaponScale);
        float angleRadians = weaponAngleCorrection * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angleRadians);
        float sine = Mathf.Sin(angleRadians);
        Vector2 rotatedGrip = new Vector2(
            scaledGrip.x * cosine - scaledGrip.y * sine,
            scaledGrip.x * sine + scaledGrip.y * cosine);

        return -rotatedGrip;
    }

    /// <summary>
    /// The held weapon shows its attack animation frame while its confirmed attack clip is inside the sequence,
    /// and its world sprite otherwise. An unarmed hand stays empty.
    /// </summary>
    internal static Sprite ResolveMainHandSprite(
        Sprite worldSprite,
        WeaponAttackSpriteAnimation attackAnimation,
        bool isAttacking,
        float attackSeconds)
    {
        return worldSprite != null &&
            attackAnimation != null &&
            isAttacking &&
            attackAnimation.TryGetSprite(attackSeconds, out Sprite frame)
                ? frame
                : worldSprite;
    }

    /// <summary>
    /// Shields intentionally cover their holding hand and glove in every facing.
    /// </summary>
    internal static int ResolveShieldSortingOrder(int itemOrder, int handOrder)
    {
        return Mathf.Max(itemOrder, handOrder + 2);
    }

    // Offensive items use fixed configured depth, independent of previous-frame hand sorting.
    internal static int ResolveOffHandSortingOrder(bool frontFacing, int frontOrder, int backOrder, int handOrder)
    {
        return frontFacing ? frontOrder : backOrder;
    }

    /// <summary>
    /// An equipped shield shows its sprite for the visual direction in every pose. Its world sprite is the
    /// fallback when a directional sprite is unavailable; an empty Off Hand stays empty.
    /// </summary>
    internal static Sprite ResolveOffHandSprite(
        Sprite worldSprite,
        DirectionalShieldSpriteSet directionalSprites,
        CharacterVisualDirection direction)
    {
        if (worldSprite == null || directionalSprites == null)
        {
            return worldSprite;
        }

        return directionalSprites.GetSprite(direction) ?? worldSprite;
    }
}

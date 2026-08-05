using System;
using UnityEngine;

/// <summary>
/// Immutable result of projecting a world-space Sanctuary marker into the local minimap.
/// The angle uses mathematical screen coordinates: zero is east and positive angles turn
/// counter-clockwise toward north. The view owns any graphical arrow correction.
/// </summary>
public readonly struct MinimapProjectionResult : IEquatable<MinimapProjectionResult>
{
    public bool IsValid { get; }
    public Vector2 Position { get; }
    public float AngleDegrees { get; }
    public bool IsClampedToEdge { get; }

    public MinimapProjectionResult(
        bool isValid,
        Vector2 position,
        float angleDegrees,
        bool isClampedToEdge)
    {
        IsValid = isValid;
        Position = isValid && IsFinite(position) ? position : Vector2.zero;
        AngleDegrees = isValid && IsFinite(angleDegrees) ? angleDegrees : 0f;
        IsClampedToEdge = isValid && isClampedToEdge;
    }

    public bool Equals(MinimapProjectionResult other)
    {
        return IsValid == other.IsValid &&
            Position == other.Position &&
            AngleDegrees.Equals(other.AngleDegrees) &&
            IsClampedToEdge == other.IsClampedToEdge;
    }

    public override bool Equals(object obj)
    {
        return obj is MinimapProjectionResult other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsValid, Position, AngleDegrees, IsClampedToEdge);
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

/// <summary>
/// Pure world-to-minimap projection math. Values returned by this class are local UI units
/// suitable for assigning to <see cref="RectTransform.anchoredPosition"/>.
/// </summary>
public static class MinimapProjection
{
    /// <summary>
    /// Projects the static map pivot relative to the local player. The map is translated in
    /// the opposite direction of player movement and is never rotated.
    /// </summary>
    public static bool TryProjectMapOffset(
        Vector2 mapPivotWorldPosition,
        Vector2 playerWorldPosition,
        float uiUnitsPerWorldUnit,
        float zoom,
        out Vector2 mapOffset)
    {
        mapOffset = Vector2.zero;
        if (!IsFinite(mapPivotWorldPosition) || !IsFinite(playerWorldPosition) ||
            !IsPositiveFinite(uiUnitsPerWorldUnit) || !IsPositiveFinite(zoom))
        {
            return false;
        }

        Vector2 offset = (mapPivotWorldPosition - playerWorldPosition) *
            (uiUnitsPerWorldUnit * zoom);
        if (!IsFinite(offset))
        {
            return false;
        }

        mapOffset = offset;
        return true;
    }

    /// <summary>
    /// Projects a Sanctuary marker and clamps only external points to the useful viewport
    /// rectangle. A marker exactly on the rectangle boundary remains an interior marker.
    /// </summary>
    public static MinimapProjectionResult ProjectMarker(
        Vector2 playerWorldPosition,
        Vector2 sanctuaryWorldPosition,
        float uiUnitsPerWorldUnit,
        float zoom,
        Vector2 viewportSize,
        Vector2 markerSize,
        float innerMargin)
    {
        if (!IsFinite(playerWorldPosition) || !IsFinite(sanctuaryWorldPosition) ||
            !IsPositiveFinite(uiUnitsPerWorldUnit) || !IsPositiveFinite(zoom) ||
            !IsPositiveFinite(viewportSize) || !IsPositiveFinite(markerSize) ||
            !IsFinite(innerMargin) || innerMargin < 0f)
        {
            return InvalidResult;
        }

        Vector2 halfExtents = viewportSize * 0.5f - markerSize * 0.5f -
            new Vector2(innerMargin, innerMargin);
        if (!IsPositiveFinite(halfExtents))
        {
            return InvalidResult;
        }

        Vector2 direction = sanctuaryWorldPosition - playerWorldPosition;
        if (!IsFinite(direction))
        {
            return InvalidResult;
        }

        float scale = uiUnitsPerWorldUnit * zoom;
        Vector2 projected = direction * scale;
        if (!IsFinite(projected))
        {
            return InvalidResult;
        }

        float angle = direction.sqrMagnitude > 0f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            : 0f;
        if (!IsFinite(angle))
        {
            return InvalidResult;
        }

        if (direction == Vector2.zero)
        {
            return new MinimapProjectionResult(true, Vector2.zero, 0f, false);
        }

        bool isInside = Mathf.Abs(projected.x) <= halfExtents.x &&
            Mathf.Abs(projected.y) <= halfExtents.y;
        if (isInside)
        {
            return new MinimapProjectionResult(true, projected, angle, false);
        }

        float intersectionScale = 1f;
        if (Mathf.Abs(projected.x) > halfExtents.x)
        {
            intersectionScale = Mathf.Min(
                intersectionScale,
                halfExtents.x / Mathf.Abs(projected.x));
        }

        if (Mathf.Abs(projected.y) > halfExtents.y)
        {
            intersectionScale = Mathf.Min(
                intersectionScale,
                halfExtents.y / Mathf.Abs(projected.y));
        }

        if (!IsPositiveFinite(intersectionScale))
        {
            return InvalidResult;
        }

        Vector2 edgePosition = projected * intersectionScale;
        return IsFinite(edgePosition)
            ? new MinimapProjectionResult(true, edgePosition, angle, true)
            : InvalidResult;
    }

    private static readonly MinimapProjectionResult InvalidResult =
        new MinimapProjectionResult(false, Vector2.zero, 0f, false);

    private static bool IsPositiveFinite(Vector2 value)
    {
        return IsPositiveFinite(value.x) && IsPositiveFinite(value.y);
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
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

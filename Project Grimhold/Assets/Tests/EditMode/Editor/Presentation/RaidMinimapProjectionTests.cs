using NUnit.Framework;
using UnityEngine;

public sealed class RaidMinimapProjectionTests
{
    private static readonly Vector2 Viewport = new Vector2(100f, 100f);
    private static readonly Vector2 Marker = new Vector2(10f, 10f);

    [Test]
    public void MapOffsetMovesOppositeToPlayerInUiUnits()
    {
        bool isValid = MinimapProjection.TryProjectMapOffset(
            new Vector2(10f, 20f),
            new Vector2(4f, 5f),
            2f,
            1.5f,
            out Vector2 offset);

        Assert.That(isValid, Is.True);
        Assert.That(offset, Is.EqualTo(new Vector2(18f, 45f)));
    }

    [TestCase(100f, 0f, 0f)]
    [TestCase(0f, 100f, 90f)]
    [TestCase(-100f, 0f, 180f)]
    [TestCase(0f, -100f, -90f)]
    public void CardinalDirectionsUseMathematicalAngles(
        float targetX,
        float targetY,
        float expectedAngle)
    {
        MinimapProjectionResult result = MinimapProjection.ProjectMarker(
            Vector2.zero,
            new Vector2(targetX, targetY),
            1f,
            1f,
            Viewport,
            Marker,
            0f);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsClampedToEdge, Is.True);
        Assert.That(result.AngleDegrees, Is.EqualTo(expectedAngle).Within(0.0001f));
        Assert.That(result.Position.x, Is.InRange(-45f, 45f));
        Assert.That(result.Position.y, Is.InRange(-45f, 45f));
    }

    [Test]
    public void ExactBoundaryIsInteriorAndZeroOffsetIsValid()
    {
        MinimapProjectionResult boundary = MinimapProjection.ProjectMarker(
            Vector2.zero,
            new Vector2(45f, 0f),
            1f,
            1f,
            Viewport,
            Marker,
            0f);
        MinimapProjectionResult zero = MinimapProjection.ProjectMarker(
            Vector2.one,
            Vector2.one,
            1f,
            1f,
            Viewport,
            Marker,
            0f);

        Assert.That(boundary.IsValid, Is.True);
        Assert.That(boundary.IsClampedToEdge, Is.False);
        Assert.That(boundary.Position, Is.EqualTo(new Vector2(45f, 0f)));
        Assert.That(zero.IsValid, Is.True);
        Assert.That(zero.Position, Is.EqualTo(Vector2.zero));
        Assert.That(zero.AngleDegrees, Is.Zero);
        Assert.That(zero.IsClampedToEdge, Is.False);
    }

    [Test]
    public void DiagonalProjectionPreservesDirectionAtRectangularEdge()
    {
        MinimapProjectionResult result = MinimapProjection.ProjectMarker(
            Vector2.zero,
            new Vector2(100f, 100f),
            1f,
            1f,
            new Vector2(160f, 100f),
            Marker,
            5f);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsClampedToEdge, Is.True);
        // Useful half-extents are (160, 100) / 2 - marker / 2 - margin = (70, 40).
        Assert.That(result.Position, Is.EqualTo(new Vector2(40f, 40f)).Within(0.0001f));
        Assert.That(result.AngleDegrees, Is.EqualTo(45f).Within(0.0001f));
    }

    [Test]
    public void InvalidGeometryNeverReturnsNonFiniteValues()
    {
        MinimapProjectionResult result = MinimapProjection.ProjectMarker(
            new Vector2(float.NaN, 0f),
            Vector2.zero,
            1f,
            1f,
            Viewport,
            Marker,
            0f);
        MinimapProjectionResult invalidMargin = MinimapProjection.ProjectMarker(
            Vector2.zero,
            Vector2.right,
            1f,
            1f,
            Viewport,
            Marker,
            50f);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Position, Is.EqualTo(Vector2.zero));
        Assert.That(result.AngleDegrees, Is.Zero);
        Assert.That(invalidMargin.IsValid, Is.False);
        Assert.That(invalidMargin.Position, Is.EqualTo(Vector2.zero));
    }
}

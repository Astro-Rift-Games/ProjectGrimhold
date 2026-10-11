using NUnit.Framework;
using UnityEngine;

public sealed class ProtectiveOrbsRulesTests
{
    // This slice contains only value-based rules. Callers must validate an enemy impact before consumption.
    // No impact eligibility, damage, CC/Downed, defeat, authority or migration contract is tested here.
    // A uint provides a technical capacity of 32 original slots, indexed 0..31, not a balance limit.
    private const float Tolerance = 1e-5f;

    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_EightOrbsAndDefaultFifteenSeconds_IsValid()
    {
        // Radius and angular speed are illustrative tuning, not authored balance defaults.
        Assert.That(ProtectiveOrbsRules.TryValidateConfiguration(8, 1.5f, 30f, 15f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_ZeroLifetimeMeansPermanent_IsValid()
    {
        // Zero is the permanent-duration sentinel, not a timer expiring immediately.
        // Timer creation/expiry is deliberately left to the eventual effect owner.
        Assert.That(ProtectiveOrbsRules.TryValidateConfiguration(8, 1.5f, 30f, 0f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(1)]
    [TestCase(32)]
    public void TryValidateConfiguration_CountWithinTechnicalCapacity_IsValid(int initialCount)
    {
        Assert.That(ProtectiveOrbsRules.TryValidateConfiguration(initialCount, 1.5f, 30f, 15f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(0f)]
    [TestCase(-30f)]
    public void TryValidateConfiguration_FiniteZeroOrNegativeAngularSpeed_IsValid(float angularSpeedDegreesPerSecond)
    {
        Assert.That(ProtectiveOrbsRules.TryValidateConfiguration(8, 1.5f, angularSpeedDegreesPerSecond, 15f,
            out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    // initialCount, radius, angular speed (degrees/second), lifetime (seconds; zero = permanent)
    [TestCase(0, 1.5f, 30f, 15f)]
    [TestCase(-1, 1.5f, 30f, 15f)]
    [TestCase(33, 1.5f, 30f, 15f)]
    [TestCase(int.MaxValue, 1.5f, 30f, 15f)]
    [TestCase(8, 0f, 30f, 15f)]
    [TestCase(8, -1f, 30f, 15f)]
    [TestCase(8, float.NaN, 30f, 15f)]
    [TestCase(8, float.PositiveInfinity, 30f, 15f)]
    [TestCase(8, float.NegativeInfinity, 30f, 15f)]
    [TestCase(8, 1.5f, float.NaN, 15f)]
    [TestCase(8, 1.5f, float.PositiveInfinity, 15f)]
    [TestCase(8, 1.5f, float.NegativeInfinity, 15f)]
    [TestCase(8, 1.5f, 30f, -1f)]
    [TestCase(8, 1.5f, 30f, float.NaN)]
    [TestCase(8, 1.5f, 30f, float.PositiveInfinity)]
    [TestCase(8, 1.5f, 30f, float.NegativeInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(int initialCount, float orbitRadius,
        float angularSpeedDegreesPerSecond, float lifetimeSeconds)
    {
        Assert.That(ProtectiveOrbsRules.TryValidateConfiguration(initialCount, orbitRadius,
            angularSpeedDegreesPerSecond, lifetimeSeconds, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    // ---- Initial slot mask ---------------------------------------------------------------

    [Test]
    public void MaxOrbCount_IsTheUintTechnicalCapacity()
    {
        Assert.That(ProtectiveOrbsRules.MaxOrbCount, Is.EqualTo(32));
    }

    [TestCase(1, 0x00000001u)]
    [TestCase(8, 0x000000FFu)]
    [TestCase(31, 0x7FFFFFFFu)]
    [TestCase(32, uint.MaxValue)]
    public void TryCreateFullMask_ValidCount_SetsExactlyTheOriginalSlots(int initialCount, uint expectedMask)
    {
        Assert.That(ProtectiveOrbsRules.TryCreateFullMask(initialCount, out uint activeMask), Is.True);
        Assert.That(activeMask, Is.EqualTo(expectedMask));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(33)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void TryCreateFullMask_InvalidCount_IsRejectedWithZeroMask(int initialCount)
    {
        Assert.That(ProtectiveOrbsRules.TryCreateFullMask(initialCount, out uint activeMask), Is.False);
        Assert.That(activeMask, Is.Zero);
    }

    // ---- Single-slot consumption ---------------------------------------------------------

    [Test]
    public void TryConsumeSlot_ActiveSlot_ClearsOnlyTheInvolvedOrb()
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0xFFu, 8, 3, out uint remainingMask), Is.True);
        Assert.That(remainingMask, Is.EqualTo(0xF7u));
    }

    [Test]
    public void TryConsumeSlot_RepeatedSlot_IsRejectedAndLeavesMaskUnchanged()
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0xFFu, 8, 3, out uint remainingMask), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(remainingMask, 8, 3, out uint repeatedMask), Is.False);
        Assert.That(repeatedMask, Is.EqualTo(remainingMask));
    }

    [Test]
    public void TryConsumeSlot_DifferentSlots_ConsumeIndependentlyInEitherOrder()
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0xFFu, 8, 3, out uint firstOrder), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(firstOrder, 8, 5, out uint firstResult), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0xFFu, 8, 5, out uint reverseOrder), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(reverseOrder, 8, 3, out uint reverseResult), Is.True);
        Assert.That(firstResult, Is.EqualTo(0xD7u));
        Assert.That(reverseResult, Is.EqualTo(firstResult));
    }

    [Test]
    public void TryConsumeSlot_SeparateMaskValues_DoNotShareConsumptionState()
    {
        const uint initialMask = 0xFFu;
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(initialMask, 8, 3, out uint firstMask), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(initialMask, 8, 3, out uint secondMask), Is.True);
        Assert.That(initialMask, Is.EqualTo(0xFFu));
        Assert.That(secondMask, Is.EqualTo(firstMask));
    }

    [TestCase(0, 0xFFFFFFFEu)]
    [TestCase(31, 0x7FFFFFFFu)]
    public void TryConsumeSlot_FullCapacityBoundary_ClearsOnlyTheRequestedBit(int slotIndex, uint expectedMask)
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(uint.MaxValue, 32, slotIndex, out uint remainingMask), Is.True);
        Assert.That(remainingMask, Is.EqualTo(expectedMask));
    }

    [Test]
    public void TryConsumeSlot_LastActiveOrb_ReturnsAnEmptyMask()
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0x80u, 8, 7, out uint remainingMask), Is.True);
        Assert.That(remainingMask, Is.Zero);
    }

    [Test]
    public void TryConsumeSlot_EmptyMask_IsRejectedAndRemainsEmpty()
    {
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0u, 8, 3, out uint remainingMask), Is.False);
        Assert.That(remainingMask, Is.Zero);
    }

    [TestCase(8, -1)]
    [TestCase(8, 8)]
    [TestCase(8, 31)]
    [TestCase(32, 32)]
    [TestCase(32, int.MinValue)]
    [TestCase(32, int.MaxValue)]
    [TestCase(0, 0)]
    [TestCase(-1, 0)]
    [TestCase(33, 0)]
    [TestCase(int.MaxValue, 0)]
    public void TryConsumeSlot_InvalidCountOrIndex_IsRejectedWithoutChangingAnyBits(int initialCount, int slotIndex)
    {
        // Include high bits so an unchecked shift or out-of-count slot cannot go unnoticed.
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(uint.MaxValue, initialCount, slotIndex,
            out uint remainingMask), Is.False);
        Assert.That(remainingMask, Is.EqualTo(uint.MaxValue));
    }

    // ---- Original-slot orbit -------------------------------------------------------------

    // At time zero, slot zero points right; slots increase counterclockwise in the XY plane.
    [TestCase(0, 2f, 0f)]
    [TestCase(1, 1.41421356f, 1.41421356f)]
    [TestCase(2, 0f, 2f)]
    [TestCase(3, -1.41421356f, 1.41421356f)]
    [TestCase(4, -2f, 0f)]
    [TestCase(5, -1.41421356f, -1.41421356f)]
    [TestCase(6, 0f, -2f)]
    [TestCase(7, 1.41421356f, -1.41421356f)]
    public void TryComputeOrbitOffset_EightOriginalSlots_AreEvenlySpaced(int slotIndex, float expectedX, float expectedY)
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(slotIndex, 8, 2f, 30f, 0f, out Vector2 offset), Is.True);
        AssertOffset(offset, expectedX, expectedY);
    }

    [TestCase(90f, 1f, 0f, 2f)]
    [TestCase(90f, 2f, -2f, 0f)]
    [TestCase(90f, 4f, 2f, 0f)]
    [TestCase(-90f, 1f, 0f, -2f)]
    [TestCase(0f, 100f, 2f, 0f)]
    [TestCase(360f, 16777216f, 2f, 0f)]
    public void TryComputeOrbitOffset_ElapsedTime_RotatesAtConfiguredSignedSpeed(float angularSpeedDegreesPerSecond,
        float elapsedSeconds, float expectedX, float expectedY)
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(0, 8, 2f, angularSpeedDegreesPerSecond, elapsedSeconds,
            out Vector2 offset), Is.True);
        AssertOffset(offset, expectedX, expectedY);
    }

    [Test]
    public void TryComputeOrbitOffset_SameInputsAfterOtherQueries_ReturnsExactlyTheSameOffset()
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(3, 8, 1.5f, 30f, 2.25f, out Vector2 first), Is.True);
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(0, 1, 4f, -90f, 10f, out _), Is.True);
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(3, 8, 1.5f, 30f, 2.25f, out Vector2 repeated), Is.True);
        Assert.That(repeated, Is.EqualTo(first));
        Assert.That(first.magnitude, Is.EqualTo(1.5f).Within(Tolerance));
    }

    [TestCase(float.MaxValue, float.MaxValue)]
    [TestCase(float.MinValue, float.MaxValue)]
    public void TryComputeOrbitOffset_ExtremeFiniteRotation_PreservesRadiusAndOriginalSlotSpacing(
        float angularSpeedDegreesPerSecond, float elapsedSeconds)
    {
        // The speed/time product exceeds float capacity. Normalize rotation before adding slot phase,
        // otherwise even double arithmetic can lose the small original-slot angle at this magnitude.
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(0, 8, 2f, angularSpeedDegreesPerSecond, elapsedSeconds,
            out Vector2 first), Is.True);
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(2, 8, 2f, angularSpeedDegreesPerSecond, elapsedSeconds,
            out Vector2 quarterTurn), Is.True);
        Assert.That(first.magnitude, Is.EqualTo(2f).Within(Tolerance));
        Assert.That(quarterTurn.magnitude, Is.EqualTo(2f).Within(Tolerance));
        AssertOffset(quarterTurn, -first.y, first.x);
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(0, 8, 2f, angularSpeedDegreesPerSecond, elapsedSeconds,
            out Vector2 repeated), Is.True);
        Assert.That(repeated, Is.EqualTo(first));
    }

    [Test]
    public void TryComputeOrbitOffset_MaximumFiniteRadius_ReturnsFiniteComponents()
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(0, 8, float.MaxValue, 0f, 0f,
            out Vector2 offset), Is.True);
        Assert.That(offset.x, Is.EqualTo(float.MaxValue));
        Assert.That(offset.y, Is.Zero);
    }

    [Test]
    public void TryComputeOrbitOffset_ConsumedSlotHoles_DoNotRenumberOrRedistributeSurvivors()
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(2, 8, 2f, 90f, 1f, out Vector2 before), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(0xFFu, 8, 0, out uint firstHole), Is.True);
        Assert.That(ProtectiveOrbsRules.TryConsumeSlot(firstHole, 8, 1, out uint secondHole), Is.True);
        Assert.That(secondHole, Is.EqualTo(0xFCu));

        // The surviving orb retains original index 2 and initial count 8, not live rank 0 and count 6.
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(2, 8, 2f, 90f, 1f, out Vector2 after), Is.True);
        Assert.That(after, Is.EqualTo(before));
        AssertOffset(after, -2f, 0f);
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(2, 8, 2f, 90f, 2f, out Vector2 later), Is.True);
        AssertOffset(later, 0f, -2f);
    }

    [TestCase(0, 1, 2f, 0f)]
    [TestCase(16, 32, -2f, 0f)]
    public void TryComputeOrbitOffset_CountBoundary_UsesOriginalEvenSpacing(int slotIndex, int initialCount,
        float expectedX, float expectedY)
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(slotIndex, initialCount, 2f, 30f, 0f,
            out Vector2 offset), Is.True);
        AssertOffset(offset, expectedX, expectedY);
    }

    // slot index, initial count, radius, angular speed (degrees/second), elapsed seconds
    [TestCase(-1, 8, 2f, 30f, 1f)]
    [TestCase(8, 8, 2f, 30f, 1f)]
    [TestCase(32, 32, 2f, 30f, 1f)]
    [TestCase(int.MaxValue, 8, 2f, 30f, 1f)]
    [TestCase(0, 0, 2f, 30f, 1f)]
    [TestCase(0, -1, 2f, 30f, 1f)]
    [TestCase(0, 33, 2f, 30f, 1f)]
    [TestCase(0, 8, 0f, 30f, 1f)]
    [TestCase(0, 8, -1f, 30f, 1f)]
    [TestCase(0, 8, float.NaN, 30f, 1f)]
    [TestCase(0, 8, float.PositiveInfinity, 30f, 1f)]
    [TestCase(0, 8, float.NegativeInfinity, 30f, 1f)]
    [TestCase(0, 8, 2f, float.NaN, 1f)]
    [TestCase(0, 8, 2f, float.PositiveInfinity, 1f)]
    [TestCase(0, 8, 2f, float.NegativeInfinity, 1f)]
    [TestCase(0, 8, 2f, 30f, -1f)]
    [TestCase(0, 8, 2f, 30f, float.NaN)]
    [TestCase(0, 8, 2f, 30f, float.PositiveInfinity)]
    [TestCase(0, 8, 2f, 30f, float.NegativeInfinity)]
    public void TryComputeOrbitOffset_InvalidInput_IsRejectedWithZeroOffset(int slotIndex, int initialCount,
        float orbitRadius, float angularSpeedDegreesPerSecond, float elapsedSeconds)
    {
        Assert.That(ProtectiveOrbsRules.TryComputeOrbitOffset(slotIndex, initialCount, orbitRadius,
            angularSpeedDegreesPerSecond, elapsedSeconds, out Vector2 offset), Is.False);
        Assert.That(offset, Is.EqualTo(Vector2.zero));
    }

    private static void AssertOffset(Vector2 actual, float expectedX, float expectedY)
    {
        Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
    }
}

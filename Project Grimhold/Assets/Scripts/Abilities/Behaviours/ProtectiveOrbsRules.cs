using System;
using UnityEngine;

/// <summary>
/// Pure, stateless protective-orb configuration, original-slot orbit and consumption rules.
/// No scene, clock or Fusion state is read here. Impact eligibility and effect lifecycle belong to callers.
/// </summary>
internal static class ProtectiveOrbsRules
{
    /// <summary>Technical capacity of a uint mask: 32 original slots indexed 0..31, not a balance limit.</summary>
    public const int MaxOrbCount = 32;

    /// <summary>Zero lifetime means permanent. Angular speed is signed degrees per second and may be zero.</summary>
    public static bool TryValidateConfiguration(int initialCount, float orbitRadius,
        float angularSpeedDegreesPerSecond, float lifetimeSeconds, out string error)
    {
        if (!IsValidCount(initialCount))
            error = "Protective orb count must be between one and 32 (the mask capacity).";
        else if (!IsPositiveFinite(orbitRadius))
            error = "Protective orb radius must be finite and greater than zero.";
        else if (!IsFinite(angularSpeedDegreesPerSecond))
            error = "Protective orb angular speed must be finite.";
        else if (!IsFinite(lifetimeSeconds) || lifetimeSeconds < 0f)
            error = "Protective orb lifetime must be finite and not negative; zero means permanent.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>Sets one bit per original slot. Invalid counts return false with a zero mask.</summary>
    public static bool TryCreateFullMask(int initialCount, out uint activeMask)
    {
        activeMask = 0u;
        if (!IsValidCount(initialCount)) return false;
        // C# masks uint shift counts to five bits: shifting by 32 would incorrectly shift by zero.
        activeMask = initialCount == MaxOrbCount ? uint.MaxValue : (1u << initialCount) - 1u;
        return true;
    }

    /// <summary>
    /// Clears only the requested active slot after the caller has validated an enemy impact.
    /// Returns false with the supplied mask unchanged for invalid counts/indexes or already-consumed slots.
    /// Does not validate an impact, apply damage or decide authority, CC/Downed persistence or effect lifetime.
    /// </summary>
    public static bool TryConsumeSlot(uint activeMask, int initialCount, int slotIndex, out uint remainingMask)
    {
        remainingMask = activeMask;
        if (!IsValidCount(initialCount) || slotIndex < 0 || slotIndex >= initialCount) return false;
        uint slotBit = 1u << slotIndex;
        if ((activeMask & slotBit) == 0u) return false;
        remainingMask = activeMask & ~slotBit;
        return true;
    }

    /// <summary>
    /// Caster-relative XY offset using the stable original slot index and initial count, never the surviving count.
    /// Slot zero starts right; positive angular speed rotates counterclockwise. The caller supplies finite,
    /// nonnegative elapsed simulation seconds. Invalid inputs return false with a zero offset.
    /// This allocation-free calculation does not search for a target or read mutable runtime state.
    /// </summary>
    public static bool TryComputeOrbitOffset(int slotIndex, int initialCount, float orbitRadius,
        float angularSpeedDegreesPerSecond, float elapsedSeconds, out Vector2 offset)
    {
        offset = Vector2.zero;
        if (!IsValidCount(initialCount) || slotIndex < 0 || slotIndex >= initialCount ||
            !IsPositiveFinite(orbitRadius) || !IsFinite(angularSpeedDegreesPerSecond) ||
            !IsFinite(elapsedSeconds) || elapsedSeconds < 0f) return false;

        // Promote before multiplying: even the product of two maximum finite floats fits in a double.
        // Reduce rotation before adding slot phase so a large elapsed angle cannot erase slot spacing.
        double rotationDegrees = ((double)angularSpeedDegreesPerSecond * elapsedSeconds) % 360d;
        if (rotationDegrees < 0d) rotationDegrees += 360d;
        double angleDegrees = (rotationDegrees + 360d * slotIndex / initialCount) % 360d;
        double angleRadians = angleDegrees * (Math.PI / 180d);
        offset = new Vector2((float)(Math.Cos(angleRadians) * orbitRadius),
            (float)(Math.Sin(angleRadians) * orbitRadius));
        return true;
    }

    private static bool IsValidCount(int count) => count >= 1 && count <= MaxOrbCount;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
}

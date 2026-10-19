using System;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Pure fog ray-fan layout rules: dense facing-cone rays and sparse near-radius rays
    /// around the remaining circle, with paired rays at the cone boundaries.
    /// </summary>
    public static class VisionFogFanRules
    {
        /// <summary>
        /// Returns an upper bound for the ray count at any cone angle, with steps clamped to 0.1°.
        /// </summary>
        public static int MaxRayCount(float coneStepDegrees, float outerStepDegrees)
        {
            return Mathf.CeilToInt(360f / Mathf.Max(coneStepDegrees, 0.1f)) +
                   Mathf.CeilToInt(360f / Mathf.Max(outerStepDegrees, 0.1f)) + 2;
        }

        /// <summary>
        /// Writes counter-clockwise, unwrapped angles and cone membership into the supplied buffers.
        /// Null or undersized buffers are programmer errors and require an ArgumentException.
        /// </summary>
        public static int BuildRayAngles(
            float facingDegrees,
            float coneAngleDegrees,
            float coneStepDegrees,
            float outerStepDegrees,
            float[] anglesDegrees,
            bool[] inCone)
        {
            float half = Mathf.Clamp(coneAngleDegrees, 0f, 360f) / 2f;
            float coneStep = Mathf.Max(coneStepDegrees, 0.1f);
            float outerStep = Mathf.Max(outerStepDegrees, 0.1f);
            float outerSpan = 360f - 2f * half;
            int coneSegments = half > 0f
                ? Mathf.Max(1, Mathf.CeilToInt(2f * half / coneStep - 1e-4f))
                : 0;
            int outerSegments = half < 180f
                ? Mathf.Max(1, Mathf.CeilToInt(outerSpan / outerStep - 1e-4f))
                : 0;
            int coneCount = coneSegments > 0 ? coneSegments + 1 : 0;
            int outerCount = outerSegments > 0 ? outerSegments + (half > 0f ? 1 : 0) : 0;
            int requiredCount = coneCount + outerCount;

            if (anglesDegrees == null || anglesDegrees.Length < requiredCount)
            {
                throw new ArgumentException("Buffer is null or too small.", nameof(anglesDegrees));
            }

            if (inCone == null || inCone.Length < requiredCount)
            {
                throw new ArgumentException("Buffer is null or too small.", nameof(inCone));
            }

            int count = 0;
            for (int i = 0; i < coneCount; i++)
            {
                anglesDegrees[count] = facingDegrees - half + i * (2f * half / coneSegments);
                inCone[count] = true;
                count++;
            }

            for (int j = 0; j < outerCount; j++)
            {
                anglesDegrees[count] = facingDegrees + half + j * (outerSpan / outerSegments);
                inCone[count] = false;
                count++;
            }

            return count;
        }
    }
}

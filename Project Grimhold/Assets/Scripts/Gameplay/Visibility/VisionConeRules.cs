using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Pure vision-shape rules: a short 360° near radius plus a facing-oriented cone with
    /// unlimited range. Occlusion, the only distance limit, is resolved by <see cref="VisionQuery"/>.
    /// </summary>
    public static class VisionConeRules
    {
        private const float MinimumFacingSqrMagnitude = 0.0001f;

        /// <summary>
        /// Converts a full cone angle in degrees into the cosine of its half angle.
        /// A 360° cone yields -1, so every direction passes the angle test.
        /// </summary>
        public static float CosineOfHalfAngle(float coneAngleDegrees)
        {
            float halfAngle = Mathf.Clamp(coneAngleDegrees, 0f, 360f) * 0.5f;
            return Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Normalizes <paramref name="facing"/>, or returns the normalized
        /// <paramref name="fallback"/> when the facing has no usable direction.
        /// </summary>
        public static Vector2 ResolveFacing(Vector2 facing, Vector2 fallback)
        {
            if (facing.sqrMagnitude >= MinimumFacingSqrMagnitude)
            {
                return facing.normalized;
            }

            return fallback.sqrMagnitude >= MinimumFacingSqrMagnitude ? fallback.normalized : Vector2.down;
        }

        /// <summary>
        /// Returns whether <paramref name="point"/> lies inside the near radius, or inside
        /// the unlimited-range cone around a normalized <paramref name="facing"/>.
        /// </summary>
        public static bool IsWithinVisionShape(
            Vector2 origin,
            Vector2 facing,
            Vector2 point,
            float nearRadius,
            float coneHalfAngleCosine)
        {
            Vector2 offset = point - origin;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance <= MinimumFacingSqrMagnitude || sqrDistance <= nearRadius * nearRadius)
            {
                return true;
            }

            float cosine = Vector2.Dot(facing, offset) / Mathf.Sqrt(sqrDistance);
            return cosine >= coneHalfAngleCosine;
        }
    }
}

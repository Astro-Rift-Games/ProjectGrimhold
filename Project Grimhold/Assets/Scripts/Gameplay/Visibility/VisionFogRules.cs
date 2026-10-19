using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Pure fog-presentation rules: smoothed cone facing, viewer clear strength and
    /// orthographic coverage. These rules do not change authoritative vision queries.
    /// </summary>
    public static class VisionFogRules
    {
        /// <summary>
        /// Smooths facing along the shortest arc using frame-rate independent exponential smoothing.
        /// </summary>
        public static Vector2 SmoothFacing(Vector2 current, Vector2 target, float sharpness, float deltaTime)
        {
            Vector2 resolvedTarget = VisionConeRules.ResolveFacing(target, current);
            if (current.sqrMagnitude < 0.0001f || sharpness <= 0f)
            {
                return resolvedTarget;
            }

            float currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(resolvedTarget.y, resolvedTarget.x) * Mathf.Rad2Deg;
            float t = 1f - Mathf.Exp(-sharpness * deltaTime);
            float angle = Mathf.LerpAngle(currentAngle, targetAngle, t) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>
        /// Moves fog clear strength toward viewer presence over the configured fade duration.
        /// </summary>
        public static float StepClearStrength(float current, bool hasViewer, float fadeSeconds, float deltaTime)
        {
            if (fadeSeconds <= 0f)
            {
                return hasViewer ? 1f : 0f;
            }

            float step = deltaTime / fadeSeconds;
            return Mathf.Clamp01(current + (hasViewer ? step : -step));
        }

        /// <summary>
        /// Returns orthographic view width and height with a non-negative margin on every side.
        /// </summary>
        public static Vector2 OrthographicCoverage(float orthographicSize, float aspect, float margin)
        {
            float height = orthographicSize * 2f;
            float padding = Mathf.Max(0f, margin) * 2f;
            return new Vector2(height * aspect + padding, height + padding);
        }
    }
}

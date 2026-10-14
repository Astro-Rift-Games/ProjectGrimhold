using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Balance values for local vision: the facing cone, the near radius and how
    /// visibility changes fade. The cone has no range limit; only obstacles stop it.
    /// </summary>
    [CreateAssetMenu(menuName = "Grimhold/Visibility/Settings", fileName = "VisibilitySettings")]
    public sealed class VisibilitySettings : ScriptableObject
    {
        [Header("Shape")]
        [Tooltip("Radius around the viewer where everything is visible in every direction, unless an obstacle blocks it.")]
        [Min(0f)]
        public float NearRadius = 2f;

        [Tooltip("Full opening angle of the facing cone, in degrees. The cone reaches until an obstacle blocks it.")]
        [Range(0f, 360f)]
        public float ConeAngleDegrees = 90f;

        [Tooltip("Layers whose colliders block vision (walls, doors, obstacles).")]
        public LayerMask ObstacleLayer;

        [Header("Transitions")]
        [Tooltip("Seconds an entity stays fully visible after leaving sight, before it starts fading out.")]
        [Min(0f)]
        public float GraceSeconds = 0.3f;

        [Tooltip("Seconds an entity takes to fade in when it comes into sight.")]
        [Min(0f)]
        public float FadeInSeconds = 0.15f;

        [Tooltip("Seconds an entity takes to fade out once its grace period ends.")]
        [Min(0f)]
        public float FadeOutSeconds = 0.4f;

        public VisibilityFadeTimings FadeTimings => new VisibilityFadeTimings(GraceSeconds, FadeInSeconds, FadeOutSeconds);

        private void OnValidate()
        {
            NearRadius = Mathf.Max(0f, NearRadius);
            ConeAngleDegrees = Mathf.Clamp(ConeAngleDegrees, 0f, 360f);
            GraceSeconds = Mathf.Max(0f, GraceSeconds);
            FadeInSeconds = Mathf.Max(0f, FadeInSeconds);
            FadeOutSeconds = Mathf.Max(0f, FadeOutSeconds);
        }
    }
}

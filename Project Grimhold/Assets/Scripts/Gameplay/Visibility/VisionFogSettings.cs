using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>Art-only settings for local fog; vision shape remains in VisibilitySettings.</summary>
    [CreateAssetMenu(menuName = "Grimhold/Visibility/Fog Settings", fileName = "VisionFogSettings")]
    public sealed class VisionFogSettings : ScriptableObject
    {
        [Header("Look")]
        [Tooltip("Fog tint; alpha is the maximum density.")]
        public Color FogColor = new Color(0.05f, 0.06f, 0.08f, 0.85f);
        [Tooltip("Density retained inside the clear shape, relative to maximum density.")]
        [Range(0f, 1f)] public float InsideDensity = 0.06f;
        [Tooltip("Noise frequency in world space.")]
        [Min(0f)] public float NoiseScale = 0.35f;
        [Tooltip("Strength of density variation.")]
        [Range(0f, 1f)] public float NoiseStrength = 0.45f;
        [Tooltip("Noise drift in world units per second.")]
        public Vector2 WindVelocity = new Vector2(0.15f, 0.05f);
        [Tooltip("World-space pixel snapping; zero disables it. Most project sprites use 16 PPU.")]
        [Min(0f)] public float PixelsPerUnit = 16f;

        [Header("Edges")]
        [Tooltip("Radial edge softness in world units.")]
        [Min(0f)] public float RadialSoftness = 1.5f;
        [Tooltip("Cone edge softness in degrees.")]
        [Range(0f, 90f)] public float AngularSoftnessDegrees = 12f;
        [Tooltip("World units of noise-driven edge breakup.")]
        [Min(0f)] public float EdgeNoiseAmplitude = 0.6f;

        [Header("Distance")]
        [Tooltip("Distance where fog starts returning inside the unlimited cone.")]
        [Min(0f)] public float FarFogStart = 8f;
        [Tooltip("Distance where returning fog reaches its configured density.")]
        [Min(0f)] public float FarFogEnd = 16f;
        [Tooltip("Fog returned at distance inside the cone.")]
        [Range(0f, 1f)] public float FarFogDensity = 0.35f;

        [Header("Motion")]
        [Tooltip("Facing smoothing sharpness; zero disables smoothing.")]
        [Min(0f)] public float FacingSharpness = 12f;
        [Tooltip("Seconds to fade the clear shape in or out when viewer presence changes.")]
        [Min(0f)] public float ClearFadeSeconds = 0.25f;
        [Tooltip("Extra world-space coverage on each side of the camera.")]
        [Min(0f)] public float CoverageMargin = 1f;

        private void OnValidate()
        {
            FogColor.a = Mathf.Clamp01(FogColor.a);
            InsideDensity = Mathf.Clamp01(InsideDensity);
            NoiseScale = Mathf.Max(0f, NoiseScale);
            NoiseStrength = Mathf.Clamp01(NoiseStrength);
            PixelsPerUnit = Mathf.Max(0f, PixelsPerUnit);
            RadialSoftness = Mathf.Max(0f, RadialSoftness);
            AngularSoftnessDegrees = Mathf.Clamp(AngularSoftnessDegrees, 0f, 90f);
            EdgeNoiseAmplitude = Mathf.Max(0f, EdgeNoiseAmplitude);
            FarFogStart = Mathf.Max(0f, FarFogStart);
            FarFogEnd = Mathf.Max(FarFogStart, FarFogEnd);
            FarFogDensity = Mathf.Clamp01(FarFogDensity);
            FacingSharpness = Mathf.Max(0f, FacingSharpness);
            ClearFadeSeconds = Mathf.Max(0f, ClearFadeSeconds);
            CoverageMargin = Mathf.Max(0f, CoverageMargin);
        }
    }
}

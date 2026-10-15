using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>Art-only settings for local fog; vision shape remains in VisibilitySettings.</summary>
    [CreateAssetMenu(menuName = "Grimhold/Visibility/Fog Settings", fileName = "VisionFogSettings")]
    public sealed class VisionFogSettings : ScriptableObject
    {
        [Header("Look")]
        [Tooltip("Fog tint; alpha is the maximum density.")]
        public Color FogColor = new Color(0.16f, 0.17f, 0.2f, 0.93f);
        [Tooltip("Density retained inside the clear shape, relative to maximum density.")]
        [Range(0f, 1f)] public float InsideDensity = 0.06f;
        [Tooltip("Noise frequency in world space.")]
        [Min(0f)] public float NoiseScale = 0.35f;
        [Tooltip("Strength of density variation.")]
        [Range(0f, 1f)] public float NoiseStrength = 0.3f;
        [Tooltip("Noise drift in world units per second.")]
        public Vector2 WindVelocity = new Vector2(0.15f, 0.05f);
        [Tooltip("World-space pixel snapping; zero disables it. Most project sprites use 16 PPU.")]
        [Min(0f)] public float PixelsPerUnit = 16f;

        [Header("Clear Shape")]
        [Tooltip("World width of the soft fade inside the visible area.")]
        [Min(0f)] public float EdgeSoftness = 1.5f;
        [Tooltip("Optional; clears this far into the blocking wall.")]
        [Min(0f)] public float WallRevealDepth = 0f;
        [Range(0.25f, 10f)] public float ConeRayStepDegrees = 1f;
        [Range(1f, 45f)] public float OuterRayStepDegrees = 6f;
        [Range(0.1f, 1f)] public float MaskResolutionScale = 0.25f;
        [Tooltip("How much noise erodes the clear edge inward; never extends clear onto walls.")]
        [Range(0f, 0.45f)] public float EdgeBreakup = 0.25f;

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
            EdgeSoftness = Mathf.Max(0f, EdgeSoftness);
            WallRevealDepth = Mathf.Max(0f, WallRevealDepth);
            ConeRayStepDegrees = Mathf.Clamp(ConeRayStepDegrees, 0.25f, 10f);
            OuterRayStepDegrees = Mathf.Clamp(OuterRayStepDegrees, 1f, 45f);
            MaskResolutionScale = Mathf.Clamp(MaskResolutionScale, 0.1f, 1f);
            EdgeBreakup = Mathf.Clamp(EdgeBreakup, 0f, 0.45f);
            FarFogStart = Mathf.Max(0f, FarFogStart);
            FarFogEnd = Mathf.Max(FarFogStart, FarFogEnd);
            FarFogDensity = Mathf.Clamp01(FarFogDensity);
            FacingSharpness = Mathf.Max(0f, FacingSharpness);
            ClearFadeSeconds = Mathf.Max(0f, ClearFadeSeconds);
            CoverageMargin = Mathf.Max(0f, CoverageMargin);
        }
    }
}

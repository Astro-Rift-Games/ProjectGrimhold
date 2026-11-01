using UnityEngine;

/// <summary>
/// One evaluated frame of the Downed post-process. <see cref="Weight"/> drives the Volume weight,
/// which blends every override against the scene; the other values are the full-weight targets.
/// </summary>
public readonly struct DownedPostProcessFrame
{
    public DownedPostProcessFrame(
        float weight,
        float saturation,
        float contrast,
        float postExposure,
        Color vignetteColor,
        float vignetteIntensity,
        float vignetteSmoothness)
    {
        Weight = weight;
        Saturation = saturation;
        Contrast = contrast;
        PostExposure = postExposure;
        VignetteColor = vignetteColor;
        VignetteIntensity = vignetteIntensity;
        VignetteSmoothness = vignetteSmoothness;
    }

    public float Weight { get; }
    public float Saturation { get; }
    public float Contrast { get; }
    public float PostExposure { get; }
    public Color VignetteColor { get; }
    public float VignetteIntensity { get; }
    public float VignetteSmoothness { get; }

    public bool IsActive => Weight > 0f;
}

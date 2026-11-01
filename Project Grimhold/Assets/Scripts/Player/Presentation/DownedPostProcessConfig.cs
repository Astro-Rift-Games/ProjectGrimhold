using UnityEngine;

/// <summary>
/// Immutable tuning for the local Downed black-and-white post-process: blend timing, greyscale
/// contrast and vignette, the reserve-driven darkening and the revive colour recovery.
/// Local presentation only; never replicated or mutated at runtime.
/// </summary>
[CreateAssetMenu(fileName = "DownedPostProcessConfig", menuName = "Grimhold/Presentation/DownedPostProcessConfig")]
public sealed class DownedPostProcessConfig : ScriptableObject
{
    [Header("Transitions")]
    [Tooltip("Seconds to blend from full colour to black and white on Active -> Downed.")]
    [SerializeField, Range(0.3f, 0.5f)]
    private float _fadeInSeconds = 0.4f;

    [Tooltip("Seconds to blend back to full colour on Downed -> Active or definitive Defeat.")]
    [SerializeField, Range(0.05f, 2f)]
    private float _fadeOutSeconds = 0.4f;

    [Tooltip("Maps linear transition progress [0, 1] to effect weight [0, 1].")]
    [SerializeField]
    private AnimationCurve _blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Black and White")]
    [SerializeField, Range(-100f, 0f)]
    private float _saturation = -100f;

    [Tooltip("Balanced contrast keeps corridors, obstacles, allies and threats readable.")]
    [SerializeField, Range(-100f, 100f)]
    private float _contrast = 20f;

    [SerializeField]
    private Color _vignetteColor = Color.black;

    [Tooltip("Soft perimeter vignette with a full Downed reserve.")]
    [SerializeField, Range(0f, 1f)]
    private float _vignetteIntensity = 0.25f;

    [SerializeField, Range(0.01f, 1f)]
    private float _vignetteSmoothness = 0.5f;

    [Header("Downed Health Reserve")]
    [Tooltip("Extra vignette intensity once the reserve reaches zero.")]
    [SerializeField, Range(0f, 0.75f)]
    private float _depletedVignetteBoost = 0.2f;

    [Tooltip("Exposure dimming (EV) once the reserve reaches zero.")]
    [SerializeField, Range(0f, 2f)]
    private float _depletedExposureDim = 0.5f;

    [Tooltip("Seconds for the reserve modulation to follow damage, so chunks never pop.")]
    [SerializeField, Range(0.05f, 2f)]
    private float _reserveResponseSeconds = 0.3f;

    [Header("Revive Feedback")]
    [Tooltip("Fraction of the original colour recovered at full revive progress.")]
    [SerializeField, Range(0f, 1f)]
    private float _reviveColorRecovery = 0.5f;

    [Tooltip("Seconds for the revive modulation to follow progress, so interruptions never pop.")]
    [SerializeField, Range(0.05f, 2f)]
    private float _reviveResponseSeconds = 0.25f;

    [Header("Volume")]
    [Tooltip("Priority of the runtime Volume; above the scene ambient Volume.")]
    [SerializeField]
    private float _volumePriority = 100f;

    public float FadeInSeconds => _fadeInSeconds;
    public float FadeOutSeconds => _fadeOutSeconds;
    public AnimationCurve BlendCurve => _blendCurve;
    public float Saturation => _saturation;
    public float Contrast => _contrast;
    public Color VignetteColor => _vignetteColor;
    public float VignetteIntensity => _vignetteIntensity;
    public float VignetteSmoothness => _vignetteSmoothness;
    public float DepletedVignetteBoost => _depletedVignetteBoost;
    public float DepletedExposureDim => _depletedExposureDim;
    public float ReserveResponseSeconds => _reserveResponseSeconds;
    public float ReviveColorRecovery => _reviveColorRecovery;
    public float ReviveResponseSeconds => _reviveResponseSeconds;
    public float VolumePriority => _volumePriority;
}

using UnityEngine;

/// <summary>
/// Applies a trauma-based screen-shake offset to the local camera.
///
/// Belongs to the local presentation layer. Not replicated.
/// Lives on the same GameObject as <see cref="LocalCameraController"/>.
///
/// Shake requests add "trauma" (0–1). Each frame, trauma decays and produces
/// a smooth random offset that is read by <see cref="LocalCameraController"/>.
/// Multiple simultaneous requests are additive (trauma clamped to 1).
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraShakeController : MonoBehaviour
{
    /// <summary>
    /// Singleton reference to the active local camera shake controller.
    /// Null when no camera shake controller is present in the scene.
    /// </summary>
    public static CameraShakeController Instance { get; private set; }

    private float _trauma;
    private float _sustainedTrauma;
    private float _traumaDecayPerSecond;
    private float _decayExponent;
    private float _maxOffset;
    private float _noiseOffsetX;
    private float _noiseOffsetY;
    private float _noiseTime;

    /// <summary>
    /// The current positional offset to add to the camera's desired position.
    /// Updated every frame. Read by <see cref="LocalCameraController"/>.
    /// </summary>
    public Vector3 ShakeOffset { get; private set; }

    private static float s_accessibilityScale = 1f;

    /// <summary>
    /// Global accessibility multiplier for screen shake intensity clamped to [0, 1].
    /// Setting this to 0 completely disables camera shake for players sensitive to motion sickness.
    /// Default is 1.
    /// </summary>
    public static float AccessibilityScale
    {
        get => s_accessibilityScale;
        set => s_accessibilityScale = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Current sustained trauma floor clamped to [0, 1].
    /// </summary>
    public float SustainedTrauma => _sustainedTrauma;

    /// <summary>
    /// Effective trauma level (maximum between instantaneous decaying trauma and sustained baseline).
    /// </summary>
    public float EffectiveTrauma => Mathf.Max(_trauma, _sustainedTrauma);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"Another active instance of {nameof(CameraShakeController)} detected. " +
                $"Replacing reference.", this);
        }

        Instance = this;

        // Randomize Perlin noise seed per session to avoid identical patterns.
        _noiseOffsetX = Random.Range(0f, 1000f);
        _noiseOffsetY = Random.Range(0f, 1000f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        _sustainedTrauma = 0f;
        ResetState();
    }

    /// <summary>
    /// Configures the controller with the active shake config.
    /// Must be called once before the first <see cref="RequestShake"/> call.
    /// </summary>
    public void Configure(CameraShakeConfig config)
    {
        Instance = this;
        if (config == null)
        {
            Debug.LogError($"{nameof(CameraShakeController)}: config is null.", this);
            return;
        }

        _decayExponent = config.DecayExponent;
        _maxOffset     = config.MaxOffset;
    }

    /// <summary>
    /// Adds a shake request. Trauma is additive and clamped to [0, 1].
    /// </summary>
    /// <param name="intensity">
    /// Trauma to add, where 1 is maximum shake. Values below 0 are ignored.
    /// </param>
    /// <param name="duration">
    /// Duration in seconds over which trauma decays to zero from <paramref name="intensity"/>.
    /// Values ≤ 0 are treated as instant (single-frame) shake.
    /// </param>
    /// <summary>
    /// Sets a continuous baseline trauma level (clamped to [0, 1]).
    /// While active, the effective trauma will not decay below this level.
    /// Pass 0 to allow trauma to decay to zero normally.
    /// </summary>
    public void SetContinuousShake(float intensity)
    {
        _sustainedTrauma = Mathf.Clamp01(intensity);
    }

    public void RequestShake(float intensity, float duration)
    {
        if (intensity <= 0f)
        {
            return;
        }

        _trauma = Mathf.Clamp01(_trauma + intensity);

        // Decay rate ensures the trauma reaches zero in exactly <duration> seconds
        // under a linear model. The power curve makes the visible motion feel more
        // snappy at high trauma and tail off smoothly.
        if (duration > 0f)
        {
            float decayRate = intensity / duration;

            // Keep the highest requested decay rate so a long soft shake does not
            // slow down the decay of an overlapping hard impact.
            if (decayRate > _traumaDecayPerSecond)
            {
                _traumaDecayPerSecond = decayRate;
            }
        }
        else
        {
            // Instant shake: decay as fast as possible (next frame it will be near zero).
            _traumaDecayPerSecond = float.MaxValue;
        }
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f && _sustainedTrauma <= 0f)
        {
            ShakeOffset = Vector3.zero;
            return;
        }

        // Advance Perlin noise time independently from trauma level.
        _noiseTime += Time.deltaTime * 24f;

        float effectiveTrauma = Mathf.Max(_trauma, _sustainedTrauma);
        float shake = Mathf.Pow(effectiveTrauma, _decayExponent);

        float offsetX = (Mathf.PerlinNoise(_noiseOffsetX + _noiseTime, 0f) * 2f - 1f) * _maxOffset * shake;
        float offsetY = (Mathf.PerlinNoise(_noiseOffsetY, _noiseTime) * 2f - 1f) * _maxOffset * shake;

        ShakeOffset = new Vector3(offsetX, offsetY, 0f) * s_accessibilityScale;

        // Decay trauma after computing the offset.
        float decay = _traumaDecayPerSecond == float.MaxValue
            ? _trauma
            : _traumaDecayPerSecond * Time.deltaTime;

        _trauma = Mathf.Max(0f, _trauma - decay);

        if (_trauma <= 0f && _sustainedTrauma <= 0f)
        {
            ResetState();
        }
    }

    private void ResetState()
    {
        _trauma               = 0f;
        _traumaDecayPerSecond = 0f;
        _noiseTime            = 0f;
        ShakeOffset           = Vector3.zero;
    }
}

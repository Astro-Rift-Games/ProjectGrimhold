using UnityEngine;

/// <summary>Immutable, visual-only tuning for a confirmed melee contact burst.</summary>
[CreateAssetMenu(fileName = "MeleeImpactParticles", menuName = "Grimhold/Combat/Melee Impact Particles")]
public sealed class MeleeImpactParticleProfile : ScriptableObject
{
    [SerializeField, Tooltip("Shared Emit-only, world-space prefab. No stop action, automatic emission or child emitters.")]
    private ParticleSystem _emitterPrefab;
    [SerializeField, Range(1, 256), Tooltip("Minimum number of particles per confirmed contact.")]
    private int _countMin = 4;
    [SerializeField, Range(1, 256), Tooltip("Maximum number of particles per confirmed contact, inclusive.")]
    private int _countMax = 6;
    [SerializeField, Min(0f), Tooltip("Minimum particle speed in world units per second.")]
    private float _speedMin = 1f;
    [SerializeField, Min(0f), Tooltip("Maximum particle speed in world units per second.")]
    private float _speedMax = 2f;
    [SerializeField, Range(0f, 360f), Tooltip("Cone width in degrees, centered on the confirmed hit direction. Zero direction uses a full circle.")]
    private float _spreadDegrees = 70f;
    [SerializeField, Range(1, 4), Tooltip("Minimum square size in whole pixels.")]
    private int _sizePixelsMin = 1;
    [SerializeField, Range(1, 4), Tooltip("Maximum square size in whole pixels, inclusive.")]
    private int _sizePixelsMax = 1;
    [SerializeField, Min(.001f), Tooltip("Pixel grid for contact position and particle size. Project art uses 16 pixels per world unit.")]
    private float _pixelsPerUnit = 16f;
    [SerializeField, Range(.001f, .6f), Tooltip("Minimum lifetime in normal presentation seconds; independent of hitstop.")]
    private float _lifetimeMin = .08f;
    [SerializeField, Range(.001f, .6f), Tooltip("Maximum lifetime in seconds. The prefab supplies the alpha fade.")]
    private float _lifetimeMax = .12f;
    [SerializeField, Tooltip("Initial particle tint. The shared prefab fades alpha over the particle lifetime.")]
    private Color _color = new Color(1f, .2f, .2f, 1f);

    public ParticleSystem EmitterPrefab => _emitterPrefab;
    public int CountMin => _countMin;
    public int CountMax => _countMax;
    public float SpeedMin => _speedMin;
    public float SpeedMax => _speedMax;
    public float SpreadDegrees => _spreadDegrees;
    public int SizePixelsMin => _sizePixelsMin;
    public int SizePixelsMax => _sizePixelsMax;
    public float PixelsPerUnit => _pixelsPerUnit;
    public float LifetimeMin => _lifetimeMin;
    public float LifetimeMax => _lifetimeMax;
    public Color Color => _color;

    public bool TryValidate(out string error)
    {
        if (_emitterPrefab == null)
        {
            error = "Melee impact particles require an emitter prefab.";
            return false;
        }
        if (_countMin < 1 || _countMax < _countMin || _countMax > 256 ||
            !InRange(_speedMin, 0f, float.MaxValue) || !InRange(_speedMax, _speedMin, float.MaxValue) ||
            !InRange(_spreadDegrees, 0f, 360f) ||
            _sizePixelsMin < 1 || _sizePixelsMax < _sizePixelsMin || _sizePixelsMax > 4 ||
            !InRange(_pixelsPerUnit, .001f, float.MaxValue) ||
            !InRange(_lifetimeMin, .001f, .6f) || !InRange(_lifetimeMax, _lifetimeMin, .6f) ||
            !InRange(_color.r, 0f, 1f) || !InRange(_color.g, 0f, 1f) ||
            !InRange(_color.b, 0f, 1f) || !InRange(_color.a, 0f, 1f))
        {
            error = "Melee impact particles require count [1, 256], ordered finite nonnegative speeds, spread [0, 360], whole-pixel size [1, 4], positive PPU, lifetime [0.001, 0.6], and normalized color.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool InRange(float value, float min, float max) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}

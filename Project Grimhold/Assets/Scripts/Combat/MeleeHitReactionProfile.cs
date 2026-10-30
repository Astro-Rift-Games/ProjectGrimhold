using UnityEngine;

/// <summary>Shared, immutable tuning for attacker-local confirmed melee presentation.</summary>
[CreateAssetMenu(fileName = "MeleeHitReaction", menuName = "Grimhold/Combat/Melee Hit Reaction")]
public sealed class MeleeHitReactionProfile : ScriptableObject
{
    [SerializeField, Tooltip("Peak sprite tint; blended from each renderer's captured color.")]
    private Color _flashColor = new Color(1f, .85f, .8f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("Peak flash blend strength.")]
    private float _flashStrength = .9f;
    [SerializeField, Range(0f, .25f), Tooltip("Compression along the dominant hit axis. Scale factors snap to 1/16 steps.")]
    private float _compression = .1f;
    [SerializeField, Range(0f, 1f), Tooltip("Perpendicular expansion as a fraction of compression.")]
    private float _stretchRatio = .5f;
    [SerializeField, Range(0, 4), Tooltip("Peak visual-only displacement in whole pixels along the hit direction.")]
    private int _nudgePixels = 1;
    [SerializeField, Min(.001f), Tooltip("Pixel grid used for displacement; character sprites currently use 16 PPU.")]
    private float _pixelsPerUnit = 16f;
    [SerializeField, Min(.001f), Tooltip("Fast ease-out recovery after the existing hitstop hold, in seconds.")]
    private float _recoverySeconds = .12f;

    public Color FlashColor => _flashColor;
    public float FlashStrength => _flashStrength;
    public float Compression => _compression;
    public float StretchRatio => _stretchRatio;
    public int NudgePixels => _nudgePixels;
    public float PixelsPerUnit => _pixelsPerUnit;
    public float RecoverySeconds => _recoverySeconds;

    public bool TryValidate(out string error)
    {
        if (!InRange(_flashColor.r, 0f, 1f) || !InRange(_flashColor.g, 0f, 1f) ||
            !InRange(_flashColor.b, 0f, 1f) || !InRange(_flashColor.a, 0f, 1f) ||
            !InRange(_flashStrength, 0f, 1f) || !InRange(_compression, 0f, .25f) ||
            !InRange(_stretchRatio, 0f, 1f) || _nudgePixels < 0 || _nudgePixels > 4 ||
            !IsFinite(_pixelsPerUnit) || _pixelsPerUnit <= 0f ||
            !IsFinite(_recoverySeconds) || _recoverySeconds <= 0f)
        {
            error = "Melee reaction requires finite normalized flash, compression [0, 0.25], stretch [0, 1], nudge [0, 4], and positive PPU/recovery.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool InRange(float value, float min, float max) => IsFinite(value) && value >= min && value <= max;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

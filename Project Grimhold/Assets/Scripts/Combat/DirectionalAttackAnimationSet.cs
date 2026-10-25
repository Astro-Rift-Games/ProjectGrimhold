using UnityEngine;

/// <summary>Shared static attack clips in N, NE, NW, S, SE, SW visual direction order.</summary>
[CreateAssetMenu(fileName = "AttackAnimationSet", menuName = "Grimhold/Combat/Directional Attack Animation Set")]
public sealed class DirectionalAttackAnimationSet : ScriptableObject
{
    // Presentation-only phase metadata, generated from the authored south clip's turning keys.
    [SerializeField] private bool _canonicalSlash;
    [SerializeField] private Vector2 _slashStrokeWindow;
    public bool CanonicalSlash => _canonicalSlash;
    public Vector2 SlashStrokeWindow => _slashStrokeWindow;
    [SerializeField] private float _slashRecoveryEndSeconds;
    public float SlashRecoveryEndSeconds => _slashRecoveryEndSeconds;
    public const float SlashStartDegrees = 135f;
    public const float SlashEndDegrees = -45f;

    [SerializeField] private OffHandStroke _offHandStroke;
    public OffHandStroke OffHandStroke => _offHandStroke;

    [SerializeField] private AnimationClip _attackN;
    [SerializeField] private AnimationClip _attackNE;
    [SerializeField] private AnimationClip _attackNW;
    [SerializeField] private AnimationClip _attackS;
    [SerializeField] private AnimationClip _attackSE;
    [SerializeField] private AnimationClip _attackSW;
    // Generated only for a retargeted set. Main Hand and legacy sets have no offset metadata.
    [SerializeField] private Vector2[] _gripOriginOffsets;
    [SerializeField, HideInInspector] private string _retargetSourceSignature;

    [SerializeField, HideInInspector] private Vector3[] _mainGripNeutralPositions;
    [SerializeField, HideInInspector] private Quaternion[] _mirroredWeaponBasisRotations;

    public string RetargetSourceSignature => _retargetSourceSignature;
    public bool HasMirrorMetadata => _mainGripNeutralPositions != null && _mainGripNeutralPositions.Length == 6 &&
        _mirroredWeaponBasisRotations != null && _mirroredWeaponBasisRotations.Length == 6;
    public Vector3 GetMainGripNeutralPosition(int direction) => _mainGripNeutralPositions[direction];
    public Quaternion GetMirroredWeaponBasisRotation(int direction) => _mirroredWeaponBasisRotations[direction];

    public bool HasGripOriginOffsets => _gripOriginOffsets != null && _gripOriginOffsets.Length == 6;

    public Vector2 GetGripOriginOffset(int direction) =>
        HasGripOriginOffsets && direction >= 0 && direction < 6 ? _gripOriginOffsets[direction] : Vector2.zero;

    public bool IsComplete => _attackN != null && _attackNE != null &&
        _attackNW != null && _attackS != null && _attackSE != null && _attackSW != null;

    public bool TryValidate(out string error)
    {
        if (_offHandStroke != OffHandStroke.Reanchored && _offHandStroke != OffHandStroke.Mirrored)
        {
            error = "Unknown Off Hand stroke mode.";
            return false;
        }
        if (!IsComplete)
        {
            error = $"Directional attack animation set '{name}' requires all six clips.";
            return false;
        }

        error = null;
        if (_gripOriginOffsets != null && _gripOriginOffsets.Length > 0)
        {
            if (!HasGripOriginOffsets)
            {
                error = "Retargeted grip offsets require all six directions.";
                return false;
            }
            foreach (Vector2 offset in _gripOriginOffsets)
                if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y))
                {
                    error = "Retargeted grip offsets must be finite.";
                    return false;
                }
        }
        return true;
    }

    public AnimationClip GetAttackClip(int index) => index switch
    {
        0 => _attackN,
        1 => _attackNE,
        2 => _attackNW,
        3 => _attackS,
        4 => _attackSE,
        5 => _attackSW,
        _ => null
    };
}

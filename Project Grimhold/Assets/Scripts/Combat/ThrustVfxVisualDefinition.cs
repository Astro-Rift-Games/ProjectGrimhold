using UnityEngine;

/// <summary>
/// Straight thrust art. The sprite sequence travels along its local +X axis from
/// <see cref="BackX"/> to <see cref="FrontX"/>, so the pose anchor is where the thrust path starts,
/// the pose rotation is the blade axis and the size is the path length up to the extended blade tip.
/// </summary>
[CreateAssetMenu(fileName = "ThrustVfxVisual", menuName = "Grimhold/Combat/Thrust VFX Visual")]
public sealed class ThrustVfxVisualDefinition : AttackVfxVisualDefinition
{
    [SerializeField, Tooltip("Sprite local X where the thrust path starts: the rearmost art across the sprite sequence.")]
    private float _backX;
    [SerializeField, Tooltip("Sprite local X where the thrust path ends: the frontmost art across the sprite sequence.")]
    private float _frontX;

    public float BackX => _backX;
    public float FrontX => _frontX;
    public float Length => _frontX - _backX;

    /// <summary>
    /// Scales the sprite sequence so its path spans <paramref name="size"/> and moves it along the
    /// pose's +X axis so the path starts at the pose anchor.
    /// </summary>
    public override bool TryResolvePose(AttackVfxDefinition.DirectionalPose pose, float size,
        out AttackVfxDefinition.ResolvedPose resolved)
    {
        resolved = default;
        float scale = size / Length;
        if (!HasValidPath() || !IsFinite(scale) || scale <= 0f)
        {
            return false;
        }
        Quaternion rotation = pose.Rotation;
        Vector3 position = pose.Position - rotation * Vector3.right * (_backX * scale);
        resolved = new AttackVfxDefinition.ResolvedPose(position, rotation,
            new Vector3(scale, pose.Mirrored ? -scale : scale, 1f), pose.SortingOrder);
        return true;
    }

    protected override bool TryValidateGeometry(out string error)
    {
        if (!HasValidPath())
        {
            error = "Thrust VFX visual requires finite path ends with the front ahead of the back.";
            return false;
        }
        error = null;
        return true;
    }

    private bool HasValidPath() => IsFinite(_backX) && IsFinite(_frontX) && Length > 0f;
}

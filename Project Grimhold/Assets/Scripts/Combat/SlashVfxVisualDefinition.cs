using UnityEngine;

/// <summary>
/// Arc-shaped slash art. Its sprite pivot is the swing arc center and the blade tip traces a circle
/// of <see cref="TipRadius"/> around it, so the pose anchor is the arc center and the size is the
/// blade tip's swing radius.
/// </summary>
[CreateAssetMenu(fileName = "SlashVfxVisual", menuName = "Grimhold/Combat/Slash VFX Visual")]
public sealed class SlashVfxVisualDefinition : AttackVfxVisualDefinition
{
    [SerializeField, Min(0f), Tooltip("Radius, in sprite local units, that the blade tip traces through the sprite sequence.")]
    private float _tipRadius;

    public float TipRadius => _tipRadius;

    /// <summary>Scales the sprite sequence so its tip radius matches the blade tip's swing radius.</summary>
    public override bool TryResolvePose(AttackVfxDefinition.DirectionalPose pose, float size,
        out AttackVfxDefinition.ResolvedPose resolved)
    {
        resolved = default;
        float scale = size / _tipRadius;
        if (!IsFinite(_tipRadius) || _tipRadius <= 0f || !IsFinite(scale) || scale <= 0f)
        {
            return false;
        }
        resolved = new AttackVfxDefinition.ResolvedPose(pose.Position, pose.Rotation,
            new Vector3(scale, pose.Mirrored ? -scale : scale, 1f), pose.SortingOrder);
        return true;
    }

    protected override bool TryValidateGeometry(out string error)
    {
        if (!IsFinite(_tipRadius) || _tipRadius <= 0f)
        {
            error = "Slash VFX visual requires a positive tip radius.";
            return false;
        }
        error = null;
        return true;
    }
}

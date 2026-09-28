using UnityEngine;

/// <summary>
/// Point flash art emitted where the weapon casts. It neither follows a path nor scales with the weapon:
/// the pose anchor is where the cast reach starts, the pose rotation is the axis it extends along and the
/// size is the distance along that axis to the cast point, so <see cref="Center"/> lands on the weapon tip
/// at native pixel scale. The flash is point-symmetric art, so it stays upright and unmirrored.
/// </summary>
[CreateAssetMenu(fileName = "CastFlashVfxVisual", menuName = "Grimhold/Combat/Cast Flash VFX Visual")]
public sealed class CastFlashVfxVisualDefinition : AttackVfxVisualDefinition
{
    [SerializeField, Tooltip("Flash center in sprite local units: the point of the art that sits on the cast point in every frame.")]
    private Vector2 _center;

    public Vector2 Center => _center;

    /// <summary>
    /// Places the flash center on the cast point, <paramref name="size"/> along the pose's +X axis from its
    /// anchor, without scaling, rotating or mirroring the art.
    /// </summary>
    public override bool TryResolvePose(AttackVfxDefinition.DirectionalPose pose, float size,
        out AttackVfxDefinition.ResolvedPose resolved)
    {
        resolved = default;
        if (!HasValidCenter() || !IsFinite(size) || size <= 0f)
        {
            return false;
        }
        Vector3 castPoint = pose.Position + pose.Rotation * Vector3.right * size;
        resolved = new AttackVfxDefinition.ResolvedPose(castPoint - (Vector3)_center, Quaternion.identity,
            Vector3.one, pose.SortingOrder);
        return true;
    }

    protected override bool TryValidateGeometry(out string error)
    {
        if (!HasValidCenter())
        {
            error = "Cast Flash VFX visual requires a finite flash center.";
            return false;
        }
        error = null;
        return true;
    }

    private bool HasValidCenter() => IsFinite(_center.x) && IsFinite(_center.y);
}

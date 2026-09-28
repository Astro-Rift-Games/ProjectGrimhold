using UnityEngine;

/// <summary>
/// Shot impulse art released from a bow. The sprite sequence starts at <see cref="Origin"/> and travels along
/// its local +X axis at native pixel scale; it is a release impulse, not a trajectory, so it never scales with
/// the weapon or its range. The pose anchor is the bow's shooting axis at release, the pose rotation is the
/// shooting direction and the size is the distance along that axis to the shot origin. It does not use the
/// weapon reach, so a bow needs no blade tip.
/// </summary>
[CreateAssetMenu(fileName = "BowShotVfxVisual", menuName = "Grimhold/Combat/Bow Shot VFX Visual")]
public sealed class BowShotVfxVisualDefinition : AttackVfxVisualDefinition
{
    [SerializeField, Tooltip("Shot origin in sprite local units: the point of the art that sits on the release point in every frame.")]
    private Vector2 _origin;

    public Vector2 Origin => _origin;
    public override bool UsesWeaponReach => false;

    /// <summary>
    /// Places the shot origin <paramref name="size"/> along the pose's +X axis from its anchor, pointing the art
    /// along that axis and mirroring it across the axis like the bow in left facings, without scaling it.
    /// </summary>
    public override bool TryResolvePose(AttackVfxDefinition.DirectionalPose pose, float size,
        out AttackVfxDefinition.ResolvedPose resolved)
    {
        resolved = default;
        if (!HasValidOrigin() || !IsFinite(size) || size < 0f)
        {
            return false;
        }
        Quaternion rotation = pose.Rotation;
        Vector3 scale = new Vector3(1f, pose.Mirrored ? -1f : 1f, 1f);
        Vector3 shotOrigin = pose.Position + rotation * Vector3.right * size;
        Vector3 position = shotOrigin - rotation * Vector3.Scale(_origin, scale);
        resolved = new AttackVfxDefinition.ResolvedPose(position, rotation, scale, pose.SortingOrder);
        return true;
    }

    protected override bool TryValidateGeometry(out string error)
    {
        if (!HasValidOrigin())
        {
            error = "Bow Shot VFX visual requires a finite shot origin.";
            return false;
        }
        error = null;
        return true;
    }

    private bool HasValidOrigin() => IsFinite(_origin.x) && IsFinite(_origin.y);
}

using System;
using UnityEngine;

/// <summary>
/// Aligns a shared <see cref="AttackVfxVisualDefinition"/> with one weapon swing: when it starts in the
/// attack clip and where it sits for each facing. Local presentation for a confirmed weapon attack.
/// </summary>
[CreateAssetMenu(fileName = "AttackVfxDefinition", menuName = "Grimhold/Combat/Attack VFX Definition")]
public sealed class AttackVfxDefinition : ScriptableObject
{
    /// <summary>
    /// Swing-relative placement for one facing. The visual decides what the anchor means for its
    /// geometry and its size is derived from the weapon's blade reach, so it holds no weapon-specific size.
    /// </summary>
    [Serializable]
    public struct DirectionalPose
    {
        [SerializeField, Tooltip("Geometric anchor of the visual in the Animator root space: the swing arc center for a slash, the thrust path start for a thrust, the casting grip for a cast flash.")]
        private Vector3 _position;
        [SerializeField] private Vector3 _rotation;
        [SerializeField, Tooltip("Added to the weapon blade reach to get the visual's size along the pose axis: the blade tip's swing radius for a slash, the path length to the extended blade tip for a thrust, the distance to the casting tip for a cast flash. A visual that does not use the weapon reach takes it alone: the distance to the shot origin for a bow shot.")]
        private float _reachOffset;
        [SerializeField, Tooltip("Mirrors the sprite sequence across its local X axis to match the swing's rotational sense.")]
        private bool _mirrored;
        [SerializeField] private int _sortingOrder;

        public DirectionalPose(Vector3 position, Vector3 rotation, float reachOffset, bool mirrored, int sortingOrder)
        {
            _position = position;
            _rotation = rotation;
            _reachOffset = reachOffset;
            _mirrored = mirrored;
            _sortingOrder = sortingOrder;
        }

        public Vector3 Position => _position;
        public Quaternion Rotation => Quaternion.Euler(_rotation);
        public float ReachOffset => _reachOffset;
        public bool Mirrored => _mirrored;
        public int SortingOrder => _sortingOrder;

        public bool IsValid => IsFinite(_position) && IsFinite(_rotation) && IsFinite(_reachOffset);
    }

    /// <summary>Transform values for one confirmed attack, resolved from a pose and a blade reach.</summary>
    public readonly struct ResolvedPose
    {
        public ResolvedPose(Vector3 position, Quaternion rotation, Vector3 scale, int sortingOrder)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            SortingOrder = sortingOrder;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public int SortingOrder { get; }
    }

    [SerializeField] private AttackVfxVisualDefinition _visual;
    [SerializeField, Min(0f), Tooltip("Attack clip time, in seconds, at which the visual starts playing.")]
    private float _startSeconds;
    [SerializeField, Min(0f), Tooltip("Ranged only: visual ignition lead before the confirmed gameplay release. Melee still uses StartSeconds.")]
    private float _releaseLeadSeconds;
    [SerializeField] private DirectionalPose[] _poses;

    public AttackVfxVisualDefinition Visual => _visual;
    public AnimationClip Clip => _visual != null ? _visual.Clip : null;
    public bool UsesWeaponReach => _visual != null && _visual.UsesWeaponReach;
    public float StartSeconds => _startSeconds;
    public float ReleaseLeadSeconds => _releaseLeadSeconds;
    public DirectionalPose GetPose(int direction) => _poses[direction];

    /// <summary>
    /// Lets the visual place and size its art for the facing's anchor, adding the blade reach only when the
    /// visual uses the weapon reach.
    /// </summary>
    public bool TryResolvePose(int direction, float bladeReach, out ResolvedPose pose)
    {
        pose = default;
        if (_visual == null || _poses == null || direction < 0 || direction >= _poses.Length ||
            !IsFinite(bladeReach))
        {
            return false;
        }
        return TryResolvePose(_poses[direction], bladeReach, out pose);
    }

    /// <summary>
    /// Resolves a pose that was not authored per facing, such as a free-aim pose built from the live weapon
    /// pivot. The visual and the reach rule are the same as for an authored facing.
    /// </summary>
    public bool TryResolvePose(DirectionalPose source, float bladeReach, out ResolvedPose pose)
    {
        pose = default;
        if (_visual == null || !IsFinite(bladeReach))
        {
            return false;
        }
        float size = _visual.UsesWeaponReach ? source.ReachOffset + bladeReach : source.ReachOffset;
        return source.IsValid && _visual.TryResolvePose(source, size, out pose);
    }

    public bool TryValidate(out string error)
    {
        if (_visual == null)
        {
            error = "Attack VFX requires a visual definition.";
            return false;
        }
        if (!_visual.TryValidate(out error)) return false;
        if (!IsFinite(_startSeconds) || _startSeconds < 0f ||
            !IsFinite(_releaseLeadSeconds) || _releaseLeadSeconds < 0f || _poses == null || _poses.Length != 6)
        {
            error = "Attack VFX requires a nonnegative finite start and six directional poses.";
            return false;
        }
        for (int i = 0; i < _poses.Length; i++)
        {
            if (!_poses[i].IsValid)
            {
                error = $"Attack VFX pose {i} must have finite values.";
                return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>Validates that every facing resolves to a positive size for the given blade reach.</summary>
    public bool TryValidateBladeReach(float bladeReach, out string error)
    {
        if (!TryValidate(out error)) return false;
        for (int i = 0; i < _poses.Length; i++)
        {
            if (!TryResolvePose(i, bladeReach, out _))
            {
                error = $"Attack VFX pose {i} resolves to a non-positive size for blade reach {bladeReach}.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}

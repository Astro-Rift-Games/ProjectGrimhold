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
    /// Swing-relative placement for one facing. The effect is centered on the swing arc and its
    /// radius is derived from the weapon's blade reach, so it holds no weapon-specific size.
    /// </summary>
    [Serializable]
    public struct DirectionalPose
    {
        [SerializeField, Tooltip("Swing arc center in the Animator root space.")]
        private Vector3 _position;
        [SerializeField] private Vector3 _rotation;
        [SerializeField, Tooltip("Added to the weapon blade reach to get the blade tip's radius around the swing arc center.")]
        private float _reachOffset;
        [SerializeField, Tooltip("Mirrors the sprite sequence across its local X axis to match the swing's rotational sense.")]
        private bool _mirrored;
        [SerializeField] private int _sortingOrder;

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
    [SerializeField] private DirectionalPose[] _poses;

    public AttackVfxVisualDefinition Visual => _visual;
    public AnimationClip Clip => _visual != null ? _visual.Clip : null;
    public float StartSeconds => _startSeconds;
    public float TipRadius => _visual != null ? _visual.TipRadius : 0f;
    public DirectionalPose GetPose(int direction) => _poses[direction];

    /// <summary>Scales the sprite sequence so its tip radius matches the blade tip's swing radius.</summary>
    public bool TryResolvePose(int direction, float bladeReach, out ResolvedPose pose)
    {
        pose = default;
        float tipRadius = TipRadius;
        if (_poses == null || direction < 0 || direction >= _poses.Length ||
            !IsFinite(tipRadius) || tipRadius <= 0f || !IsFinite(bladeReach))
        {
            return false;
        }
        DirectionalPose source = _poses[direction];
        float scale = (source.ReachOffset + bladeReach) / tipRadius;
        if (!source.IsValid || !IsFinite(scale) || scale <= 0f)
        {
            return false;
        }
        pose = new ResolvedPose(source.Position, source.Rotation,
            new Vector3(scale, source.Mirrored ? -scale : scale, 1f), source.SortingOrder);
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (_visual == null)
        {
            error = "Attack VFX requires a visual definition.";
            return false;
        }
        if (!_visual.TryValidate(out error)) return false;
        if (!IsFinite(_startSeconds) || _startSeconds < 0f || _poses == null || _poses.Length != 6)
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

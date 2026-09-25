using System;
using UnityEngine;

/// <summary>Shared, local sprite-clip presentation for a confirmed weapon attack.</summary>
[CreateAssetMenu(fileName = "AttackVfxDefinition", menuName = "Grimhold/Combat/Attack VFX Definition")]
public sealed class AttackVfxDefinition : ScriptableObject
{
    [Serializable]
    public struct DirectionalPose
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private Vector3 _rotation;
        [SerializeField] private Vector3 _scale;
        [SerializeField] private int _sortingOrder;

        public Vector3 Position => _position;
        public Quaternion Rotation => Quaternion.Euler(_rotation);
        public Vector3 Scale => _scale;
        public int SortingOrder => _sortingOrder;

        public bool IsValid => IsFinite(_position) && IsFinite(_rotation) && IsFinite(_scale) &&
            _scale.x != 0f && _scale.y != 0f && _scale.z != 0f;
    }

    [SerializeField] private AnimationClip _clip;
    [SerializeField, Min(0f)] private float _startSeconds;
    [SerializeField] private DirectionalPose[] _poses;

    public AnimationClip Clip => _clip;
    public float StartSeconds => _startSeconds;
    public DirectionalPose GetPose(int direction) => _poses[direction];

    public bool TryValidate(out string error)
    {
        if (_clip == null || _clip.length <= 0f || _clip.isLooping ||
            !IsFinite(_startSeconds) || _startSeconds < 0f ||
            _poses == null || _poses.Length != 6)
        {
            error = "Attack VFX requires a non-looping clip, nonnegative finite start and six directional poses.";
            return false;
        }
        for (int i = 0; i < _poses.Length; i++)
        {
            if (!_poses[i].IsValid)
            {
                error = $"Attack VFX pose {i} must have finite values and nonzero scale.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}

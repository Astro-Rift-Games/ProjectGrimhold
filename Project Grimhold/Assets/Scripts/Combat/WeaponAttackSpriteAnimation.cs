using System;
using UnityEngine;

/// <summary>
/// Optional sprite sequence the weapon's own held visual shows during its confirmed attack, such as a bow
/// drawing and releasing its string. It only swaps the sprite: the weapon pose, grip point, angle correction,
/// facing and mirror stay owned by the attack clip and the weapon presenter. Local presentation only.
/// </summary>
/// <remarks>
/// Every frame is authored in the frame of reference of the weapon's world sprite: same orientation and pixels
/// per unit, with a pivot that keeps the weapon's grip point on the same pixel of the art.
/// </remarks>
[CreateAssetMenu(fileName = "WeaponAttackSpriteAnimation", menuName = "Grimhold/Combat/Weapon Attack Sprite Animation")]
public sealed class WeaponAttackSpriteAnimation : ScriptableObject
{
    [Serializable]
    public struct Frame
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField, Min(0f)] private float _durationSeconds;

        public Sprite Sprite => _sprite;
        public float DurationSeconds => _durationSeconds;
    }

    [SerializeField, Min(0f), Tooltip("Attack clip time, in seconds, at which the first frame replaces the weapon sprite.")]
    private float _startSeconds;
    [SerializeField, Tooltip("Frames in playback order. Outside them the weapon shows its world sprite again.")]
    private Frame[] _frames;

    public float StartSeconds => _startSeconds;
    public int FrameCount => _frames != null ? _frames.Length : 0;
    public Frame GetFrame(int index) => _frames[index];

    /// <summary>Attack clip time, in seconds, at which the last frame ends.</summary>
    public float EndSeconds
    {
        get
        {
            float end = _startSeconds;
            for (int i = 0; i < FrameCount; i++) end += _frames[i].DurationSeconds;
            return end;
        }
    }

    /// <summary>Returns the frame shown at the given attack clip time, or false outside the sequence.</summary>
    public bool TryGetSprite(float attackSeconds, out Sprite sprite)
    {
        sprite = null;
        if (!IsFinite(attackSeconds) || attackSeconds < _startSeconds) return false;
        float frameEnd = _startSeconds;
        for (int i = 0; i < FrameCount; i++)
        {
            frameEnd += _frames[i].DurationSeconds;
            if (attackSeconds < frameEnd)
            {
                sprite = _frames[i].Sprite;
                return sprite != null;
            }
        }
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (!IsFinite(_startSeconds) || _startSeconds < 0f)
        {
            error = "Weapon attack sprite animation requires a nonnegative finite start.";
            return false;
        }
        if (FrameCount == 0)
        {
            error = "Weapon attack sprite animation requires at least one frame.";
            return false;
        }
        for (int i = 0; i < _frames.Length; i++)
        {
            if (_frames[i].Sprite == null || !IsFinite(_frames[i].DurationSeconds) ||
                _frames[i].DurationSeconds <= 0f)
            {
                error = $"Weapon attack sprite animation frame {i} requires a sprite and a positive finite duration.";
                return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>Validates that the sequence ends inside every clip of the attack set it plays over.</summary>
    public bool TryValidateAttackSet(DirectionalAttackAnimationSet attackSet, out string error)
    {
        if (!TryValidate(out error)) return false;
        if (attackSet == null || !attackSet.IsComplete)
        {
            error = "Weapon attack sprite animation requires a complete attack animation set.";
            return false;
        }
        float end = EndSeconds;
        for (int i = 0; i < 6; i++)
        {
            if (attackSet.GetAttackClip(i).length < end)
            {
                error = $"Weapon attack sprite animation ends at {end}s, after attack clip {i} ends.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

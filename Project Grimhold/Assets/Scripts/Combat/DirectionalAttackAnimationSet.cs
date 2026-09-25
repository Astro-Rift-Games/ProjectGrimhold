using UnityEngine;

/// <summary>Shared static attack clips in N, NE, NW, S, SE, SW visual direction order.</summary>
[CreateAssetMenu(fileName = "AttackAnimationSet", menuName = "Grimhold/Combat/Directional Attack Animation Set")]
public sealed class DirectionalAttackAnimationSet : ScriptableObject
{
    [SerializeField] private AnimationClip _attackN;
    [SerializeField] private AnimationClip _attackNE;
    [SerializeField] private AnimationClip _attackNW;
    [SerializeField] private AnimationClip _attackS;
    [SerializeField] private AnimationClip _attackSE;
    [SerializeField] private AnimationClip _attackSW;

    public bool IsComplete => _attackN != null && _attackNE != null &&
        _attackNW != null && _attackS != null && _attackSE != null && _attackSW != null;

    public bool TryValidate(out string error)
    {
        if (!IsComplete)
        {
            error = $"Directional attack animation set '{name}' requires all six clips.";
            return false;
        }

        error = null;
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

using UnityEngine;

/// <summary>
/// Shared sprite-clip art for an attack VFX. It holds only properties of the art itself, so any
/// number of weapon swings can align the same effect through their own <see cref="AttackVfxDefinition"/>.
/// Each archetype owns how its art is placed and sized around a swing-relative anchor.
/// </summary>
public abstract class AttackVfxVisualDefinition : ScriptableObject
{
    [SerializeField] private AnimationClip _clip;

    public AnimationClip Clip => _clip;

    public bool TryValidate(out string error)
    {
        if (_clip == null || _clip.length <= 0f || _clip.isLooping)
        {
            error = "Attack VFX visual requires a non-looping clip.";
            return false;
        }
        return TryValidateGeometry(out error);
    }

    /// <summary>
    /// Places the art for one facing. <paramref name="size"/> is the length, in Animator root units,
    /// that the art's geometry must span, already derived from the weapon's blade reach.
    /// </summary>
    public abstract bool TryResolvePose(AttackVfxDefinition.DirectionalPose pose, float size,
        out AttackVfxDefinition.ResolvedPose resolved);

    protected abstract bool TryValidateGeometry(out string error);

    protected static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

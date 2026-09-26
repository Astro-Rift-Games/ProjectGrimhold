using UnityEngine;

/// <summary>
/// Shared sprite-clip art for an attack VFX. It holds only properties of the art itself, so any
/// number of weapon swings can align the same effect through their own <see cref="AttackVfxDefinition"/>.
/// </summary>
[CreateAssetMenu(fileName = "AttackVfxVisualDefinition", menuName = "Grimhold/Combat/Attack VFX Visual Definition")]
public sealed class AttackVfxVisualDefinition : ScriptableObject
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, Min(0f), Tooltip("Radius, in sprite local units, that the blade tip traces through the sprite sequence.")]
    private float _tipRadius;

    public AnimationClip Clip => _clip;
    public float TipRadius => _tipRadius;

    public bool TryValidate(out string error)
    {
        if (_clip == null || _clip.length <= 0f || _clip.isLooping ||
            float.IsNaN(_tipRadius) || float.IsInfinity(_tipRadius) || _tipRadius <= 0f)
        {
            error = "Attack VFX visual requires a non-looping clip and a positive tip radius.";
            return false;
        }
        error = null;
        return true;
    }
}

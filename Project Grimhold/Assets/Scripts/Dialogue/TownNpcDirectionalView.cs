using UnityEngine;

/// <summary>
/// Local-only facing presentation for a Town NPC's modular Idle Animator.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownNpcDirectionalView : MonoBehaviour
{
    [SerializeField] private CharacterVisualDirection _initialFacing = CharacterVisualDirection.South;
    [SerializeField] private Animator _animator;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    public CharacterVisualDirection InitialFacing => _initialFacing;
    public CharacterVisualDirection CurrentFacing { get; private set; }

    private void Awake()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[{nameof(TownNpcDirectionalView)}] Missing required Idle Animator or controller.", this);
            return;
        }

        RestoreInitialFacing();
    }

    private void OnDisable()
    {
        RestoreInitialFacing();
    }

    public void FaceTarget(Vector3 targetPosition)
    {
        Vector2 direction = targetPosition - transform.position;
        CharacterVisualDirection facing = CharacterVisualDirectionResolver.Resolve(
            direction,
            CharacterVisualDirectionResolver.GetCanonicalVector(_initialFacing));
        SetFacing(facing);
    }

    public void RestoreInitialFacing()
    {
        SetFacing(_initialFacing);
    }

    private void SetFacing(CharacterVisualDirection facing)
    {
        CurrentFacing = facing;
        if (_animator == null)
        {
            return;
        }

        Vector2 direction = CharacterVisualDirectionResolver.GetCanonicalVector(facing);
        _animator.SetFloat(MoveXHash, direction.x);
        _animator.SetFloat(MoveYHash, direction.y);
    }
}

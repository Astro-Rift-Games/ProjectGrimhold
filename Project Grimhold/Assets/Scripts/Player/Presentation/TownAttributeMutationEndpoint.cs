using System;

/// <summary>Rejects locked Town assignments before persistence or profile notifications.</summary>
public sealed class TownAttributeMutationEndpoint
{
    private readonly LocalProfileStore _store;
    private readonly Func<bool> _canMutate;

    public TownAttributeMutationEndpoint(LocalProfileStore store, Func<bool> canMutate)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _canMutate = canMutate ?? throw new ArgumentNullException(nameof(canMutate));
    }

    /// <summary>Returns false for a Ready lock or a rejected store assignment.</summary>
    public bool TryAssign(CharacterAttribute attribute, out CharacterAttributeAssignmentFailure failure)
    {
        failure = default;
        if (!_canMutate())
        {
            return false;
        }

        return _store.TryAssignCharacterAttribute(attribute, out failure) ==
            CharacterAttributeAssignmentCommitResult.Success;
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// Process-local testing state used to configure one character override across Town and Raid.
/// It has no repository, backend, progression or serialization dependency.
/// </summary>
public sealed class RuntimeAttributeOverrideSession
{
    public RuntimeAttributeOverrideState State { get; private set; }

    public bool TryGetEffectiveState(
        in CharacterAttributeState persistent,
        out CharacterAttributeState effective) =>
        State.TryApply(persistent, out effective);

    public bool TryAdjust(CharacterAttribute attribute, int amount, in CharacterAttributeState persistent)
    {
        if (!State.TryAdjust(attribute, amount, persistent, out RuntimeAttributeOverrideState candidate))
        {
            return false;
        }

        State = candidate;
        return true;
    }

    public void Reset(CharacterAttribute attribute) => State = State.Reset(attribute);
    public void ResetAll() => State = State.ResetAll();
}
#endif

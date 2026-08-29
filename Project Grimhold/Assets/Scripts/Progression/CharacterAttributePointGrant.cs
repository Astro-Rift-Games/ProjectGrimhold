/// <summary>
/// Immutable one-shot consequence of applying a level transition to character attribute points.
/// The default value represents a pending grant.
/// </summary>
public readonly struct CharacterAttributePointGrant
{
    public bool IsApplied { get; }
    public int GrantedPoints { get; }
    public CharacterAttributeState Result { get; }

    internal CharacterAttributePointGrant(
        int grantedPoints,
        in CharacterAttributeState result)
    {
        IsApplied = true;
        GrantedPoints = grantedPoints;
        Result = result;
    }
}

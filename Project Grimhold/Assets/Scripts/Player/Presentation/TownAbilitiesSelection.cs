using System.Collections.Generic;

/// <summary>Resolves which ability the Abilities tab keeps selected across refreshes and filter changes.</summary>
public static class TownAbilitiesSelection
{
    /// <summary>
    /// Keeps <paramref name="current"/> while it is still visible, otherwise selects the first visible
    /// entry. Returns an invalid id when nothing is visible.
    /// </summary>
    public static AbilityId Resolve(IReadOnlyList<TownAbilityEntry> visible, AbilityId current)
    {
        if (visible == null || visible.Count == 0)
        {
            return default;
        }

        if (current.IsValid)
        {
            for (int index = 0; index < visible.Count; index++)
            {
                if (visible[index].Id == current)
                {
                    return current;
                }
            }
        }

        return visible[0].Id;
    }
}

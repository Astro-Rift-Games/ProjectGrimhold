using System.Collections.Generic;

/// <summary>
/// Immutable per-level experience requirements beginning at Level 1.
/// </summary>
public sealed class ExperienceCurve
{
    public const int InitialLevel = 1;

    private readonly long[] _requirements;

    /// <summary>
    /// Highest level represented by this curve.
    /// </summary>
    public int MaximumLevel { get; }

    private ExperienceCurve(long[] requirements)
    {
        _requirements = requirements;
        MaximumLevel = InitialLevel + requirements.Length;
    }

    /// <summary>
    /// Creates an immutable curve by copying one positive requirement per level transition.
    /// </summary>
    public static bool TryCreate(IReadOnlyList<long> requirements, out ExperienceCurve curve)
    {
        curve = null;
        if (requirements == null || requirements.Count == 0 || requirements.Count == int.MaxValue)
        {
            return false;
        }

        var copiedRequirements = new long[requirements.Count];
        for (int index = 0; index < requirements.Count; index++)
        {
            long requirement = requirements[index];
            if (requirement <= 0)
            {
                return false;
            }

            copiedRequirements[index] = requirement;
        }

        curve = new ExperienceCurve(copiedRequirements);
        return true;
    }

    /// <summary>
    /// Gets the experience required to advance from <paramref name="currentLevel"/>.
    /// </summary>
    public bool TryGetRequiredExperience(int currentLevel, out long requiredExperience)
    {
        requiredExperience = 0;
        if (currentLevel < InitialLevel || currentLevel >= MaximumLevel)
        {
            return false;
        }

        requiredExperience = _requirements[currentLevel - InitialLevel];
        return true;
    }
}

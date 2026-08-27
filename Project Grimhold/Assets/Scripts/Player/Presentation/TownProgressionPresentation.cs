/// <summary>
/// Immutable local presentation of one persistent character progression state in Town.
/// </summary>
public readonly struct TownProgressionPresentation
{
    public int Level { get; }
    public long CurrentExperience { get; }
    public long RequiredExperience { get; }
    public float NormalizedProgress { get; }
    public bool IsMaximumLevel { get; }

    private TownProgressionPresentation(
        int level,
        long currentExperience,
        long requiredExperience,
        float normalizedProgress,
        bool isMaximumLevel)
    {
        Level = level;
        CurrentExperience = currentExperience;
        RequiredExperience = requiredExperience;
        NormalizedProgress = normalizedProgress;
        IsMaximumLevel = isMaximumLevel;
    }

    public static bool TryCreate(
        ExperienceCurve curve,
        int level,
        long experience,
        out TownProgressionPresentation presentation)
    {
        presentation = default;
        if (!CharacterProgressionRules.IsValidState(curve, level, experience))
        {
            return false;
        }

        if (level == curve.MaximumLevel)
        {
            presentation = new TownProgressionPresentation(
                level,
                experience,
                0,
                1f,
                true);
            return true;
        }

        if (!curve.TryGetRequiredExperience(level, out long requiredExperience))
        {
            return false;
        }

        presentation = new TownProgressionPresentation(
            level,
            experience,
            requiredExperience,
            (float)((double)experience / requiredExperience),
            false);
        return true;
    }
}

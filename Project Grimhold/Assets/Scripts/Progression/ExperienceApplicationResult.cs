/// <summary>
/// Immutable result of applying one valid experience reward.
/// </summary>
public readonly struct ExperienceApplicationResult
{
    public int PreviousLevel { get; }
    public long PreviousExperience { get; }
    public int ResultingLevel { get; }
    public long ResultingExperience { get; }
    public int LevelsGained { get; }

    internal ExperienceApplicationResult(
        int previousLevel,
        long previousExperience,
        int resultingLevel,
        long resultingExperience,
        int levelsGained)
    {
        PreviousLevel = previousLevel;
        PreviousExperience = previousExperience;
        ResultingLevel = resultingLevel;
        ResultingExperience = resultingExperience;
        LevelsGained = levelsGained;
    }
}

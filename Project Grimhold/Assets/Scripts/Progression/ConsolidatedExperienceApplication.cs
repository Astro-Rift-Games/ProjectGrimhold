/// <summary>
/// Immutable one-shot application of consolidated experience to persistent progression.
/// The default value represents a pending application.
/// </summary>
public readonly struct ConsolidatedExperienceApplication
{
    public bool IsApplied { get; }
    public ExperienceApplicationResult Result { get; }

    internal ConsolidatedExperienceApplication(in ExperienceApplicationResult result)
    {
        IsApplied = true;
        Result = result;
    }
}

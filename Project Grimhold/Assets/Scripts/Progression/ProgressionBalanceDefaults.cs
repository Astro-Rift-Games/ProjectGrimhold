using System;

/// <summary>
/// Initial playtest balance data for persistent character progression.
/// </summary>
public static class ProgressionBalanceDefaults
{
    public static ExperienceCurve InitialExperienceCurve { get; } = CreateInitialExperienceCurve();

    private static ExperienceCurve CreateInitialExperienceCurve()
    {
        long[] requirements =
        {
            100, 105, 110, 115, 120, 126, 132, 138, 144, 151,
            158, 165, 173, 181, 190, 199, 208, 218, 228, 239,
            250, 262, 275, 288, 302, 317, 332, 348, 365
        };

        if (!ExperienceCurve.TryCreate(requirements, out ExperienceCurve curve))
        {
            throw new InvalidOperationException("Initial progression balance is invalid.");
        }

        return curve;
    }
}

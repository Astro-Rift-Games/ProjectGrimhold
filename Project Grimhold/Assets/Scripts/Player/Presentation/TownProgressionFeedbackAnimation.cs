using System;

/// <summary>Interpolates presentation across every level threshold without changing persistent progression.</summary>
public static class TownProgressionFeedbackAnimation
{
    public static bool TrySample(ExperienceCurve curve, ExperienceApplicationResult result,
        float progress, out TownProgressionPresentation presentation)
    {
        presentation = default;
        if (float.IsNaN(progress) || float.IsInfinity(progress) ||
            !TownProgressionPresentation.TryCreate(curve, result.PreviousLevel, result.PreviousExperience, out var start) ||
            !TownProgressionPresentation.TryCreate(curve, result.ResultingLevel, result.ResultingExperience, out var end) ||
            result.ResultingLevel < result.PreviousLevel)
            return false;

        if (progress >= 1f) { presentation = end; return true; }
        if (progress <= 0f) { presentation = start; return true; }

        // Maximum Level has a full visual bar but no additional threshold to animate through.
        double endPosition = end.IsMaximumLevel ? end.Level : end.Level + (double)end.CurrentExperience / end.RequiredExperience;
        double startPosition = start.IsMaximumLevel ? start.Level : start.Level + (double)start.CurrentExperience / start.RequiredExperience;
        double position = startPosition + (endPosition - startPosition) * progress;
        int level = Math.Min(curve.MaximumLevel, (int)Math.Floor(position));
        long experience = 0;
        if (curve.TryGetRequiredExperience(level, out long required))
            experience = Math.Min(required - 1, (long)((position - level) * required));
        return TownProgressionPresentation.TryCreate(curve, level, experience, out presentation);
    }
}

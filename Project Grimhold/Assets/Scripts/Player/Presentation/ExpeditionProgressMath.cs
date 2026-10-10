using UnityEngine;

/// <summary>
/// Pure presentation math for the individual expedition progress indicator.
/// </summary>
public static class ExpeditionProgressMath
{
    /// <summary>
    /// Converts confirmed individual progress into a fill fraction in the range 0 to 1.
    /// </summary>
    /// <returns>False when the quota is not positive, so the indicator must be cleared.</returns>
    public static bool TryGetFraction(int currentProgress, int quota, out float fraction)
    {
        fraction = 0f;
        if (quota <= 0)
        {
            return false;
        }

        fraction = Mathf.Clamp01((float)currentProgress / quota);
        return true;
    }

    /// <summary>
    /// Returns the whole percentage without rounding up, so 99.5% is never shown as complete.
    /// </summary>
    public static int ToWholePercent(float fraction)
    {
        if (float.IsNaN(fraction) || float.IsInfinity(fraction))
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(fraction) * 100f + 0.0001f), 0, 100);
    }
}

using UnityEngine;

/// <summary>Pure deterministic rules used by the networked Stamina owner.</summary>
internal static class StaminaResourceRules
{
    internal static bool IsValidCost(float amount)
    {
        return IsFinite(amount) && amount >= 0f;
    }

    internal static bool CanSpend(float current, bool isExhausted, float amount)
    {
        return IsValidCost(amount) &&
               (amount == 0f ||
                (!isExhausted && IsFinite(current) && current >= amount));
    }

    internal static bool TrySpend(
        float current,
        bool isExhausted,
        float amount,
        bool exhaustOnFailure,
        bool exhaustWhenDepleted,
        out float resultingCurrent,
        out bool resultingExhaustion)
    {
        resultingCurrent = IsFinite(current) ? Mathf.Max(0f, current) : 0f;
        resultingExhaustion = isExhausted;

        if (!IsValidCost(amount))
        {
            return false;
        }

        if (amount == 0f)
        {
            return true;
        }

        if (isExhausted)
        {
            return false;
        }

        if (resultingCurrent < amount)
        {
            resultingExhaustion = exhaustOnFailure;
            return false;
        }

        resultingCurrent = Mathf.Max(0f, resultingCurrent - amount);
        if (exhaustWhenDepleted && resultingCurrent <= 0f)
        {
            resultingExhaustion = true;
        }

        return true;
    }

    internal static float ClampCurrent(float current, float maximum)
    {
        if (!IsFinite(current) || !IsFinite(maximum))
        {
            return 0f;
        }

        return Mathf.Clamp(current, 0f, Mathf.Max(0f, maximum));
    }

    internal static float Regenerate(float current, float maximum, float ratePerSecond, float deltaTime)
    {
        float clampedCurrent = ClampCurrent(current, maximum);
        if (!IsFinite(ratePerSecond) || !IsFinite(deltaTime) ||
            ratePerSecond <= 0f || deltaTime <= 0f)
        {
            return clampedCurrent;
        }

        return Mathf.Min(Mathf.Max(0f, maximum), clampedCurrent + ratePerSecond * deltaTime);
    }

    internal static bool HasRecoveredFromExhaustion(
        float current,
        float maximum,
        float recoveryThreshold)
    {
        if (!IsFinite(current) || !IsFinite(maximum) || !IsFinite(recoveryThreshold) || maximum < 0f)
        {
            return false;
        }

        return current >= maximum * Mathf.Clamp01(recoveryThreshold);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

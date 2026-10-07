/// <summary>Deterministic, all-or-nothing Mana payments and explicit restoration.</summary>
public static class ManaResourceRules
{
    public static bool IsValidAmount(float amount) =>
        !float.IsNaN(amount) && !float.IsInfinity(amount) && amount >= 0f;

    public static float ClampCurrent(float current, float maximum)
    {
        if (!IsValidAmount(maximum) || !IsValidAmount(current)) return 0f;
        return System.Math.Min(current, maximum);
    }

    public static bool CanSpend(float current, float amount) =>
        IsValidAmount(current) && IsValidAmount(amount) && current >= amount;

    public static bool TrySpend(float current, float amount, out float remaining)
    {
        remaining = current;
        if (!CanSpend(current, amount)) return false;
        remaining = current - amount;
        return true;
    }

    public static bool TryRestore(float current, float maximum, float amount, out float restored)
    {
        restored = current;
        if (!IsValidAmount(current) || !IsValidAmount(maximum) || !IsValidAmount(amount)) return false;
        // Add in double precision so two finite float operands cannot overflow the balance.
        restored = (float)System.Math.Min((double)current + amount, maximum);
        return true;
    }
}

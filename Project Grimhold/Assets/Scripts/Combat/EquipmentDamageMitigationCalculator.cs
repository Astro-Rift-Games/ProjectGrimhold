using System;

/// <summary>Pure armor mitigation calculation for one compatible incoming damage type.</summary>
public static class EquipmentDamageMitigationCalculator
{
    public static bool TryCalculate(
        float incomingDamage,
        int defense,
        float mitigationConstant,
        out float mitigatedDamage)
    {
        mitigatedDamage = 0f;
        if (!IsFinite(incomingDamage) || incomingDamage < 0f || defense < 0 ||
            !IsFinite(mitigationConstant) || mitigationConstant <= 0f)
        {
            return false;
        }

        if (defense == 0)
        {
            mitigatedDamage = incomingDamage;
            return true;
        }

        double candidate = Math.Floor(
            incomingDamage * ((double)mitigationConstant / ((double)defense + mitigationConstant)));
        if (double.IsNaN(candidate) || double.IsInfinity(candidate) ||
            candidate < 0d || candidate > float.MaxValue)
        {
            return false;
        }

        mitigatedDamage = (float)candidate;
        return true;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

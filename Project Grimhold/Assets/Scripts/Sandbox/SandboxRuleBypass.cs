#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

/// <summary>
/// Pure decisions for the sandbox "ignore session rules" mode: the cast-time attribute gate, splitting an
/// attribute adjustment into the steps the override controller accepts, and display strings.
/// </summary>
public static class SandboxRuleBypass
{
    /// <summary>Largest total attribute adjustment accepted in one request.</summary>
    public const int MaxAdjustment = 50;

    private const int LargeStep = 5;

    /// <summary>With the bypass on, attribute requirements always pass; otherwise the real result stands.</summary>
    public static bool EvaluateRequirements(bool ignoreSessionRules, bool requirementsSatisfied) =>
        ignoreSessionRules || requirementsSatisfied;

    /// <summary>
    /// Splits <paramref name="amount"/> into +/-5 and +/-1 steps, the only amounts
    /// <c>RuntimeAttributeOverrideNetworkController</c> accepts. Fails for 0, out of range or a null list.
    /// </summary>
    public static bool TryDecomposeAdjustment(int amount, List<int> steps)
    {
        if (steps == null || amount == 0 || amount > MaxAdjustment || amount < -MaxAdjustment)
        {
            return false;
        }

        int sign = amount > 0 ? 1 : -1;
        int remaining = amount * sign;
        while (remaining >= LargeStep)
        {
            steps.Add(sign * LargeStep);
            remaining -= LargeStep;
        }

        while (remaining > 0)
        {
            steps.Add(sign);
            remaining--;
        }

        return true;
    }

    public static string DescribeRequirement(string attribute, int minimum, int? current) =>
        $"needs {attribute} {minimum} (current {(current.HasValue ? current.Value.ToString() : "?")})";

    /// <summary>Human-readable explanation of the last activation failure shown in the panel.</summary>
    public static string DescribeFailure(AbilityActivationFailure failure)
    {
        switch (failure)
        {
            case AbilityActivationFailure.None: return "none (last attempt started, or no attempt yet)";
            case AbilityActivationFailure.PlayerUnavailable: return "player unavailable (dead, downed, match not in progress or not simulating)";
            case AbilityActivationFailure.MissingBehaviour: return "no execution behaviour bound to this slot";
            case AbilityActivationFailure.RequirementsNotMet: return "attribute requirements not met (enable Ignore session rules)";
            case AbilityActivationFailure.AlreadyExecuting: return "already executing";
            case AbilityActivationFailure.Cooldown: return "on cooldown (use Reset cooldowns)";
            case AbilityActivationFailure.ResourceUnavailable: return "resource controller unavailable";
            case AbilityActivationFailure.InsufficientResource: return "not enough Stamina or Mana";
            case AbilityActivationFailure.BehaviourRejected: return "rejected by the ability rules (for example Seismic Strike needs an enemy in range)";
            case AbilityActivationFailure.InvalidPlan: return "ability produced an invalid start plan";
            case AbilityActivationFailure.AimUnavailable: return "no usable aim or facing direction";
            default: return $"unknown failure ({(int)failure})";
        }
    }

    public static string DescribeAbility(string displayName, bool hasBehaviour) =>
        hasBehaviour ? displayName : $"{displayName} (no behaviour - cannot cast)";
}
#endif

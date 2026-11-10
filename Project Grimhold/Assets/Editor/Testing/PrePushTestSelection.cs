using System;

/// <summary>
/// Decides which EditMode tests the Git pre-push quality gate runs.
/// Slow editor-tooling suites stay out of the gate and run in full manual or CI-style runs.
/// </summary>
public static class PrePushTestSelection
{
    // Namespaces or fixture class names; each matches a whole dotted segment of the test full name.
    private static readonly string[] ExcludedScopes =
    {
        "Tests.EditMode.AttackAnimationValidator",
        "SessionConnectionStateMachineTests",
        "DirectionalAnimationGeneratorTests",
    };

    public static bool IsExcluded(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return false;

        foreach (string scope in ExcludedScopes)
        {
            if (fullName.StartsWith(scope + ".", StringComparison.Ordinal) ||
                fullName.Contains("." + scope + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

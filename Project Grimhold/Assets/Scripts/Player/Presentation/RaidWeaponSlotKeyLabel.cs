using System;

/// <summary>
/// Key label shown on the weapon slot of the Raid action bar. It mirrors the real binding of
/// <c>Gameplay/PrimaryAttack</c> (<c>&lt;Mouse&gt;/leftButton</c>); an EditMode test compares it with the
/// Input Actions asset so it cannot drift.
/// </summary>
public static class RaidWeaponSlotKeyLabel
{
    public const string Label = "LMB";

    private const string MousePrefix = "<Mouse>/";
    private const string KeyboardPrefix = "<Keyboard>/";

    /// <summary>
    /// Short label for a binding path: mouse buttons become LMB/RMB/MMB, keyboard keys are upper-cased.
    /// Unsupported or empty paths return an empty string.
    /// </summary>
    public static string FromBindingPath(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath))
        {
            return string.Empty;
        }

        if (bindingPath.StartsWith(MousePrefix, StringComparison.Ordinal))
        {
            return bindingPath.Substring(MousePrefix.Length) switch
            {
                "leftButton" => "LMB",
                "rightButton" => "RMB",
                "middleButton" => "MMB",
                _ => string.Empty
            };
        }

        if (bindingPath.StartsWith(KeyboardPrefix, StringComparison.Ordinal))
        {
            return bindingPath.Substring(KeyboardPrefix.Length).ToUpperInvariant();
        }

        return string.Empty;
    }
}

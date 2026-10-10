/// <summary>
/// Text shown by the local interaction prompt. The key label mirrors the real keyboard binding of
/// <c>Gameplay/Interact</c>; an EditMode test compares it with the Input Actions asset so it cannot drift.
/// </summary>
public static class InteractionPromptText
{
    public const string KeyLabel = "F";
    public const string DefaultAction = "Interactuar";

    public static string Format(string action)
    {
        return $"[{KeyLabel}] {(string.IsNullOrWhiteSpace(action) ? DefaultAction : action)}";
    }
}

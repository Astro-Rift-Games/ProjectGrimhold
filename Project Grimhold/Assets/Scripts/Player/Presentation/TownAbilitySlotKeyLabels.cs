/// <summary>
/// Key labels shown for the two universal ability slots. They mirror the real keyboard bindings
/// (<c>Gameplay/AbilitySlot1</c> and <c>Gameplay/AbilitySlot2</c>); an EditMode test compares them with
/// the Input Actions asset so they cannot drift.
/// </summary>
public static class TownAbilitySlotKeyLabels
{
    public const string Slot1 = "Q";
    public const string Slot2 = "E";

    public static string For(UniversalAbilitySlot slot) => slot switch
    {
        UniversalAbilitySlot.Slot1 => Slot1,
        UniversalAbilitySlot.Slot2 => Slot2,
        _ => string.Empty
    };
}

/// <summary>
/// Local presentation descriptor for one contextual inventory action.
/// </summary>
public readonly struct LootContextActionDescriptor
{
    public LootContextActionId Id { get; }
    public string Label { get; }
    public bool IsEnabled { get; }
    public ILootContextActionProvider Provider { get; }

    public LootContextActionDescriptor(
        LootContextActionId id,
        string label,
        bool isEnabled,
        ILootContextActionProvider provider)
    {
        Id = id;
        Label = label;
        IsEnabled = isEnabled;
        Provider = provider;
    }
}

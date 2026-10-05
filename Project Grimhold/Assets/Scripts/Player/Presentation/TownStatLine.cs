/// <summary>One display row of the Town statistics column.</summary>
public readonly struct TownStatLine
{
    public TownStatLineKind Kind { get; }
    public string Label { get; }
    public string Value { get; }
    public string Detail { get; }

    public TownStatLine(TownStatLineKind kind, string label, string value = "", string detail = "")
    {
        Kind = kind;
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
        Detail = detail ?? string.Empty;
    }
}

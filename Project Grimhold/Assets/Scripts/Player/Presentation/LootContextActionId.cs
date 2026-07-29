using System;

/// <summary>
/// Stable local identifier for one contextual inventory action.
/// </summary>
public readonly struct LootContextActionId : IEquatable<LootContextActionId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public LootContextActionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A contextual action ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public bool Equals(LootContextActionId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is LootContextActionId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    public static bool operator ==(LootContextActionId left, LootContextActionId right) =>
        left.Equals(right);

    public static bool operator !=(LootContextActionId left, LootContextActionId right) =>
        !left.Equals(right);
}

using System;

/// <summary>
/// Small, stable, and comparable mission identifier.
/// </summary>
public readonly struct MissionId : IEquatable<MissionId>
{
    public string Value { get; }

    /// <summary>
    /// Indicates whether this identifier contains a usable domain value.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public MissionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("MissionId value cannot be null or empty.", nameof(value));
        }
        Value = value;
    }

    public bool Equals(MissionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is MissionId other && Equals(other);

    public override int GetHashCode() => Value != null ? Value.GetHashCode(StringComparison.Ordinal) : 0;

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(MissionId left, MissionId right) => left.Equals(right);
    public static bool operator !=(MissionId left, MissionId right) => !left.Equals(right);
}

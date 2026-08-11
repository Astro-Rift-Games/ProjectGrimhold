using System;

/// <summary>
/// Canonical value object for the six-digit code used to identify a coded Raid.
/// </summary>
public readonly struct RaidCode : IEquatable<RaidCode>
{
    public const int Length = 6;

    private readonly string _value;

    public bool IsValid => _value != null;
    public string Value => _value;
    public string SessionName => DerivedIdentity;
    public string RaidId => DerivedIdentity;

    private string DerivedIdentity => IsValid ? $"raid-{_value}" : null;

    private RaidCode(string value) => _value = value;

    /// <summary>Trims external whitespace and accepts only six ASCII decimal digits.</summary>
    public static bool TryParse(string value, out RaidCode code)
    {
        string normalized = value?.Trim();
        if (normalized == null || normalized.Length != Length)
        {
            code = default;
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            if (normalized[index] < '0' || normalized[index] > '9')
            {
                code = default;
                return false;
            }
        }

        code = new RaidCode(normalized);
        return true;
    }

    public bool Equals(RaidCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is RaidCode other && Equals(other);
    public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    public override string ToString() => _value ?? string.Empty;
    public static bool operator ==(RaidCode left, RaidCode right) => left.Equals(right);
    public static bool operator !=(RaidCode left, RaidCode right) => !left.Equals(right);
}

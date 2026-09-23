using System;

/// <summary>Stable, ordinal identity used to reference one ability across system boundaries.</summary>
public readonly struct AbilityId : IEquatable<AbilityId>
{
    public const int MaximumLength = 64;

    public string Value { get; }

    public bool IsValid => IsValidValue(Value);

    public AbilityId(string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(
                $"AbilityId must contain 1-{MaximumLength} lowercase ASCII letters, numbers, or underscores.",
                nameof(value));
        }

        Value = value;
    }

    public static bool TryCreate(string value, out AbilityId abilityId)
    {
        abilityId = default;
        if (!IsValidValue(value))
        {
            return false;
        }

        abilityId = new AbilityId(value);
        return true;
    }

    public static bool IsValidValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            bool isValid = (character >= 'a' && character <= 'z') ||
                           (character >= '0' && character <= '9') ||
                           character == '_';
            if (!isValid)
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(AbilityId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is AbilityId other && Equals(other);

    public override int GetHashCode() =>
        Value != null ? Value.GetHashCode(StringComparison.Ordinal) : 0;

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(AbilityId left, AbilityId right) => left.Equals(right);
    public static bool operator !=(AbilityId left, AbilityId right) => !left.Equals(right);
}

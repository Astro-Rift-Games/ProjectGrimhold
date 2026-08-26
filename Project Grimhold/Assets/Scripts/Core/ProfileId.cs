using System;

/// <summary>
/// Stable backend CharacterId used to identify one locally persisted player profile.
/// </summary>
public readonly struct ProfileId : IEquatable<ProfileId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public ProfileId(string value)
    {
        Value = value;
    }

    public bool Equals(ProfileId other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is ProfileId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value != null ? Value.GetHashCode() : 0;
    }

    public static bool operator ==(ProfileId left, ProfileId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ProfileId left, ProfileId right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

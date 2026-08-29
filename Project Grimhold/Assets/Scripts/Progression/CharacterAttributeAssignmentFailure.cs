/// <summary>Deterministic reason why an attribute point assignment was rejected.</summary>
public enum CharacterAttributeAssignmentFailure : byte
{
    None = 0,
    InvalidMaximumAttributeValue = 1,
    UnknownAttribute = 2,
    NoAvailablePoints = 3,
    AttributeAtMaximum = 4
}

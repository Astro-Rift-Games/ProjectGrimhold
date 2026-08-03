/// <summary>
/// Authoritative lifecycle of one reserved Sanctuary ritual.
/// Completed and Cancelled are terminal for the current expedition.
/// </summary>
public enum ExtractionRitualState
{
    NotStarted,
    InProgress,
    Completed,
    Cancelled
}

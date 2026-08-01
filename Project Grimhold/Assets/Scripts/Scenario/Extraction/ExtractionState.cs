/// <summary>
/// Networked process states for player extraction.
/// </summary>
public enum ExtractionState
{
    /// <summary>
    /// Player is not currently extracting.
    /// </summary>
    None,

    /// <summary>
    /// Player is actively undergoing extraction countdown.
    /// </summary>
    InProgress,

    /// <summary>
    /// Terminal state reached after successful extraction countdown completion.
    /// </summary>
    Extracted
}

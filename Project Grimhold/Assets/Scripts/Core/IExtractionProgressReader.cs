/// <summary>
/// Read-only capability that projects a player's confirmed individual extraction progress.
/// </summary>
public interface IExtractionProgressReader : IEntity
{
    /// <summary>
    /// Reads the current replicated progress without mutating simulation state.
    /// </summary>
    bool TryGetSnapshot(out ExtractionProgressSnapshot snapshot);
}

using UnityEngine;

/// <summary>
/// Capability contract for gameplay entities capable of participating in extraction zones.
/// Decouples spatial zone broadphase detection from concrete player controllers.
/// </summary>
public interface IExtractionParticipant : IEntity
{
    /// <summary>
    /// Gets the current extraction process state of the participant.
    /// </summary>
    ExtractionState State { get; }

    /// <summary>
    /// Gets the canonical entity identifier of the active extraction zone, or default if none.
    /// </summary>
    EntityId ActiveZoneId { get; }

    /// <summary>
    /// Gets the authoritative world point used by extraction geometry checks.
    /// </summary>
    Vector2 ValidationPoint { get; }

    /// <summary>
    /// Attempts to initiate extraction in the specified zone under State Authority.
    /// </summary>
    /// <param name="zoneId">Target extraction zone entity ID.</param>
    /// <returns><see langword="true"/> if extraction initiated; otherwise, <see langword="false"/>.</returns>
    bool TryBeginExtraction(EntityId zoneId);

    /// <summary>
    /// Notifies the participant that an exit event was detected for a zone.
    /// </summary>
    /// <param name="zoneId">Target extraction zone entity ID.</param>
    void NotifyExtractionZoneExit(EntityId zoneId);
}

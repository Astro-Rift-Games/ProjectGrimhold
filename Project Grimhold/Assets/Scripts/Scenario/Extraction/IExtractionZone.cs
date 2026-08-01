using UnityEngine;

/// <summary>
/// Contract representing an extraction zone entity in the game world.
/// Exposes spatial detection methods and network availability state.
/// </summary>
public interface IExtractionZone : IEntity
{
    /// <summary>
    /// Gets whether the extraction zone is currently active and available for extraction.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Evaluates whether a given point is exactly contained within the physical boundary of the extraction zone.
    /// </summary>
    /// <param name="point">The 2D world position to evaluate.</param>
    /// <returns><see langword="true"/> if the point is strictly inside the zone boundary; otherwise, <see langword="false"/>.</returns>
    bool ContainsExact(Vector2 point);

    /// <summary>
    /// Evaluates whether a given point is contained within the zone boundary extended by a non-negative tolerance buffer.
    /// </summary>
    /// <param name="point">The 2D world position to evaluate.</param>
    /// <param name="tolerance">Outward buffer distance from the zone boundary. Must be finite and non-negative.</param>
    /// <returns><see langword="true"/> if the point is within the zone or its tolerance buffer; otherwise, <see langword="false"/>.</returns>
    bool ContainsWithTolerance(Vector2 point, float tolerance);

    /// <summary>
    /// Authoritatively modifies the zone's availability state.
    /// Requires State Authority.
    /// </summary>
    /// <param name="available">Target availability state.</param>
    /// <returns><see langword="true"/> if availability was modified or was already set to the target value; otherwise, <see langword="false"/>.</returns>
    bool TrySetAvailability(bool available);
}

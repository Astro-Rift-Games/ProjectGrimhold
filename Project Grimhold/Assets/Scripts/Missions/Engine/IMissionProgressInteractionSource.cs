using UnityEngine;

/// <summary>
/// Capability exposed by interactable entities (like chests) to provide progress to missions upon interaction.
/// </summary>
public interface IMissionProgressInteractionSource
{
    EntityId Id { get; }
    
    /// <summary>
    /// The target ID configured for this interaction (e.g. 'chest_wooden', 'chest_silver').
    /// </summary>
    string TargetId { get; }

    /// <summary>
    /// The zone ID where this interactable resides.
    /// </summary>
    string ZoneId { get; }

    /// <summary>
    /// How much progress interacting with this source grants.
    /// </summary>
    int InteractionProgressAmount { get; }
}

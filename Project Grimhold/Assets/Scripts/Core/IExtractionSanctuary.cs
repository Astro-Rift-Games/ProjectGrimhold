/// <summary>
/// Network sanctuary capability whose replicated owner is the authoritative reservation state.
/// </summary>
public interface IExtractionSanctuary : IEntity
{
    /// <summary>Gets the replicated owner, or the default identity while unreserved.</summary>
    EntityId OwnerId { get; }

    /// <summary>Gets whether this sanctuary currently has a valid owner.</summary>
    bool IsReserved { get; }

    /// <summary>Gets the replicated ritual lifecycle for this Sanctuary.</summary>
    ExtractionRitualState RitualState { get; }

    /// <summary>Returns whether the supplied valid player identity owns this sanctuary.</summary>
    bool IsOwnedBy(EntityId playerId);

    /// <summary>
    /// Returns whether the supplied player owns this Sanctuary and has completed its ritual.
    /// </summary>
    bool CanUseExtraction(EntityId playerId);

    /// <summary>Builds a side-effect-free snapshot from confirmed ritual state.</summary>
    bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot);

    /// <summary>
    /// Reserves this sanctuary under State Authority without replacing a different owner.
    /// </summary>
    bool TryReserve(EntityId playerId);
}

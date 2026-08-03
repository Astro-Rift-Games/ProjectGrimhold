/// <summary>
/// Network sanctuary capability whose replicated owner is the authoritative reservation state.
/// </summary>
public interface IExtractionSanctuary : IEntity
{
    /// <summary>Gets the replicated owner, or the default identity while unreserved.</summary>
    EntityId OwnerId { get; }

    /// <summary>Gets whether this sanctuary currently has a valid owner.</summary>
    bool IsReserved { get; }

    /// <summary>Returns whether the supplied valid player identity owns this sanctuary.</summary>
    bool IsOwnedBy(EntityId playerId);

    /// <summary>
    /// Reserves this sanctuary under State Authority without replacing a different owner.
    /// </summary>
    bool TryReserve(EntityId playerId);
}

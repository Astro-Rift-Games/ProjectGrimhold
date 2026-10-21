/// <summary>
/// Network sanctuary capability whose replicated team slots maintain authoritative reservation and ritual state.
/// A Sanctuary is reserved exclusively for one owning team (GD 06 section 10).
/// </summary>
public interface IExtractionSanctuary : IEntity
{
    /// <summary>Returns whether this sanctuary is assigned to the specified team.</summary>
    bool IsAssignedToTeam(RaidTeamId teamId);

    /// <summary>Assigns this sanctuary to the specified team under State Authority. Idempotent for same team.</summary>
    bool TryAssignTeam(RaidTeamId teamId);

    /// <summary>Returns whether the team is assigned to this sanctuary and has completed its ritual.</summary>
    bool CanUseExtraction(RaidTeamId teamId);

    /// <summary>Builds a side-effect-free snapshot of ritual progress for the specified team.</summary>
    bool TryGetRitualProgress(RaidTeamId teamId, out ExtractionRitualSnapshot snapshot);

    /// <summary>Gets whether any assigned team has completed their ritual at this sanctuary.</summary>
    bool IsAnyRitualCompleted { get; }

    /// <summary>Gets whether at least one team is currently assigned to this sanctuary.</summary>
    bool IsReserved { get; }

    /// <summary>Legacy helper: gets the first assigned team identity or default.</summary>
    EntityId OwnerId { get; }

    /// <summary>Legacy helper: aggregate ritual lifecycle for this sanctuary.</summary>
    ExtractionRitualState RitualState { get; }

    /// <summary>Legacy helper: returns whether the player's team is assigned to this sanctuary.</summary>
    bool IsOwnedBy(EntityId playerId);

    /// <summary>Legacy helper: returns whether the player's team can extract.</summary>
    bool CanUseExtraction(EntityId playerId);

    /// <summary>Legacy helper: builds snapshot of ritual progress for the local team or first active.</summary>
    bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot);

    /// <summary>Legacy helper: reserves this sanctuary for the player's team.</summary>
    bool TryReserve(EntityId playerId);
}

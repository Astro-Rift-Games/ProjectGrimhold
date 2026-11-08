/// <summary>
/// The Town gate that closes build changes once the local player is Ready for a raid. Every Town
/// screen that mutates the build (Attributes, Abilities, Inventory Equipment and the Stash) uses it,
/// so they cannot disagree about when preparation is locked. The rule itself is
/// <see cref="TownRaidPreparationRules.CanMutateTownBuild"/>.
/// </summary>
public static class TownEquipmentMutationGate
{
    /// <summary>
    /// The build may change unless the local player has a preparation and is Ready in it. A missing
    /// directory or preparation means no raid preparation exists yet, so nothing is locked.
    /// </summary>
    public static bool CanMutate(TownRaidPreparationDirectory directory, ProfileId localProfileId)
    {
        if (directory == null ||
            !directory.TryGetPreparation(localProfileId, out TownRaidPreparationNetworkController preparation))
        {
            return true;
        }

        return TownRaidPreparationRules.CanMutateTownBuild(preparation.Snapshot, localProfileId);
    }
}

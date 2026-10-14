/// <summary>
/// The Town rule that closes Equipment changes once the local player is Ready for a raid. Every
/// Town screen that mutates Equipment (the Inventory screen and the Stash screen) uses it, so they
/// cannot disagree about when preparation is locked.
/// </summary>
public static class TownEquipmentMutationGate
{
    /// <summary>
    /// Equipment may change unless the local player has a preparation and is Ready in it. A missing
    /// directory or preparation means no raid preparation exists yet, so nothing is locked.
    /// </summary>
    public static bool CanMutate(TownRaidPreparationDirectory directory, ProfileId localProfileId)
    {
        if (directory == null ||
            !directory.TryGetPreparation(localProfileId, out TownRaidPreparationNetworkController preparation))
        {
            return true;
        }

        return TownRaidPreparationPresentation.TryCreate(
                preparation.Snapshot,
                localProfileId,
                out TownRaidPreparationPresentation presentation) &&
            !presentation.LocalReady;
    }
}

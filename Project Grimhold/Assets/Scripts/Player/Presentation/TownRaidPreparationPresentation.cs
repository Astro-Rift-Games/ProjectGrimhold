using System.Collections.Generic;

/// <summary>Pure local presentation projection for one player's current Town preparation.</summary>
public readonly struct TownRaidPreparationPresentation
{
    public TownRaidPreparationSnapshot Snapshot { get; }
    public bool IsHost { get; }
    public bool LocalReady { get; }
    public bool CanStart { get; }

    private TownRaidPreparationPresentation(
        in TownRaidPreparationSnapshot snapshot,
        bool isHost,
        bool localReady,
        bool canStart)
    {
        Snapshot = snapshot;
        IsHost = isHost;
        LocalReady = localReady;
        CanStart = canStart;
    }

    public static bool TryCreate(
        in TownRaidPreparationSnapshot snapshot,
        ProfileId localProfileId,
        out TownRaidPreparationPresentation presentation)
    {
        presentation = default;
        if (!localProfileId.IsValid || !TownRaidPreparationRules.IsValidSnapshot(snapshot))
        {
            return false;
        }

        IReadOnlyList<TownRaidPreparationMember> members = snapshot.Members;
        bool found = false;
        bool localReady = false;
        for (int index = 0; index < members.Count; index++)
        {
            if (members[index].ProfileId != localProfileId)
            {
                continue;
            }

            found = true;
            localReady = members[index].IsReady;
            break;
        }

        if (!found)
        {
            return false;
        }

        bool isHost = snapshot.HostProfileId == localProfileId;
        presentation = new TownRaidPreparationPresentation(
            snapshot,
            isHost,
            localReady,
            TownRaidPreparationRules.CanStart(snapshot, localProfileId));
        return true;
    }
}

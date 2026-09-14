public readonly struct TownPartyPresentation
{
    public string LocalDisplayName { get; }
    public string CompanionDisplayName { get; }
    public bool HasCompanion { get; }

    public TownPartyPresentation(string localDisplayName, string companionDisplayName, bool hasCompanion)
    {
        LocalDisplayName = localDisplayName ?? string.Empty;
        CompanionDisplayName = companionDisplayName ?? string.Empty;
        HasCompanion = hasCompanion;
    }

    public static bool TryCreate(
        in TownPartySnapshot party,
        ProfileId localProfile,
        string localDisplayName,
        string companionDisplayName,
        out TownPartyPresentation presentation)
    {
        presentation = default;
        if (!TownPartyRules.IsValid(party) || !party.Contains(localProfile)) return false;

        bool hasCompanion = party.Members.Count == TownPartyRules.MaxMembers;
        presentation = new TownPartyPresentation(
            ResolveName(localDisplayName, localProfile),
            hasCompanion ? ResolveName(companionDisplayName, GetCompanion(party, localProfile)) : string.Empty,
            hasCompanion);
        return true;
    }

    private static ProfileId GetCompanion(in TownPartySnapshot party, ProfileId localProfile) =>
        party.Members[0] == localProfile ? party.Members[1] : party.Members[0];

    private static string ResolveName(string displayName, ProfileId fallback) =>
        string.IsNullOrWhiteSpace(displayName) ? fallback.Value : displayName;
}

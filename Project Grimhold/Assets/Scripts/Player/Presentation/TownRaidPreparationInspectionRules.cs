/// <summary>Local-only selection rules for inspecting another preparation member.</summary>
public static class TownRaidPreparationInspectionRules
{
    public static bool TryFindInspectable(in TownRaidPreparationPresentation presentation,
        ProfileId profileId, out TownRaidPreparationPresentation.Member member)
    {
        member = default;
        if (presentation.Members == null) return false;
        for (int index = 0; index < presentation.Members.Count; index++)
        {
            var candidate = presentation.Members[index];
            if (candidate.ProfileId != profileId || candidate.IsLocal) continue;
            member = candidate;
            return true;
        }
        return false;
    }
}

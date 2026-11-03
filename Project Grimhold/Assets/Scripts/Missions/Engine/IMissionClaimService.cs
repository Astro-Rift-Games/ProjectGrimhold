/// <summary>
/// Domain service interface for validating and executing mission reward claims with atomic item deduction.
/// </summary>
public interface IMissionClaimService
{
    bool CanClaimMission(MissionDefinition mission, out string failureReason);
    StashOperationResult TryClaimMission(MissionDefinition mission);
}

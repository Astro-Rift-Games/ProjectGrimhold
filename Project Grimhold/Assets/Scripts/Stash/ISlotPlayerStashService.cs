/// <summary>Addressed consumption alongside definition-wide IPlayerStashService consumption.</summary>
public interface ISlotPlayerStashService
{
    StashOperationResult TryConsumeSlot(ProfileId profile, int slot, LootId id, int amount);
}

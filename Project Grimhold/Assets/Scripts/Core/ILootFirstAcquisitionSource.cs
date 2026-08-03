/// <summary>
/// Optional loot-source capability that resolves natural units not yet acquired by a player.
/// The query is side-effect free and may only be used after successful source validation.
/// </summary>
public interface ILootFirstAcquisitionSource : IEntity
{
    LootFirstAcquisitionResult ResolveFirstAcquisition(in LootTransferRequest request);
}

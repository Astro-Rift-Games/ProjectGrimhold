using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>A complete, revision-consistent response, not the partial reservation POST response.</summary>
public sealed class RecoveryWeaponAuthorityState
{
    public InventoryData Inventory { get; }
    public ProgressionData Progression { get; }
    public RecoveryWeaponAuthorityState(InventoryData inventory, ProgressionData progression)
    {
        Inventory = inventory;
        Progression = progression;
    }
}

public interface IRecoveryWeaponAuthorityReader
{
    Task<RecoveryWeaponAuthorityState> FetchRecoveryWeaponAuthorityAsync();
}

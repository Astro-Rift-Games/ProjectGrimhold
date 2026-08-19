using UnityEngine;

/// <summary>
/// Runtime configuration required to initialize the local profile aggregate.
/// </summary>
[CreateAssetMenu(fileName = "LocalProfilePersistenceConfiguration", menuName = "Grimhold/Persistence/Local Profile Configuration")]
public sealed class LocalProfilePersistenceConfiguration : ScriptableObject
{
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField, Min(1)] private int _receiptCapacity = LocalProfileSnapshot.MaxAppliedExtractionReceipts;

    [SerializeField]
    [Tooltip("Loot id of the base recovery weapon Town guarantees when no weapon is prepared. " +
             "Must be a Weapon category loot with a valid WeaponDefinition. While empty, a profile " +
             "without any prepared weapon cannot start an expedition.")]
    private string _recoveryWeaponLootId;

    public LootDefinitionCatalog LootCatalog => _lootCatalog;
    public int ReceiptCapacity => _receiptCapacity;

    public LootId RecoveryWeaponLootId =>
        string.IsNullOrWhiteSpace(_recoveryWeaponLootId) ? default : new LootId(_recoveryWeaponLootId);
}

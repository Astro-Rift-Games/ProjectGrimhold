using UnityEngine;

/// <summary>
/// Runtime configuration required to initialize local profile persistence.
/// </summary>
[CreateAssetMenu(fileName = "LocalProfilePersistenceConfiguration", menuName = "Grimhold/Persistence/Local Profile Configuration")]
public sealed class LocalProfilePersistenceConfiguration : ScriptableObject
{
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField, Min(1)] private int _receiptCapacity = LocalProfileSnapshot.MaxAppliedExtractionReceipts;

    public LootDefinitionCatalog LootCatalog => _lootCatalog;
    public int ReceiptCapacity => _receiptCapacity;
}

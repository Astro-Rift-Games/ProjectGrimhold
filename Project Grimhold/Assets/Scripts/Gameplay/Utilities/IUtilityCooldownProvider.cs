using Fusion;

/// <summary>
/// Contrato para consultar y limpiar el cooldown persistente de una utilidad de expedición (Doc 14 TASK-234).
/// </summary>
public interface IUtilityCooldownProvider
{
    TickTimer GetUtilityCooldown(LootId lootId);
    void ClearUtilityCooldown(LootId lootId);
}

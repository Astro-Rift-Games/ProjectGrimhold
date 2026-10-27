using Fusion;

/// <summary>
/// Contrato para recibir o restaurar el cooldown activo de una utilidad al recogerla o transferirla (Doc 14 TASK-234).
/// </summary>
public interface IUtilityCooldownReceiver
{
    void SetUtilityCooldown(LootId lootId, TickTimer timer);
}

/// <summary>
/// Contrato para entidades que pueden recibir y gestionar maná.
/// </summary>
public interface IManaReceiver
{
    float CurrentMana { get; }
    float MaximumMana { get; }
    bool TryRestoreMana(float amount, out string failureReason);
}

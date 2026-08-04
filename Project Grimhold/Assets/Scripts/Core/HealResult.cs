/// <summary>
/// Resultado inmutable devuelto por un IHealable tras procesar una solicitud de curación.
/// </summary>
public readonly struct HealResult
{
    public EntityId TargetId { get; }
    public bool Success { get; }
    public float AmountHealed { get; }
    public float NewHealth { get; }
    public HealFailureReason FailureReason { get; }

    public HealResult(
        EntityId targetId,
        bool success,
        float amountHealed,
        float newHealth,
        HealFailureReason failureReason)
    {
        TargetId = targetId;
        Success = success;
        AmountHealed = amountHealed;
        NewHealth = newHealth;
        FailureReason = failureReason;
    }
}

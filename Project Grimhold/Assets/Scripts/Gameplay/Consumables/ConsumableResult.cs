/// <summary>
/// Estructura inmutable que encapsula el resultado de la solicitud de uso de un consumible.
/// </summary>
public readonly struct ConsumableResult
{
    public bool Success { get; }
    public ConsumableFailureReason FailureReason { get; }

    private ConsumableResult(bool success, ConsumableFailureReason failureReason)
    {
        Success = success;
        FailureReason = failureReason;
    }

    public static ConsumableResult Ok() => new ConsumableResult(true, ConsumableFailureReason.None);
    public static ConsumableResult Rejected(ConsumableFailureReason reason) => new ConsumableResult(false, reason);
}

/// <summary>
/// Solicitud inmutable de curación transportada a través del pipeline autoritativo.
/// </summary>
public readonly struct HealRequest
{
    public float Amount { get; }

    public HealRequest(float amount)
    {
        Amount = amount;
    }
}

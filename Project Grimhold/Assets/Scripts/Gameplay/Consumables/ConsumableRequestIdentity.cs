/// <summary>
/// Estructura inmutable para identificar y rastrear solicitudes de consumo en el pipeline de red.
/// </summary>
public readonly struct ConsumableRequestIdentity
{
    public uint RequestSequence { get; }
    public int CatalogIndex { get; }

    public ConsumableRequestIdentity(uint requestSequence, int catalogIndex)
    {
        RequestSequence = requestSequence;
        CatalogIndex = catalogIndex;
    }
}

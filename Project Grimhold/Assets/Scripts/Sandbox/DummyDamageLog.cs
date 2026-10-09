#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// Pure damage accumulator for the training dummy. Holds no Unity or Fusion state.
/// </summary>
public sealed class DummyDamageLog
{
    public float LastDamage { get; private set; }
    public float TotalDamage { get; private set; }
    public int HitCount { get; private set; }

    /// <summary>Records one hit. Non-positive and non-finite amounts are ignored.</summary>
    public void Record(float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount))
        {
            return;
        }

        LastDamage = amount;
        TotalDamage += amount;
        HitCount++;
    }

    public void Reset()
    {
        LastDamage = 0f;
        TotalDamage = 0f;
        HitCount = 0;
    }
}
#endif

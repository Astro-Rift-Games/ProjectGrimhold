/// <summary>
/// Receives resolved damage synchronously at the authoritative combat boundary.
/// Implementations may record presentation metadata but must not change damage.
/// </summary>
public interface IResolvedDamageFeedbackSink
{
    void RecordResolvedDamage(in DamageResolvedEvent resolvedDamage);
}

/// <summary>
/// Authoritative side effects requested by <see cref="ProjectileContactResolver"/> in spatial order.
/// </summary>
public interface IProjectileContactSink
{
    /// <summary>Attempts to consume the interceptor. False means it is no longer active and is passed through.</summary>
    bool TryIntercept(in ProjectileContact contact);

    /// <summary>Applies one impact on the contact's target.</summary>
    void ApplyImpact(in ProjectileContact contact);
}

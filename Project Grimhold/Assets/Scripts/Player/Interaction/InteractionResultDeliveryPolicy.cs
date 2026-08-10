/// <summary>
/// Pure routing policy for authoritative interaction-result presentation.
/// Shared Mode owners deliver locally; Host/Client authority sends only to a remote owner.
/// </summary>
public static class InteractionResultDeliveryPolicy
{
    /// <summary>Returns whether the authoritative owner can enqueue presentation without transport.</summary>
    public static bool ShouldEnqueueLocally(bool hasStateAuthority, bool hasInputAuthority)
    {
        return hasStateAuthority && hasInputAuthority;
    }

    /// <summary>Returns whether State Authority must transport the result to a remote Input Authority.</summary>
    public static bool ShouldSendRemote(bool hasStateAuthority, bool hasInputAuthority)
    {
        return hasStateAuthority && !hasInputAuthority;
    }
}

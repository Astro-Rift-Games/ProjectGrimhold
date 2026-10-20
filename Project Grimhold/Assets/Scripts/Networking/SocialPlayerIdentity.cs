using Fusion;
using UnityEngine;

/// <summary>
/// Replicates the local persistent profile identity of a Town PlayerObject.
/// The Shared Mode queue uses it to validate RPC senders without treating PlayerRef as profile identity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class SocialPlayerIdentity : NetworkBehaviour
{
    [Networked]
    public NetworkString<_32> ProfileId { get; private set; }

    [Networked]
    public NetworkString<_32> DisplayName { get; private set; }

    [Networked]
    public NetworkString<_512> SessionTicket { get; private set; }

    [Networked]
    public NetworkBool HasPendingPartyContinuation { get; private set; }

    private string _lastPrewarmedTicket;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            LocalPlayerJoinContext context = Runner.GetComponent<LocalPlayerJoinContext>();
            if (context == null || !context.JoinData.ProfileId.IsValid)
            {
                Debug.LogError($"{nameof(SocialPlayerIdentity)} requires a valid local profile.", this);
                return;
            }

            ProfileId = context.JoinData.ProfileId.Value;
            string displayName = ApplicationAuthContext.Instance != null
                ? ApplicationAuthContext.Instance.CharacterName
                : null;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? context.JoinData.ProfileId.Value : displayName.Trim();

            string ticket = ApplicationAuthContext.Instance != null && ApplicationAuthContext.Instance.ActiveTownTicket.IsValid
                ? ApplicationAuthContext.Instance.ActiveTownTicket.RawTicket
                : null;
            if (!string.IsNullOrWhiteSpace(ticket))
            {
                SessionTicket = ticket;
            }

            RefreshContinuationState();
        }

        PrewarmTicketVerificationIfNeeded();
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            RefreshContinuationState();
        }
    }

    public override void Render()
    {
        PrewarmTicketVerificationIfNeeded();
    }

    private void PrewarmTicketVerificationIfNeeded()
    {
        string rawTicket = SessionTicket.ToString();
        if (string.IsNullOrWhiteSpace(rawTicket) ||
            string.Equals(_lastPrewarmedTicket, rawTicket, System.StringComparison.Ordinal))
        {
            return;
        }

        _lastPrewarmedTicket = rawTicket;
        ITownSessionTicketVerifier verifier = ApplicationAuthContext.Instance?.TownTicketVerifier;
        if (verifier != null)
        {
            _ = verifier.VerifyTownTicketAsync(rawTicket);
        }
    }

    private void RefreshContinuationState()
    {
        bool hasPending = SessionConnectionCoordinator.Instance?.HasPendingPartyContinuation ?? false;
        if ((bool)HasPendingPartyContinuation != hasPending)
        {
            HasPendingPartyContinuation = hasPending;
        }
    }
}

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
    public NetworkBool HasPendingPartyContinuation { get; private set; }

    [Networked]
    public NetworkBool IsAwaitingPartyCompletion { get; private set; }

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

            RefreshContinuationState();
            PublishTownTicket();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            RefreshContinuationState();
            PublishTownTicket();
        }
    }

    /// <summary>
    /// Delivers the ticket only to the Town State Authority. The sender is the RPC source (<c>info.Source</c>),
    /// never a payload field, so a client cannot register a ticket on behalf of another PlayerRef.
    /// </summary>
    [Rpc(Channel = RpcChannel.ReliableLargeData, TickAligned = false)]
    public static void RPC_RegisterTownSender(
        NetworkRunner runner,
        [RpcTarget] PlayerRef target,
        string rawTicket,
        RpcInfo info = default)
    {
        if (info.IsInvokeLocal || info.Source.IsNone || runner == null)
        {
            return;
        }

        TownSenderIdentityContext context = runner.GetComponent<TownSenderIdentityContext>();
        context?.AcceptRegistration(info.Source, rawTicket, player => TownSenderIdentityContext.IsActive(runner, player));
    }

    private void PublishTownTicket()
    {
        ApplicationAuthContext auth = ApplicationAuthContext.Instance;
        if (auth == null || !auth.ActiveTownTicket.IsValid)
        {
            return;
        }

        Runner.GetComponent<TownSenderIdentityContext>()?.PublishLocalTicket(Runner, auth.ActiveTownTicket.RawTicket);
    }

    private void RefreshContinuationState()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        bool hasPending = coordinator != null && coordinator.HasPendingPartyContinuation;
        if ((bool)HasPendingPartyContinuation != hasPending)
        {
            HasPendingPartyContinuation = hasPending;
        }

        bool awaitingPartyCompletion = coordinator != null && coordinator.IsAwaitingPartyCompletion;
        if ((bool)IsAwaitingPartyCompletion != awaitingPartyCompletion)
        {
            IsAwaitingPartyCompletion = awaitingPartyCompletion;
        }
    }
}

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

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        LocalPlayerJoinContext context = Runner.GetComponent<LocalPlayerJoinContext>();
        if (context == null || !context.JoinData.ProfileId.IsValid)
        {
            Debug.LogError($"{nameof(SocialPlayerIdentity)} requires a valid local profile.", this);
            return;
        }

        ProfileId = context.JoinData.ProfileId.Value;
    }
}

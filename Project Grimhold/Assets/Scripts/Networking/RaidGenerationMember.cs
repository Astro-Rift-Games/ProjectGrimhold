using Fusion;
using UnityEngine;

/// <summary>
/// Optional replicated generation marker for a runtime raid network object.
/// Objects without this component remain covered by the runner-scoped cleanup
/// fallback in <see cref="NetworkSpawnManager"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class RaidGenerationMember : NetworkBehaviour
{
    [Networked]
    public NetworkString<_32> RaidGenerationId { get; private set; }

    public override void Spawned()
    {
        if (!HasStateAuthority || !string.IsNullOrEmpty(RaidGenerationId.ToString()))
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        NetworkMatchController matchController = spawnManager?.MatchController;
        if (matchController != null)
        {
            RaidGenerationId = matchController.RaidGenerationId;
        }
    }
}

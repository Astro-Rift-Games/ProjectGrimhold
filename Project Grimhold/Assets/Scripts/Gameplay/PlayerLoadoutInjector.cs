using Fusion;
using UnityEngine;

/// <summary>
/// Compatibility shell for stale Unity-generated prefab metadata.
/// Raid admission is owned by <see cref="NetworkSpawnManager"/> and this type
/// must not read local persistence or mutate player inventory.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLootReceiver))]
[RequireComponent(typeof(PlayerCharacter))]
public class PlayerLoadoutInjector : NetworkBehaviour
{
    public override void Spawned()
    {
        // Admission data is now supplied by NetworkSpawnManager. This legacy
        // component remains source-compatible until Unity regenerates prefab data.
    }
}

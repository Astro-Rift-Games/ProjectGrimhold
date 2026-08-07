using UnityEngine;

/// <summary>
/// Composition root for stash services at the application level.
/// Decouples consumers from concrete stash implementations.
/// </summary>
[DisallowMultipleComponent]
public sealed class ApplicationStashContext : MonoBehaviour
{
    public IPlayerStashService StashService { get; private set; }

    /// <summary>
    /// Injects the concrete implementation of the stash service.
    /// This should only be called during initialization by a bootstrapper.
    /// </summary>
    public void Initialize(IPlayerStashService stashService)
    {
        StashService = stashService;
    }
}

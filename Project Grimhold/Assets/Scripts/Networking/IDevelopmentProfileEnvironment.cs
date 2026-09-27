/// <summary>
/// Narrow deterministic testing seam over the process-local profile/stash services that
/// <see cref="DirectRaidDevelopmentPreparation"/> needs to prepare an offline development
/// profile: assigning/clearing the current <see cref="ProfileId"/>, initializing the local
/// stash, and resolving the active <see cref="IPlayerLoadoutService"/>. Production code is
/// backed by <see cref="LocalProfileProvider"/> and <see cref="ApplicationStashServiceBootstrapper"/>;
/// tests substitute a fake to observe calls deterministically without touching Unity statics.
/// </summary>
public interface IDevelopmentProfileEnvironment
{
    /// <summary>The profile currently active in this process, default/invalid when none.</summary>
    ProfileId CurrentProfile { get; }

    /// <summary>Assigns the given profile as the current one for this process.</summary>
    void AssignProfile(ProfileId profileId);

    /// <summary>Clears the current profile, restoring the unauthenticated default.</summary>
    void ClearProfile();

    /// <summary>
    /// Initializes the local stash for the given profile. Returns false when initialization
    /// failed (e.g. missing configuration).
    /// </summary>
    bool TryInitializeLocalStash(ProfileId profileId);

    /// <summary>The active loadout service, or null when the stash is not initialized yet.</summary>
    IPlayerLoadoutService LoadoutService { get; }
}

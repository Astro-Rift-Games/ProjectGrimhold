/// <summary>
/// Outcome of <see cref="DirectRaidDevelopmentPreparation.Prepare"/>: the pure rule that governs
/// whether <see cref="DevelopmentProfileBootstrap"/> may hand <see cref="DirectRaidDevelopmentStarter"/>
/// a launchable offline development profile for one direct raid attempt.
/// </summary>
public enum DirectRaidDevelopmentPreparationResult
{
    /// <summary>The bypass is disabled; nothing was touched and the legacy (real-login) route is unchanged.</summary>
    NotRequested,

    /// <summary>The current build does not allow offline development identities.</summary>
    UnavailableInBuild,

    /// <summary>The configured development profile key does not satisfy <see cref="DevelopmentProfileIdentity.TryCreate"/>.</summary>
    InvalidKey,

    /// <summary>The local stash could not be initialized, or no loadout service is available.</summary>
    StashUnavailable,

    /// <summary>A profile is present but has no launchable loadout (<see cref="IPlayerLoadoutService.TryPrepareExpeditionLoadout"/> failed).</summary>
    LoadoutNotLaunchable,

    /// <summary>A profile with a launchable loadout is ready for the direct raid attempt.</summary>
    Prepared
}

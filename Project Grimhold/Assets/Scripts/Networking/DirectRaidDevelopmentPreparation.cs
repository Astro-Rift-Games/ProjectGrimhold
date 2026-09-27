using System;

/// <summary>
/// Pure orchestration rule that prepares a launchable offline development profile for one
/// <see cref="DirectRaidDevelopmentStarter"/> direct raid attempt (Host or Client). Holds no
/// state of its own; every side effect goes through <see cref="IDevelopmentProfileEnvironment"/>,
/// so this rule is exercised deterministically in EditMode tests without Unity statics.
/// </summary>
public static class DirectRaidDevelopmentPreparation
{
    /// <summary>
    /// Evaluates whether a direct raid attempt may proceed and, when it may, leaves
    /// <paramref name="environment"/> with a profile whose loadout has a launchable weapon.
    /// Never overwrites an already-valid profile (real, or a dev profile assigned by an earlier
    /// call in the same process), and never touches anything when the bypass is disabled or the
    /// build does not allow it.
    /// </summary>
    public static DirectRaidDevelopmentPreparationResult Prepare(
        bool buildAllowsDevelopment,
        bool enabled,
        string profileKey,
        IDevelopmentProfileEnvironment environment,
        out ProfileId profileId,
        out ExpeditionPreparationResult loadoutPreparation)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        profileId = default;
        loadoutPreparation = ExpeditionPreparationResult.ProfileUnavailable;

        // Disabled never touches anything, regardless of build: a build that disallows
        // development identities is irrelevant when the bypass was never requested.
        if (!enabled)
        {
            return DirectRaidDevelopmentPreparationResult.NotRequested;
        }

        if (!buildAllowsDevelopment)
        {
            return DirectRaidDevelopmentPreparationResult.UnavailableInBuild;
        }

        ProfileId current = environment.CurrentProfile;
        if (current.IsValid)
        {
            // A profile is already present for this process: either a real, logged-in profile,
            // or a dev profile this same rule assigned on an earlier direct raid attempt in this
            // session. Either way it is never replaced. Its stash is also never reinitialized
            // here: a real profile's stash is not this rule's responsibility to touch, and a dev
            // profile's stash was already initialized exactly once by the AssignProfile branch
            // below on the attempt that created it (InitializeWithProfile would short-circuit for
            // the same id anyway, so skipping it is simplest and equally correct).
            profileId = current;
        }
        else
        {
            if (!DevelopmentProfileIdentity.TryCreate(profileKey, out profileId))
            {
                return DirectRaidDevelopmentPreparationResult.InvalidKey;
            }

            environment.AssignProfile(profileId);
            if (!environment.TryInitializeLocalStash(profileId))
            {
                // Only clear what this call assigned; never touch a profile that pre-existed.
                environment.ClearProfile();
                profileId = default;
                return DirectRaidDevelopmentPreparationResult.StashUnavailable;
            }
        }

        IPlayerLoadoutService loadout = environment.LoadoutService;
        if (loadout == null)
        {
            return DirectRaidDevelopmentPreparationResult.StashUnavailable;
        }

        // Always run the same normalization the Town launch path runs before its reservation
        // boundary (recovery weapon when no Main Hand is prepared). It is atomic, deterministic
        // and idempotent, so calling it again on a second direct raid attempt for an already
        // prepared/reserved profile commits nothing and grants nothing twice.
        loadoutPreparation = loadout.TryPrepareExpeditionLoadout(profileId);
        return loadoutPreparation == ExpeditionPreparationResult.Success
            ? DirectRaidDevelopmentPreparationResult.Prepared
            : DirectRaidDevelopmentPreparationResult.LoadoutNotLaunchable;
    }
}

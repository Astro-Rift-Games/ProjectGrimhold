using UnityEngine;

/// <summary>
/// Editor/Development-only offline identity and local stash preparation for
/// <see cref="DirectRaidDevelopmentStarter"/>. Never authenticates, never writes to the
/// backend, and never touches <see cref="ApplicationAuthContext"/>. Has no effect in release
/// builds: the entire <see cref="TryPrepareForDirectRaid"/> body is compiled out there.
/// Serialized fields stay always compiled (no <c>#if</c> around them) so the release player
/// keeps the same serialization layout as the Editor/Development component.
///
/// Has no Unity lifecycle side effects: no logic runs on <c>Awake</c>/<c>OnEnable</c>/<c>Start</c>.
/// <see cref="TryPrepareForDirectRaid"/> is invoked lazily by <see cref="DirectRaidDevelopmentStarter"/>
/// immediately before every direct raid attempt (Host or Client), so a disabled bypass leaves
/// the legacy (real-login) route completely unchanged, and an enabled bypass is prepared fresh
/// on every attempt rather than only once per Play session.
/// </summary>
[DisallowMultipleComponent]
public sealed class DevelopmentProfileBootstrap : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Enables the offline development profile bootstrap. Has no effect outside " +
             "Editor/Development builds and never overwrites an existing (real) profile.")]
    private bool _enabled;

    [SerializeField]
    [Tooltip("Deterministic development profile key: lowercase ASCII letters, digits and " +
             "hyphens only, 1-32 characters after trimming. Produces ProfileId 'dev-local-<key>'.")]
    private string _profileKey = "host";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private UnityDevelopmentProfileEnvironment _environment;
#endif

    /// <summary>
    /// Prepares (or reuses) a launchable offline development profile for one direct raid
    /// attempt. Returns true when the attempt may proceed: either a profile was prepared
    /// (<see cref="DirectRaidDevelopmentPreparationResult.Prepared"/>), or the bypass is disabled
    /// and the caller should proceed with the legacy (real-login) route unchanged
    /// (<see cref="DirectRaidDevelopmentPreparationResult.NotRequested"/>). Returns false, with a
    /// logged error, for every other outcome. Resolves its Unity-side services lazily on first
    /// use per call; this is not a hot path (invoked once per direct raid attempt).
    /// </summary>
    public bool TryPrepareForDirectRaid()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _environment ??= new UnityDevelopmentProfileEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            DevelopmentProfileIdentity.IsAvailableInThisBuild,
            _enabled,
            _profileKey,
            _environment,
            out ProfileId profileId,
            out ExpeditionPreparationResult loadoutPreparation);

        switch (result)
        {
            case DirectRaidDevelopmentPreparationResult.NotRequested:
                return true;

            case DirectRaidDevelopmentPreparationResult.Prepared:
                if (DevelopmentProfileIdentity.IsDevelopmentProfile(profileId))
                {
                    Debug.LogWarning(
                        $"[{nameof(DevelopmentProfileBootstrap)}] OFFLINE DEVELOPMENT PROFILE active: " +
                        $"{profileId.Value}. Not authenticated. Local persistence only.",
                        this);
                }
                return true;

            case DirectRaidDevelopmentPreparationResult.UnavailableInBuild:
                Debug.LogError(
                    $"[{nameof(DevelopmentProfileBootstrap)}] Enabled but unavailable in this build. " +
                    "The offline development profile only bootstraps in Editor or Development builds.",
                    this);
                return false;

            case DirectRaidDevelopmentPreparationResult.InvalidKey:
                Debug.LogError(
                    $"[{nameof(DevelopmentProfileBootstrap)}] Invalid development profile key '{_profileKey}'. " +
                    "Use 1-32 lowercase letters, digits or hyphens.",
                    this);
                return false;

            case DirectRaidDevelopmentPreparationResult.StashUnavailable:
                Debug.LogError(
                    $"[{nameof(DevelopmentProfileBootstrap)}] Failed to initialize the local stash " +
                    "or resolve a loadout service for the offline development profile.",
                    this);
                return false;

            case DirectRaidDevelopmentPreparationResult.LoadoutNotLaunchable:
                Debug.LogError(
                    $"[{nameof(DevelopmentProfileBootstrap)}] Offline development profile has no " +
                    $"launchable loadout: {loadoutPreparation}.",
                    this);
                return false;

            default:
                Debug.LogError(
                    $"[{nameof(DevelopmentProfileBootstrap)}] Unhandled preparation result: {result}.",
                    this);
                return false;
        }
#else
        if (!_enabled)
        {
            return true;
        }

        Debug.LogError(
            $"[{nameof(DevelopmentProfileBootstrap)}] Enabled but unavailable in this build. " +
            "The offline development profile only bootstraps in Editor or Development builds.",
            this);
        return false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Adapts the process-local profile/stash statics to <see cref="IDevelopmentProfileEnvironment"/>.
    /// Resolves <see cref="ApplicationStashContext"/> lazily rather than caching it, since it is
    /// created by <see cref="ApplicationStashServiceBootstrapper"/> outside this component's
    /// control and only needs to be found once per direct raid attempt.
    /// </summary>
    private sealed class UnityDevelopmentProfileEnvironment : IDevelopmentProfileEnvironment
    {
        public ProfileId CurrentProfile => LocalProfileProvider.GetOrCreateLocalProfile();

        public void AssignProfile(ProfileId profileId) => LocalProfileProvider.SetRemoteCharacterId(profileId);

        public void ClearProfile() => LocalProfileProvider.ClearRemoteCharacterId();

        public bool TryInitializeLocalStash(ProfileId profileId) =>
            ApplicationStashServiceBootstrapper.InitializeWithProfile(profileId);

        public IPlayerLoadoutService LoadoutService
        {
            get
            {
                ApplicationStashContext stashContext = Object.FindAnyObjectByType<ApplicationStashContext>();
                return stashContext != null ? stashContext.LoadoutService : null;
            }
        }
    }
#endif
}

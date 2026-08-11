using System;
using System.Collections.Generic;

/// <summary>
/// Immutable admission contract for either a frozen Town cohort or a manually coded raid.
/// </summary>
public readonly struct RaidLaunchManifest
{
    public const int MaximumMembers = 4;

    /// <summary>
    /// User-facing code contract for manually created raid sessions.
    /// </summary>
    public static class Code
    {
        public const int Length = RaidCode.Length;

        /// <summary>Trims and validates an exact six-digit ASCII raid code.</summary>
        public static bool TryNormalize(string value, out string code)
        {
            bool isValid = RaidCode.TryParse(value, out RaidCode raidCode);
            code = isValid ? raidCode.Value : null;
            return isValid;
        }

        /// <summary>Generates a six-digit code suitable for the local create-raid form.</summary>
        public static string Generate()
        {
            return UnityEngine.Random.Range(0, 1_000_000).ToString("D6");
        }

        /// <summary>Builds the deterministic open-admission contract shared by Host and Clients.</summary>
        public static RaidLaunchManifest CreateManifest(string code)
        {
            if (!RaidCode.TryParse(code, out RaidCode raidCode))
            {
                return default;
            }

            return CreateCodeAdmission(raidCode.RaidId, raidCode.SessionName, $"code-{raidCode}", 1);
        }
    }

    private readonly ProfileId[] _admittedProfiles;
    private readonly bool _allowsCodeAdmission;

    public string RaidId { get; }
    public string SessionName { get; }
    public string AccessSecret { get; }
    public ProfileId HostProfileId { get; }
    public int LaunchSequence { get; }
    public IReadOnlyList<ProfileId> AdmittedProfiles => _admittedProfiles ?? Array.Empty<ProfileId>();
    public bool AllowsCodeAdmission => _allowsCodeAdmission;

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RaidId) || string.IsNullOrWhiteSpace(SessionName) ||
                string.IsNullOrWhiteSpace(AccessSecret) || LaunchSequence <= 0)
            {
                return false;
            }

            if (_allowsCodeAdmission)
            {
                return !HostProfileId.IsValid && (_admittedProfiles == null || _admittedProfiles.Length == 0);
            }

            if (!HostProfileId.IsValid || _admittedProfiles == null ||
                _admittedProfiles.Length == 0 || _admittedProfiles.Length > MaximumMembers)
            {
                return false;
            }

            bool containsHost = false;
            for (int i = 0; i < _admittedProfiles.Length; i++)
            {
                if (!_admittedProfiles[i].IsValid)
                {
                    return false;
                }

                if (_admittedProfiles[i] == HostProfileId)
                {
                    containsHost = true;
                }

                for (int other = i + 1; other < _admittedProfiles.Length; other++)
                {
                    if (_admittedProfiles[i] == _admittedProfiles[other])
                    {
                        return false;
                    }
                }
            }

            return containsHost;
        }
    }

    public RaidLaunchManifest(
        string raidId,
        string sessionName,
        string accessSecret,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> admittedProfiles,
        int launchSequence)
    {
        RaidId = raidId;
        SessionName = sessionName;
        AccessSecret = accessSecret;
        HostProfileId = hostProfileId;
        LaunchSequence = launchSequence;
        _admittedProfiles = CopyProfiles(admittedProfiles);
        _allowsCodeAdmission = false;
    }

    private RaidLaunchManifest(
        string raidId,
        string sessionName,
        string accessSecret,
        int launchSequence)
    {
        RaidId = raidId;
        SessionName = sessionName;
        AccessSecret = accessSecret;
        HostProfileId = default;
        LaunchSequence = launchSequence;
        _admittedProfiles = Array.Empty<ProfileId>();
        _allowsCodeAdmission = true;
    }

    /// <summary>Creates a manifest that authorizes valid unique profiles through a shared code secret.</summary>
    public static RaidLaunchManifest CreateCodeAdmission(
        string raidId,
        string sessionName,
        string accessSecret,
        int launchSequence)
    {
        return new RaidLaunchManifest(raidId, sessionName, accessSecret, launchSequence);
    }

    public bool Contains(ProfileId profileId)
    {
        if (_allowsCodeAdmission)
        {
            return profileId.IsValid;
        }

        if (_admittedProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < _admittedProfiles.Length; i++)
        {
            if (_admittedProfiles[i] == profileId)
            {
                return true;
            }
        }

        return false;
    }

    private static ProfileId[] CopyProfiles(IReadOnlyList<ProfileId> profiles)
    {
        if (profiles == null)
        {
            return null;
        }

        var copied = new ProfileId[profiles.Count];
        for (int i = 0; i < profiles.Count; i++)
        {
            copied[i] = profiles[i];
        }

        return copied;
    }
}

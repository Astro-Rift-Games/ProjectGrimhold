using System;
using System.Collections.Generic;

/// <summary>
/// Immutable cohort contract frozen by the Town queue before any peer leaves Town.
/// </summary>
public readonly struct RaidLaunchManifest
{
    public const int MaximumMembers = 4;

    private readonly ProfileId[] _admittedProfiles;

    public string RaidId { get; }
    public string SessionName { get; }
    public string AccessSecret { get; }
    public ProfileId HostProfileId { get; }
    public int LaunchSequence { get; }
    public IReadOnlyList<ProfileId> AdmittedProfiles => _admittedProfiles ?? Array.Empty<ProfileId>();

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RaidId) || string.IsNullOrWhiteSpace(SessionName) ||
                string.IsNullOrWhiteSpace(AccessSecret) || !HostProfileId.IsValid ||
                LaunchSequence <= 0 || _admittedProfiles == null ||
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
    }

    public bool Contains(ProfileId profileId)
    {
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

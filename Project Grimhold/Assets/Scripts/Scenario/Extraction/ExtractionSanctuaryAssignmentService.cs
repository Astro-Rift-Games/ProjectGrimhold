using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Fusion;
using UnityEngine;

/// <summary>
/// Runner-local coordinator that derives team assignments from replicated sanctuary reservations
/// and performs the Host-only selection that precedes the authoritative assignment write.
/// </summary>
[DisallowMultipleComponent]
public class ExtractionSanctuaryAssignmentService : MonoBehaviour
{
    private readonly List<EntityId> _sanctuaryIds = new();
    private readonly HashSet<SanctuaryAssignmentFailureReason> _reportedConfigurationFailures = new();

    private NetworkRunner _runner;
    private EntityRegistry _registry;
    private ulong _sessionSeed;
    private bool _hasSessionSeed;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public bool HasSessionSeed => _hasSessionSeed;

    /// <summary>Clears runner-local assignment state after the raid generation closes.</summary>
    public void ResetForRaidClosure()
    {
        _sanctuaryIds.Clear();
        _reportedConfigurationFailures.Clear();
        _sessionSeed = 0UL;
        _hasSessionSeed = false;
    }

    /// <summary>
    /// Binds this service to exactly one runner. Host initialization creates one local,
    /// non-replicated seed; Client initialization remains query-only.
    /// </summary>
    public bool Initialize(NetworkRunner runner, GameMode requestedMode)
    {
        if (_isInitialized)
        {
            return ReferenceEquals(_runner, runner);
        }

        if (runner == null)
        {
            return false;
        }

        EntityRegistry registry = runner.GetComponent<EntityRegistry>();
        if (registry == null)
        {
            return false;
        }

        ulong seed = 0UL;
        bool hasSeed = requestedMode == GameMode.Host;
        if (hasSeed && !TryGenerateSeed(out seed))
        {
            return false;
        }

        _runner = runner;
        _registry = registry;
        _sessionSeed = seed;
        _hasSessionSeed = hasSeed;
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// Registers one sanctuary identity in ascending order after confirming that the registry
    /// resolves the same expected instance. No sanctuary instance is retained by the service.
    /// </summary>
    public bool TryRegisterSanctuary(EntityId sanctuaryId, IExtractionSanctuary expectedSanctuary)
    {
        if (!_isInitialized || sanctuaryId.Value == 0 || expectedSanctuary == null ||
            expectedSanctuary.Id != sanctuaryId ||
            !_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary registered) ||
            !ReferenceEquals(registered, expectedSanctuary))
        {
            return false;
        }

        int index = FindInsertionIndex(sanctuaryId);
        if (index < _sanctuaryIds.Count && _sanctuaryIds[index] == sanctuaryId)
        {
            return true;
        }

        _sanctuaryIds.Insert(index, sanctuaryId);
        return true;
    }

    /// <summary>
    /// Removes an identity only while the registry still resolves the expected instance.
    /// Callers must invoke this before unregistering the registry capability.
    /// </summary>
    public bool TryUnregisterSanctuary(EntityId sanctuaryId, IExtractionSanctuary expectedSanctuary)
    {
        if (!_isInitialized || sanctuaryId.Value == 0 || expectedSanctuary == null ||
            !_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary registered) ||
            !ReferenceEquals(registered, expectedSanctuary))
        {
            return false;
        }

        int index = FindInsertionIndex(sanctuaryId);
        if (index >= _sanctuaryIds.Count || _sanctuaryIds[index] != sanctuaryId)
        {
            return false;
        }

        _sanctuaryIds.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Derives the unique sanctuary assigned to a team without requiring authority or mutating state.
    /// </summary>
    public virtual SanctuaryAssignmentResult TryGetAssignment(RaidTeamId teamId)
    {
        if (!_isInitialized)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.ServiceNotInitialized);
        }

        if (!teamId.IsValid)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.InvalidTeam);
        }

        EntityId foundId = default;
        for (int i = 0; i < _sanctuaryIds.Count; i++)
        {
            EntityId sanctuaryId = _sanctuaryIds[i];
            if (!_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary sanctuary) ||
                sanctuary == null || sanctuary.Id != sanctuaryId)
            {
                ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
                return SanctuaryAssignmentResult.Rejected(
                    teamId,
                    SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
            }

            if (!sanctuary.IsAssignedToTeam(teamId))
            {
                continue;
            }

            if (foundId.Value != 0)
            {
                ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.DuplicateExistingAssignment);
                return SanctuaryAssignmentResult.Rejected(
                    teamId,
                    SanctuaryAssignmentFailureReason.DuplicateExistingAssignment);
            }

            foundId = sanctuaryId;
        }

        return foundId.Value != 0
            ? SanctuaryAssignmentResult.Assigned(teamId, foundId, true)
            : SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.AssignmentNotFound);
    }

    /// <summary>
    /// Selects and assigns one sanctuary during Host simulation after validating current team progress.
    /// An existing unique assignment is returned idempotently before selection is evaluated.
    /// Multiple teams can share the same sanctuary per decision D-3.
    /// </summary>
    public SanctuaryAssignmentResult TryAssign(RaidTeamId teamId)
    {
        if (!_isInitialized)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.ServiceNotInitialized);
        }

        if (!teamId.IsValid)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.InvalidTeam);
        }

        if (_runner == null || !_runner.IsServer || !_hasSessionSeed)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.NoAuthority);
        }

        if (!_runner.IsSimulationUpdating)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.OutsideSimulation);
        }

        SanctuaryAssignmentResult existing = TryGetAssignment(teamId);
        if (existing.Success)
        {
            return existing;
        }

        if (existing.FailureReason != SanctuaryAssignmentFailureReason.AssignmentNotFound)
        {
            return existing;
        }

        if (!_registry.TryGetTeamExtractionProgress(out ITeamExtractionProgress teamProgress))
        {
            return SanctuaryAssignmentResult.Rejected(
                teamId,
                SanctuaryAssignmentFailureReason.ProgressReaderUnavailable);
        }

        if (!teamProgress.TryGetSnapshot(teamId, out ExtractionProgressSnapshot snapshot))
        {
            return SanctuaryAssignmentResult.Rejected(
                teamId,
                SanctuaryAssignmentFailureReason.InvalidProgressSnapshot);
        }

        if (!snapshot.IsQuotaComplete)
        {
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.QuotaIncomplete);
        }

        if (!snapshot.AssignmentRequested)
        {
            return SanctuaryAssignmentResult.Rejected(
                teamId,
                SanctuaryAssignmentFailureReason.AssignmentNotRequested);
        }

        if (_sanctuaryIds.Count == 0)
        {
            ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.NoSanctuariesConfigured);
            return SanctuaryAssignmentResult.Rejected(
                teamId,
                SanctuaryAssignmentFailureReason.NoSanctuariesConfigured);
        }

        for (int i = 0; i < _sanctuaryIds.Count; i++)
        {
            EntityId sanctuaryId = _sanctuaryIds[i];
            if (!_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary sanctuary) ||
                sanctuary == null || sanctuary.Id != sanctuaryId)
            {
                ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
                return SanctuaryAssignmentResult.Rejected(
                    teamId,
                    SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
            }
        }

        int selectedIndex = SanctuarySelectionPolicy.SelectIndex(
            _sessionSeed,
            _runner.Tick,
            teamId,
            _sanctuaryIds.Count);
        EntityId selectedId = _sanctuaryIds[selectedIndex];

        if (!_registry.TryGetExtractionSanctuary(selectedId, out IExtractionSanctuary selected) ||
            selected == null || !selected.TryAssignTeam(teamId) || !selected.IsAssignedToTeam(teamId))
        {
            ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.ReservationConflict);
            return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.ReservationConflict);
        }

        return SanctuaryAssignmentResult.Assigned(teamId, selectedId, false);
    }

    // --- Legacy / compatibility helpers for individual player callers ---

    public SanctuaryAssignmentResult TryGetAssignment(EntityId playerId)
    {
        if (TryResolvePlayerTeam(playerId, out RaidTeamId teamId))
        {
            SanctuaryAssignmentResult teamResult = TryGetAssignment(teamId);
            return teamResult.Success
                ? SanctuaryAssignmentResult.Assigned(playerId, teamResult.SanctuaryId, teamResult.IsExistingAssignment)
                : SanctuaryAssignmentResult.Rejected(playerId, teamResult.FailureReason);
        }

        if (RaidTeamId.TryCreate(playerId.Value, out RaidTeamId surrogate))
        {
            SanctuaryAssignmentResult teamResult = TryGetAssignment(surrogate);
            if (teamResult.Success)
            {
                return SanctuaryAssignmentResult.Assigned(playerId, teamResult.SanctuaryId, teamResult.IsExistingAssignment);
            }
        }

        return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.InvalidPlayer);
    }

    public SanctuaryAssignmentResult TryAssign(EntityId playerId)
    {
        if (TryResolvePlayerTeam(playerId, out RaidTeamId teamId))
        {
            SanctuaryAssignmentResult teamResult = TryAssign(teamId);
            return teamResult.Success
                ? SanctuaryAssignmentResult.Assigned(playerId, teamResult.SanctuaryId, teamResult.IsExistingAssignment)
                : SanctuaryAssignmentResult.Rejected(playerId, teamResult.FailureReason);
        }

        if (RaidTeamId.TryCreate(playerId.Value, out RaidTeamId surrogate))
        {
            SanctuaryAssignmentResult teamResult = TryAssign(surrogate);
            if (teamResult.Success)
            {
                return SanctuaryAssignmentResult.Assigned(playerId, teamResult.SanctuaryId, teamResult.IsExistingAssignment);
            }
        }

        return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.InvalidPlayer);
    }

    private bool TryResolvePlayerTeam(EntityId playerId, out RaidTeamId teamId)
    {
        teamId = default;
        if (_registry != null && _registry.TryGetCharacter(playerId, out ICharacter character) && character is Component c)
        {
            if (c.TryGetComponent(out RaidAvatarParticipantLink link) &&
                link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
                participant != null &&
                participant.RaidParticipantId.IsValid)
            {
                if (_runner != null)
                {
                    NetworkSpawnManager spawnManager = _runner.GetComponent<NetworkSpawnManager>();
                    if (spawnManager != null &&
                        spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
                        affiliations.TryGetTeam(participant.RaidParticipantId, out teamId))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryGenerateSeed(out ulong seed)
    {
        seed = 0UL;
        try
        {
            byte[] bytes = new byte[sizeof(ulong)];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            seed = BitConverter.ToUInt64(bytes, 0);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private int FindInsertionIndex(EntityId sanctuaryId)
    {
        int low = 0;
        int high = _sanctuaryIds.Count;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (_sanctuaryIds[middle].Value < sanctuaryId.Value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private void ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason reason)
    {
        if (_reportedConfigurationFailures.Add(reason))
        {
            Debug.LogError($"{nameof(ExtractionSanctuaryAssignmentService)} configuration failure: {reason}.", this);
        }
    }

    private void OnDestroy()
    {
        ResetForRaidClosure();
        _runner = null;
        _registry = null;
        _sessionSeed = 0UL;
        _hasSessionSeed = false;
        _isInitialized = false;
    }
}

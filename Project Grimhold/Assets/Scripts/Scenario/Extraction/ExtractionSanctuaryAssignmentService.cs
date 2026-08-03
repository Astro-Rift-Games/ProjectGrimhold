using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Fusion;
using UnityEngine;

/// <summary>
/// Runner-local coordinator that derives player assignments from replicated sanctuary owners
/// and performs the Host-only selection that precedes the authoritative reservation write.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExtractionSanctuaryAssignmentService : MonoBehaviour
{
    private readonly List<EntityId> _sanctuaryIds = new();
    private readonly List<EntityId> _freeSanctuaryIds = new();
    private readonly HashSet<SanctuaryAssignmentFailureReason> _reportedConfigurationFailures = new();

    private NetworkRunner _runner;
    private EntityRegistry _registry;
    private ulong _sessionSeed;
    private bool _hasSessionSeed;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public bool HasSessionSeed => _hasSessionSeed;

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
    /// Derives the unique sanctuary owned by a player without requiring authority or mutating state.
    /// </summary>
    public SanctuaryAssignmentResult TryGetAssignment(EntityId playerId)
    {
        if (!_isInitialized)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.ServiceNotInitialized);
        }

        if (playerId.Value == 0)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.InvalidPlayer);
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
                    playerId,
                    SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
            }

            if (!sanctuary.IsOwnedBy(playerId))
            {
                continue;
            }

            if (foundId.Value != 0)
            {
                ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.DuplicateExistingAssignment);
                return SanctuaryAssignmentResult.Rejected(
                    playerId,
                    SanctuaryAssignmentFailureReason.DuplicateExistingAssignment);
            }

            foundId = sanctuaryId;
        }

        return foundId.Value != 0
            ? SanctuaryAssignmentResult.Assigned(playerId, foundId, true)
            : SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.AssignmentNotFound);
    }

    /// <summary>
    /// Selects and reserves one sanctuary during Host simulation after validating current player state.
    /// An existing unique assignment is returned before mutable eligibility is evaluated.
    /// </summary>
    public SanctuaryAssignmentResult TryAssign(EntityId playerId)
    {
        if (!_isInitialized)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.ServiceNotInitialized);
        }

        if (playerId.Value == 0)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.InvalidPlayer);
        }

        if (_runner == null || !_runner.IsServer || !_hasSessionSeed)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.NoAuthority);
        }

        if (!_runner.IsSimulationUpdating)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.OutsideSimulation);
        }

        SanctuaryAssignmentResult existing = TryGetAssignment(playerId);
        if (existing.Success)
        {
            return existing;
        }

        if (existing.FailureReason != SanctuaryAssignmentFailureReason.AssignmentNotFound)
        {
            return existing;
        }

        if (!_registry.TryGetExtractionProgressReader(playerId, out IExtractionProgressReader reader))
        {
            return SanctuaryAssignmentResult.Rejected(
                playerId,
                SanctuaryAssignmentFailureReason.ProgressReaderUnavailable);
        }

        if (!reader.TryGetSnapshot(out ExtractionProgressSnapshot snapshot))
        {
            return SanctuaryAssignmentResult.Rejected(
                playerId,
                SanctuaryAssignmentFailureReason.InvalidProgressSnapshot);
        }

        if (!snapshot.IsQuotaComplete)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.QuotaIncomplete);
        }

        if (!snapshot.AssignmentRequested)
        {
            return SanctuaryAssignmentResult.Rejected(
                playerId,
                SanctuaryAssignmentFailureReason.AssignmentNotRequested);
        }

        if (!_registry.TryGetCharacter(playerId, out ICharacter character) || character == null || !character.IsAlive ||
            !_registry.TryGetExtractionParticipant(playerId, out IExtractionParticipant participant) ||
            participant == null || participant.State == ExtractionState.Extracted)
        {
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.PlayerUnavailable);
        }

        if (_sanctuaryIds.Count == 0)
        {
            ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.NoSanctuariesConfigured);
            return SanctuaryAssignmentResult.Rejected(
                playerId,
                SanctuaryAssignmentFailureReason.NoSanctuariesConfigured);
        }

        _freeSanctuaryIds.Clear();
        for (int i = 0; i < _sanctuaryIds.Count; i++)
        {
            EntityId sanctuaryId = _sanctuaryIds[i];
            if (!_registry.TryGetExtractionSanctuary(sanctuaryId, out IExtractionSanctuary sanctuary) ||
                sanctuary == null || sanctuary.Id != sanctuaryId)
            {
                ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
                return SanctuaryAssignmentResult.Rejected(
                    playerId,
                    SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent);
            }

            if (!sanctuary.IsReserved)
            {
                _freeSanctuaryIds.Add(sanctuaryId);
            }
        }

        if (_freeSanctuaryIds.Count == 0)
        {
            ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.NoFreeSanctuary);
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.NoFreeSanctuary);
        }

        int selectedIndex = SanctuarySelectionPolicy.SelectIndex(
            _sessionSeed,
            _runner.Tick,
            playerId,
            _freeSanctuaryIds.Count);
        EntityId selectedId = _freeSanctuaryIds[selectedIndex];
        if (!_registry.TryGetExtractionSanctuary(selectedId, out IExtractionSanctuary selected) ||
            selected == null || !selected.TryReserve(playerId) || !selected.IsOwnedBy(playerId))
        {
            ReportConfigurationFailureOnce(SanctuaryAssignmentFailureReason.ReservationConflict);
            return SanctuaryAssignmentResult.Rejected(playerId, SanctuaryAssignmentFailureReason.ReservationConflict);
        }

        return SanctuaryAssignmentResult.Assigned(playerId, selectedId, false);
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
        _sanctuaryIds.Clear();
        _freeSanctuaryIds.Clear();
        _reportedConfigurationFailures.Clear();
        _runner = null;
        _registry = null;
        _sessionSeed = 0UL;
        _hasSessionSeed = false;
        _isInitialized = false;
    }
}

using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns one peer's local spectator target selection and camera binding.
/// It observes replicated participant state without changing gameplay authority,
/// PlayerObject mappings, or network simulation.
/// </summary>
public sealed class LocalRaidSpectatorController
{
    private readonly NetworkRunner _runner;
    private readonly NetworkRaidParticipant _localParticipant;
    private readonly List<SpectatorTarget> _targets = new(RaidSessionRules.MaxParticipants);
    private readonly List<NetworkObject> _networkObjects = new();

    private NetworkRaidParticipant _currentParticipant;
    private Transform _currentTransform;
    private string _currentProfileId;
    private bool _fallbackReported;

    public bool IsActive { get; private set; }
    public bool HasTarget => _currentTransform != null;
    public string CurrentProfileId => _currentProfileId ?? string.Empty;

    public LocalRaidSpectatorController(
        NetworkRunner runner,
        NetworkRaidParticipant localParticipant)
    {
        _runner = runner;
        _localParticipant = localParticipant;
    }

    /// <summary>Enters local spectator presentation and selects the first ordered target.</summary>
    public bool Enter()
    {
        if (IsActive)
        {
            return false;
        }

        IsActive = true;
        Debug.Log("[RAID-SPECTATOR] Spectator entered.", _localParticipant);
        RebuildTargets();
        return SelectTarget(_targets.Count > 0 ? 0 : -1);
    }

    /// <summary>Selects the previous valid target in ordinal ProfileId order.</summary>
    public bool SelectPrevious()
    {
        if (!IsActive)
        {
            return false;
        }

        string previousProfileId = _currentProfileId;
        RebuildTargets();
        if (_targets.Count == 0)
        {
            return SelectTarget(-1);
        }

        int targetIndex = RaidSpectatorSelectionPolicy.FindRelative(
            CreateOrderedProfileView(),
            previousProfileId,
            -1);
        return SelectTarget(targetIndex);
    }

    /// <summary>Selects the next valid target in ordinal ProfileId order.</summary>
    public bool SelectNext()
    {
        if (!IsActive)
        {
            return false;
        }

        string previousProfileId = _currentProfileId;
        RebuildTargets();
        if (_targets.Count == 0)
        {
            return SelectTarget(-1);
        }

        int targetIndex = RaidSpectatorSelectionPolicy.FindRelative(
            CreateOrderedProfileView(),
            previousProfileId,
            1);
        return SelectTarget(targetIndex);
    }

    /// <summary>
    /// Revalidates the current replicated target. When it becomes invalid, selects the
    /// first valid ProfileId greater than the previous identity and wraps when necessary.
    /// </summary>
    public bool RefreshCurrentTarget()
    {
        if (!IsActive || IsCurrentTargetValid())
        {
            return false;
        }

        string invalidatedProfileId = _currentProfileId;
        Debug.Log(
            $"[RAID-SPECTATOR] Target invalidated. ProfileId={invalidatedProfileId}.",
            _localParticipant);
        RebuildTargets();
        if (_targets.Count == 0)
        {
            return SelectTarget(-1);
        }

        int nextIndex = RaidSpectatorSelectionPolicy.FindNextAfterInvalidated(
            CreateOrderedProfileView(),
            invalidatedProfileId);

        return SelectTarget(nextIndex);
    }

    /// <summary>Clears camera ownership and all runner-scoped local caches.</summary>
    public void Cleanup()
    {
        if (_currentTransform != null && LocalCameraController.Instance != null)
        {
            LocalCameraController.Instance.ClearTarget(_currentTransform);
        }

        if (IsActive)
        {
            Debug.Log("[RAID-SPECTATOR] Spectator cleanup.", _localParticipant);
        }

        IsActive = false;
        _currentParticipant = null;
        _currentTransform = null;
        _currentProfileId = null;
        _targets.Clear();
        _networkObjects.Clear();
        _fallbackReported = false;
    }

    private void RebuildTargets()
    {
        _targets.Clear();
        if (_runner == null || !_runner.IsRunning || _localParticipant == null)
        {
            return;
        }

        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            NetworkObject playerObject = _runner.GetPlayerObject(player);
            if (playerObject != null &&
                playerObject.TryGetBehaviour(out NetworkRaidParticipant participant))
            {
                TryAddTarget(participant);
            }
        }

        if (_targets.Count == 0)
        {
            _networkObjects.Clear();
            _runner.GetAllNetworkObjects(_networkObjects);
            for (int index = 0; index < _networkObjects.Count; index++)
            {
                NetworkObject networkObject = _networkObjects[index];
                if (networkObject != null &&
                    networkObject.TryGetBehaviour(out NetworkRaidParticipant participant))
                {
                    TryAddTarget(participant);
                }
            }

            if (!_fallbackReported)
            {
                _fallbackReported = true;
                Debug.Log(
                    "[RAID-SPECTATOR] PlayerObject enumeration produced no valid targets; " +
                    "using a bounded replicated-object fallback.",
                    _localParticipant);
            }
        }

        _targets.Sort(SpectatorTargetComparer.Instance);
    }

    private void TryAddTarget(NetworkRaidParticipant participant)
    {
        if (!TryResolveValidTarget(participant, out string profileId, out Transform targetTransform))
        {
            return;
        }

        for (int index = 0; index < _targets.Count; index++)
        {
            if (ReferenceEquals(_targets[index].Participant, participant))
            {
                return;
            }
        }

        _targets.Add(new SpectatorTarget(profileId, participant, targetTransform));
    }

    private bool TryResolveValidTarget(
        NetworkRaidParticipant participant,
        out string profileId,
        out Transform targetTransform)
    {
        profileId = null;
        targetTransform = null;
        if (participant == null || participant == _localParticipant ||
            participant.State != RaidParticipantState.Raiding)
        {
            return false;
        }

        profileId = participant.ProfileId.ToString();
        string localGenerationId = _localParticipant.RaidGenerationId.ToString();
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(localGenerationId) ||
            !string.Equals(
                participant.RaidGenerationId.ToString(),
                localGenerationId,
                StringComparison.Ordinal) ||
            !participant.TryResolveCurrentAvatar(out NetworkObject avatar) ||
            avatar == null || !avatar.IsValid)
        {
            return false;
        }

        targetTransform = avatar.transform;
        return targetTransform != null;
    }

    private bool IsCurrentTargetValid()
    {
        if (_currentParticipant == null || _currentTransform == null ||
            !TryResolveValidTarget(
                _currentParticipant,
                out string profileId,
                out Transform targetTransform))
        {
            return false;
        }

        return string.Equals(profileId, _currentProfileId, StringComparison.Ordinal) &&
            targetTransform == _currentTransform;
    }

    private IReadOnlyList<string> CreateOrderedProfileView()
    {
        var profileIds = new string[_targets.Count];
        for (int index = 0; index < _targets.Count; index++)
        {
            profileIds[index] = _targets[index].ProfileId;
        }
        return profileIds;
    }

    private bool SelectTarget(int index)
    {
        Transform previousTransform = _currentTransform;
        string previousProfileId = _currentProfileId;
        if (previousTransform != null && LocalCameraController.Instance != null)
        {
            LocalCameraController.Instance.ClearTarget(previousTransform);
        }

        if (index < 0 || index >= _targets.Count)
        {
            _currentParticipant = null;
            _currentTransform = null;
            _currentProfileId = null;
            if (!string.IsNullOrEmpty(previousProfileId))
            {
                Debug.Log("[RAID-SPECTATOR] No valid targets remain.", _localParticipant);
            }
            return previousTransform != null || !string.IsNullOrEmpty(previousProfileId);
        }

        SpectatorTarget target = _targets[index];
        _currentParticipant = target.Participant;
        _currentTransform = target.Transform;
        _currentProfileId = target.ProfileId;
        if (LocalCameraController.Instance != null)
        {
            LocalCameraController.Instance.SetTarget(_currentTransform);
        }

        Debug.Log(
            $"[RAID-SPECTATOR] Selected target ProfileId={_currentProfileId}.",
            _localParticipant);
        return previousTransform != _currentTransform ||
            !string.Equals(previousProfileId, _currentProfileId, StringComparison.Ordinal);
    }

    private readonly struct SpectatorTarget
    {
        public string ProfileId { get; }
        public NetworkRaidParticipant Participant { get; }
        public Transform Transform { get; }

        public SpectatorTarget(
            string profileId,
            NetworkRaidParticipant participant,
            Transform transform)
        {
            ProfileId = profileId;
            Participant = participant;
            Transform = transform;
        }
    }

    private sealed class SpectatorTargetComparer : IComparer<SpectatorTarget>
    {
        public static readonly SpectatorTargetComparer Instance = new();

        public int Compare(SpectatorTarget left, SpectatorTarget right) =>
            string.CompareOrdinal(left.ProfileId, right.ProfileId);
    }
}

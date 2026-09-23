using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned tracker for combat contributions.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class CombatContributionTracker : NetworkBehaviour, ICombatContributionTracker
{
    [SerializeField, Min(0f), Tooltip("Duration in seconds that a contribution remains valid for assist credit.")]
    private float _assistWindowSeconds = 10f;

    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    private NetworkArray<int> _contributionTicks { get; }

    private int _assistWindowTicks;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public override void Spawned()
    {
        _assistWindowTicks = Mathf.CeilToInt(_assistWindowSeconds / Runner.DeltaTime);

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
            {
                _contributionTicks.Set(i, 0);
            }
        }

        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        if (_registry == null)
        {
            Debug.LogError($"{nameof(CombatContributionTracker)} requires a runner registry.", this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterCombatContributionTracker(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError(
                $"{nameof(CombatContributionTracker)} could not register entity {_registeredId}.",
                this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterTracker();
    }

    public void TryRecordContribution(RaidParticipantId contributor, int simulationTick)
    {
        if (!HasStateAuthority || !contributor.IsValid)
        {
            return;
        }

        int index = contributor.Value - 1;
        _contributionTicks.Set(index, simulationTick);
    }

    public void GetValidContributors(int currentTick, HashSet<RaidParticipantId> contributors)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
        {
            int tick = _contributionTicks.Get(i);
            if (tick > 0 && (currentTick - tick) <= _assistWindowTicks)
            {
                if (RaidParticipantId.TryCreate(i + 1, out RaidParticipantId id))
                {
                    contributors.Add(id);
                }
            }
        }
    }

    private void UnregisterTracker()
    {
        if (!_isRegistered)
        {
            return;
        }

        _registry?.TryUnregisterCombatContributionTracker(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterTracker();
    }
}

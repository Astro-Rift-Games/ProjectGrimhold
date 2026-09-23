using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned one-shot Kill Experience reward for one networked target.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class KillExperienceSource : NetworkBehaviour, IKillExperienceSource
{
    [SerializeField, Min(0)]
    private long _killExperience;

    [Networked]
    public NetworkBool IsGranted { get; private set; }

    [Networked]
    public NetworkBool IsAssistGranted { get; private set; }

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public long KillExperience => _killExperience;

    public bool IsAvailable => _killExperience > 0 && !IsGranted;
    bool IKillExperienceSource.IsAssistGranted => IsAssistGranted;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public override void Spawned()
    {
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsGranted = false;
            IsAssistGranted = false;
        }

        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        if (_registry == null || _killExperience < 0)
        {
            Debug.LogError(
                $"{nameof(KillExperienceSource)} requires a runner registry and non-negative Kill Experience.",
                this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterKillExperienceSource(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError(
                $"{nameof(KillExperienceSource)} could not register entity {_registeredId}.",
                this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterSource();
    }

    public bool TryGrantTo(PlayerExpeditionExperienceLedger ledger)
    {
        if (!HasStateAuthority || !IsAvailable || ledger == null)
        {
            return false;
        }

        if (!ledger.TryRegisterNormalReward(
                ExpeditionExperienceSource.PveKill,
                _killExperience,
                out _))
        {
            return false;
        }

        IsGranted = true;
        return true;
    }

    public bool TryGrantAssistTo(PlayerExpeditionExperienceLedger ledger)
    {
        if (!HasStateAuthority || IsAssistGranted || ledger == null)
        {
            return false;
        }

        long assistExperience = _killExperience / 2;
        if (assistExperience <= 0)
        {
            return false;
        }

        if (!ledger.TryRegisterNormalReward(
                ExpeditionExperienceSource.PveAssist,
                assistExperience,
                out _))
        {
            return false;
        }

        return true;
    }

    public void MarkAssistGranted()
    {
        if (HasStateAuthority)
        {
            IsAssistGranted = true;
        }
    }

    private void UnregisterSource()
    {
        if (!_isRegistered)
        {
            return;
        }

        _registry?.TryUnregisterKillExperienceSource(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterSource();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_killExperience < 0)
        {
            _killExperience = 0;
        }
    }
#endif
}

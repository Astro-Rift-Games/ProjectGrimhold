using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns the single weapon equipped by a Raid avatar and rebuilds its attack strategy
/// from the deterministic loot catalog index replicated by Fusion.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-8)]
public sealed class PlayerWeaponEquipmentNetworkController : NetworkBehaviour
{
    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private PlayerLootReceiver _lootReceiver;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private PlayerCombatNetworkController _combatController;

    [SerializeField]
    private MeleeAttack _meleeAttack;

    [SerializeField]
    private RangedAttack _rangedAttack;

    [SerializeField]
    private FusionProjectileSpawner _projectileSpawner;

    [Networked]
    private int EquippedCatalogIndexPlusOne { get; set; }

    private ICharacter _character;
    private NetworkMatchController _matchController;
    private bool _hasPendingAuthorityRequest;
    private int _pendingCatalogIndex;
    private int _pendingRequestSequence;
    private int _nextRequestSequence;
    private int _appliedCatalogIndexPlusOne = int.MinValue;
    private readonly Queue<WeaponEquipResult> _pendingPresentationResults = new();

    public bool HasEquippedWeapon => EquippedCatalogIndexPlusOne > 0;
    public bool HasRequestInFlight { get; private set; }

    public event Action<WeaponEquipResult> EquipRequestResolved;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        if (!ValidateDependencies())
        {
            return;
        }

        _matchController = Runner.GetComponent<NetworkMatchController>();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            EquippedCatalogIndexPlusOne = 0;
            _combatController.TryClearActiveAttack();
        }

        if (HasStateAuthority)
        {
            ApplyReplicatedWeapon();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_appliedCatalogIndexPlusOne != EquippedCatalogIndexPlusOne)
        {
            ApplyReplicatedWeapon();
        }

        if (!_hasPendingAuthorityRequest)
        {
            return;
        }

        int catalogIndex = _pendingCatalogIndex;
        int requestSequence = _pendingRequestSequence;
        _hasPendingAuthorityRequest = false;

        WeaponEquipResult result = TryEquipAuthority(catalogIndex);
        RPC_ConfirmEquip(requestSequence, (int)result);
    }

    public override void Render()
    {
        while (_pendingPresentationResults.Count > 0)
        {
            EquipRequestResolved?.Invoke(_pendingPresentationResults.Dequeue());
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _pendingPresentationResults.Clear();
        HasRequestInFlight = false;
        _hasPendingAuthorityRequest = false;
    }

    public bool TryRequestEquip(LootId lootId)
    {
        if (!HasInputAuthority || HasRequestInFlight || HasEquippedWeapon ||
            _lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex))
        {
            return false;
        }

        int requestSequence = ++_nextRequestSequence;
        RpcInvokeInfo invokeInfo = RPC_RequestEquip(catalogIndex, requestSequence);
        if (!WasAccepted(invokeInfo, HasStateAuthority))
        {
            return false;
        }

        HasRequestInFlight = true;
        return true;
    }

    public bool TryGetEquippedLoot(out LootEntry entry)
    {
        entry = default;
        int catalogIndex = EquippedCatalogIndexPlusOne - 1;
        if (catalogIndex < 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
        {
            return false;
        }

        entry = new LootEntry(definition.LootId, 1);
        return true;
    }

    /// <summary>Resolves the replicated equipped identity for presentation consumers.</summary>
    public bool TryGetEquippedDefinition(out LootDefinition definition)
    {
        definition = null;
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        int catalogIndex = EquippedCatalogIndexPlusOne - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) &&
            definition != null;
    }

    public bool TryMatchesExactEquippedLoot(LootEntry? expected, out string error)
    {
        error = null;
        bool hasCurrent = TryGetEquippedLoot(out LootEntry current);
        if (!expected.HasValue)
        {
            if (!hasCurrent)
            {
                return true;
            }

            error = "An equipped weapon appeared after the snapshot was captured.";
            return false;
        }

        if (hasCurrent && current == expected.Value)
        {
            return true;
        }

        error = "Equipped weapon no longer matches the expected snapshot.";
        return false;
    }

    /// <summary>
    /// Clears the equipped unit after an owning expedition flow has atomically verified
    /// the expected identity. Intended for corpse and extraction ownership transfer only.
    /// </summary>
    public bool TryClearExactEquippedLoot(in LootEntry expected, out string error)
    {
        error = null;
        if (!HasStateAuthority)
        {
            error = "Equipment can only be cleared by State Authority.";
            return false;
        }

        if (!TryGetEquippedLoot(out LootEntry current) || !current.Equals(expected))
        {
            error = "Equipped weapon no longer matches the expected snapshot.";
            return false;
        }

        EquippedCatalogIndexPlusOne = 0;
        _appliedCatalogIndexPlusOne = 0;
        if (!_combatController.TryClearActiveAttack())
        {
            error = "Combat strategy could not be cleared.";
            return false;
        }

        return true;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority,
        InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestEquip(
        int catalogIndex,
        int requestSequence,
        RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority)
        {
            return default;
        }

        if (_hasPendingAuthorityRequest)
        {
            RPC_ConfirmEquip(requestSequence, (int)WeaponEquipResult.InvalidRequest);
            return default;
        }

        _pendingCatalogIndex = catalogIndex;
        _pendingRequestSequence = requestSequence;
        _hasPendingAuthorityRequest = true;
        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ConfirmEquip(int requestSequence, int resultValue)
    {
        if (requestSequence != _nextRequestSequence)
        {
            return;
        }

        HasRequestInFlight = false;
        _pendingPresentationResults.Enqueue((WeaponEquipResult)resultValue);
    }

    private WeaponEquipResult TryEquipAuthority(int catalogIndex)
    {
        if (!ValidateDependencies())
        {
            return WeaponEquipResult.DependenciesUnavailable;
        }

        if (_character == null || !_character.IsAlive ||
            (_matchController != null &&
             _matchController.Phase != NetworkMatchController.MatchPhase.InProgress))
        {
            return WeaponEquipResult.PlayerUnavailable;
        }

        if (HasEquippedWeapon)
        {
            return WeaponEquipResult.WeaponAlreadyEquipped;
        }

        if (!TryResolveValidWeapon(catalogIndex, out LootDefinition definition, out AttackConfig attackConfig) ||
            !TryConfigureStrategy(attackConfig, out MonoBehaviour attackSource))
        {
            return WeaponEquipResult.InvalidWeapon;
        }

        LootTransferRequest extraction = new LootTransferRequest(
            _lootReceiver.Id,
            _lootReceiver.Id,
            definition.LootId,
            1,
            Runner.Tick);
        if (_lootReceiver.ValidateExtraction(extraction) != LootTransferFailureReason.None)
        {
            return WeaponEquipResult.WeaponNotOwned;
        }

        _lootReceiver.CommitExtraction(extraction);
        EquippedCatalogIndexPlusOne = catalogIndex + 1;
        _appliedCatalogIndexPlusOne = EquippedCatalogIndexPlusOne;
        if (!_combatController.TrySetActiveAttack(attackSource))
        {
            throw new InvalidOperationException("Validated equipment could not bind its combat strategy.");
        }

        return WeaponEquipResult.Succeeded;
    }

    private void ApplyReplicatedWeapon()
    {
        _appliedCatalogIndexPlusOne = EquippedCatalogIndexPlusOne;
        if (!HasEquippedWeapon)
        {
            _combatController.TryClearActiveAttack();
            return;
        }

        int catalogIndex = EquippedCatalogIndexPlusOne - 1;
        if (!TryResolveValidWeapon(catalogIndex, out _, out AttackConfig attackConfig) ||
            !TryConfigureStrategy(attackConfig, out MonoBehaviour attackSource) ||
            !_combatController.TrySetActiveAttack(attackSource))
        {
            Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} could not rebuild equipped weapon index {catalogIndex}.", this);
        }
    }

    private bool TryResolveValidWeapon(
        int catalogIndex,
        out LootDefinition definition,
        out AttackConfig attackConfig)
    {
        definition = null;
        attackConfig = null;
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(catalogIndex, out definition) ||
            definition.Category != LootCategory.Weapon || definition.WeaponDefinition == null ||
            !definition.WeaponDefinition.TryValidate(out _))
        {
            return false;
        }

        attackConfig = definition.WeaponDefinition.PrimaryAttack;
        return true;
    }

    private bool TryConfigureStrategy(AttackConfig attackConfig, out MonoBehaviour attackSource)
    {
        attackSource = null;
        if (attackConfig is MeleeAttackConfig meleeConfig && _meleeAttack != null &&
            _meleeAttack.TryConfigure(meleeConfig))
        {
            attackSource = _meleeAttack;
            return true;
        }

        if (attackConfig is RangedAttackConfig rangedConfig && _rangedAttack != null &&
            _projectileSpawner != null && _projectileSpawner.TryConfigure(rangedConfig) &&
            _rangedAttack.TryConfigure(rangedConfig))
        {
            attackSource = _rangedAttack;
            return true;
        }

        return false;
    }

    private void CacheDependencies()
    {
        _character = _characterSource as ICharacter;
        _character ??= GetComponent<ICharacter>();
    }

    private bool ValidateDependencies()
    {
        if (_lootCatalog != null && _lootReceiver != null && _character != null &&
            _combatController != null && _meleeAttack != null && _rangedAttack != null &&
            _projectileSpawner != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} has missing dependencies.", this);
        return false;
    }

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;
}

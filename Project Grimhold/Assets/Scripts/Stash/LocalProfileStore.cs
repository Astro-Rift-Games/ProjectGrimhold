using System;
using System.Collections.Generic;
using UnityEngine;
using static ProfileInventoryRules;

/// <summary>
/// Atomic application-level operations over the local profile aggregate.
/// </summary>
public sealed class LocalProfileStore
{
    private readonly object _sync = new();
    private readonly ILocalProfileRepository _repository;
    private readonly ProfileId _profileId;
    private readonly LootDefinitionCatalog _lootCatalog;
    private readonly LootId _recoveryWeaponLootId;
    private readonly MissionDefinitionCatalog _missionCatalog;
    private readonly AbilityDefinitionCatalog _abilityCatalog;

    public event Action<ProfileId> ProfileCommitted;

    public ProfileId ProfileId => _profileId;
    public LocalProfilePersistenceStatus Status => _repository.Status;
    public string LastError => _repository.LastError;
    public bool IsAvailable => Status == LocalProfilePersistenceStatus.Ready || Status == LocalProfilePersistenceStatus.RecoveredFromBackup;
    public MissionDefinitionCatalog MissionCatalog => _missionCatalog;
    public LootDefinitionCatalog LootCatalog => _lootCatalog;
    public const int DefaultStashCapacity = 60;
    public int StashCapacity { get; }

    public LocalProfileStore(
        ILocalProfileRepository repository,
        ProfileId profileId,
        LootDefinitionCatalog lootCatalog = null,
        LootId recoveryWeaponLootId = default,
        MissionDefinitionCatalog missionCatalog = null,
        AbilityDefinitionCatalog abilityCatalog = null,
        int stashCapacity = DefaultStashCapacity)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        if (stashCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(stashCapacity));
        StashCapacity = stashCapacity;
        _profileId = profileId;
        _lootCatalog = lootCatalog;
        _recoveryWeaponLootId = recoveryWeaponLootId;
        _missionCatalog = missionCatalog;
        _abilityCatalog = abilityCatalog;
    }

    public IReadOnlyList<StashItem> GetStash() =>
        _repository.Snapshot != null ? _repository.Snapshot.Stash : Array.Empty<StashItem>();

    public IReadOnlyList<StashItem> GetLoadout() =>
        _repository.Snapshot != null ? _repository.Snapshot.Loadout : Array.Empty<StashItem>();
    public PreparedEquipmentLoadout GetPreparedEquipment() =>
        _repository.Snapshot != null ? _repository.Snapshot.PreparedEquipment : default;
    public PendingLoadoutReservation PendingReservation => 
        _repository.Snapshot?.PendingReservation;
    public PendingExtractionCommit PendingExtractionCommit => 
        _repository.Snapshot?.PendingExtractionCommit;
    public long GetCurrency() =>
        _repository.Snapshot != null ? _repository.Snapshot.Currency : LocalProfileSnapshot.InitialCurrency;
    public int GetLevel() =>
        _repository.Snapshot != null ? _repository.Snapshot.Level : ExperienceCurve.InitialLevel;
    public long GetCurrentExperience() =>
        _repository.Snapshot != null ? _repository.Snapshot.CurrentExperience : 0L;
    public int GetLastAppliedProgressionResultSequence() =>
        _repository.Snapshot != null ? _repository.Snapshot.LastAppliedProgressionResultSequence : 0;
    public int RemoteRevision =>
        _repository.Snapshot != null ? _repository.Snapshot.RemoteRevision : 0;

    public IReadOnlyList<MissionInstanceState> GetActiveMissions() =>
        _repository.Snapshot != null ? _repository.Snapshot.ActiveMissions : Array.Empty<MissionInstanceState>();

    public PreparedAbilityLoadout GetPreparedAbilities()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            return IsAvailable && snapshot != null && snapshot.ProfileId == _profileId
                ? snapshot.PreparedAbilities
                : default;
        }
    }

    public QuickSlotBindings GetQuickSlots()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            return IsAvailable && snapshot != null && snapshot.ProfileId == _profileId
                ? snapshot.QuickSlots
                : default;
        }
    }

    /// <summary>
    /// Commits Quick Slot bindings. Bindings only reference loot definitions, so this never
    /// touches Stash, Loadout, Origin or amounts and, unlike equipment, is not blocked by
    /// Ready or a pending reservation. Whether the definition is present in the Inventory the
    /// player is looking at is checked by the caller, because the Loadout is reserved or
    /// empty while in Raid.
    /// </summary>
    public StashOperationResult TrySetQuickSlots(QuickSlotBindings bindings)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return StashOperationResult.PersistenceFailed;
            }

            for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
            {
                if (bindings.IsAssigned(i) &&
                    (_lootCatalog == null ||
                     !_lootCatalog.TryGet(bindings.Get(i).Value, out LootDefinition definition) ||
                     !definition.QuickSlotCompatible))
                {
                    return StashOperationResult.InvalidInventory;
                }
            }

            if (current.QuickSlots == bindings)
            {
                return StashOperationResult.Success;
            }

            LocalProfileSnapshot next = current.Clone();
            next.QuickSlots = bindings;
            return Commit(next);
        }
    }

    /// <summary>
    /// Captures the confirmed attributes and prepared abilities for Raid admission in one
    /// aggregate read. The unlocked repertoire remains local and is used only to revalidate the
    /// two transported identities before they cross the Town-to-Raid boundary.
    /// </summary>
    public bool TryGetRaidAdmissionAbilitySnapshot(
        out CharacterAttributeState characterAttributes,
        out PreparedAbilityLoadout preparedAbilities,
        out string error)
    {
        lock (_sync)
        {
            characterAttributes = default;
            preparedAbilities = default;
            error = null;

            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                error = "The local profile is unavailable for Raid admission.";
                return false;
            }

            if (!PreparedAbilityLoadout.TryValidate(
                    snapshot.PreparedAbilities,
                    snapshot.UnlockedAbilities,
                    snapshot.CharacterAttributes,
                    _abilityCatalog,
                    out error))
            {
                return false;
            }

            characterAttributes = snapshot.CharacterAttributes;
            preparedAbilities = snapshot.PreparedAbilities;
            return true;
        }
    }

    public IReadOnlyList<AbilityId> GetUnlockedAbilities()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return Array.Empty<AbilityId>();
            }

            var result = new AbilityId[snapshot.UnlockedAbilities.Count];
            snapshot.UnlockedAbilities.CopyTo(result);
            return result;
        }
    }

    public bool IsAbilityUnlocked(AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return false;
        }

        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return false;
            }

            return snapshot.UnlockedAbilities.Contains(abilityId);
        }
    }

    public AbilityUnlockResult TryUnlockAbility(AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return AbilityUnlockResult.InvalidAbility;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityUnlockResult.ProfileUnavailable;
            }

            AbilityUnlockResult validation = ValidateAbilityUnlock(current, abilityId);
            if (validation != AbilityUnlockResult.Success)
            {
                return validation;
            }

            LocalProfileSnapshot next = current.Clone();
            next.UnlockedAbilities.Add(abilityId);
            return Commit(next) == StashOperationResult.Success
                ? AbilityUnlockResult.Success
                : AbilityUnlockResult.PersistenceFailed;
        }
    }

    /// <summary>
    /// Debits <paramref name="price"/> Currency and unlocks the ability in one transaction.
    /// Attribute requirements do not condition acquisition; an owned ability cannot be bought again.
    /// </summary>
    public AbilityPurchaseResult TryPurchaseAbility(AbilityId abilityId, long price)
    {
        if (!abilityId.IsValid)
        {
            return AbilityPurchaseResult.InvalidAbility;
        }

        if (price <= 0)
        {
            return AbilityPurchaseResult.InvalidPrice;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPurchaseResult.ProfileUnavailable;
            }

            switch (ValidateAbilityUnlock(current, abilityId))
            {
                case AbilityUnlockResult.UnknownAbility:
                    return AbilityPurchaseResult.UnknownAbility;
                case AbilityUnlockResult.AlreadyUnlocked:
                    return AbilityPurchaseResult.AlreadyUnlocked;
            }

            if (current.Currency < price)
            {
                return AbilityPurchaseResult.InsufficientCurrency;
            }

            LocalProfileSnapshot next = current.Clone();
            next.Currency -= price;
            next.UnlockedAbilities.Add(abilityId);
            return Commit(next) == StashOperationResult.Success
                ? AbilityPurchaseResult.Success
                : AbilityPurchaseResult.PersistenceFailed;
        }
    }

    private AbilityUnlockResult ValidateAbilityUnlock(LocalProfileSnapshot candidate, AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return AbilityUnlockResult.InvalidAbility;
        }

        if (_abilityCatalog == null || !_abilityCatalog.TryGet(abilityId, out _))
        {
            return AbilityUnlockResult.UnknownAbility;
        }

        return candidate.UnlockedAbilities.Contains(abilityId)
            ? AbilityUnlockResult.AlreadyUnlocked
            : AbilityUnlockResult.Success;
    }

    public AbilityPreparationResult TrySetPreparedAbility(
        UniversalAbilitySlot slot,
        AbilityId abilityId)
    {
        if (!PreparedAbilityLoadout.IsKnownSlot(slot))
        {
            return AbilityPreparationResult.InvalidSlot;
        }

        if (!abilityId.IsValid)
        {
            return AbilityPreparationResult.InvalidAbility;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPreparationResult.ProfileUnavailable;
            }

            if (_abilityCatalog == null || !_abilityCatalog.TryGet(abilityId, out AbilityDefinition definition))
            {
                return AbilityPreparationResult.UnknownAbility;
            }

            if (!current.UnlockedAbilities.Contains(abilityId))
            {
                return AbilityPreparationResult.AbilityNotUnlocked;
            }

            UniversalAbilitySlot otherSlot = slot == UniversalAbilitySlot.Slot1
                ? UniversalAbilitySlot.Slot2
                : UniversalAbilitySlot.Slot1;
            if (current.PreparedAbilities.Get(otherSlot) == abilityId)
            {
                return AbilityPreparationResult.DuplicateAbility;
            }

            if (!definition.AreAttributeRequirementsSatisfiedBy(current.CharacterAttributes))
            {
                return AbilityPreparationResult.AttributeRequirementsNotMet;
            }

            PreparedAbilityLoadout candidate = current.PreparedAbilities.With(slot, abilityId);
            if (!PreparedAbilityLoadout.TryValidate(
                    candidate,
                    current.UnlockedAbilities,
                    current.CharacterAttributes,
                    _abilityCatalog,
                    out _))
            {
                return AbilityPreparationResult.InvalidPreparedState;
            }

            LocalProfileSnapshot next = current.Clone();
            next.PreparedAbilities = candidate;
            return Commit(next) == StashOperationResult.Success
                ? AbilityPreparationResult.Success
                : AbilityPreparationResult.PersistenceFailed;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Developer/test tooling only: clears the whole unlocked repertoire and both prepared slots in one
    /// transaction. Gameplay acquisition stays permanent; no production flow calls this.
    /// </summary>
    public bool TryResetAbilityRepertoire()
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return false;
            }

            if (current.UnlockedAbilities.Count == 0 && !current.PreparedAbilities.Slot1.IsValid
                && !current.PreparedAbilities.Slot2.IsValid)
            {
                return false;
            }

            LocalProfileSnapshot next = current.Clone();
            next.UnlockedAbilities.Clear();
            next.PreparedAbilities = default;
            return Commit(next) == StashOperationResult.Success;
        }
    }
#endif

    public AbilityPreparationResult TryClearPreparedAbility(UniversalAbilitySlot slot)
    {
        if (!PreparedAbilityLoadout.IsKnownSlot(slot))
        {
            return AbilityPreparationResult.InvalidSlot;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPreparationResult.ProfileUnavailable;
            }

            if (!current.PreparedAbilities.Get(slot).IsValid)
            {
                return AbilityPreparationResult.Success;
            }

            PreparedAbilityLoadout candidate = current.PreparedAbilities.Without(slot);
            if (!PreparedAbilityLoadout.TryValidate(
                    candidate,
                    current.UnlockedAbilities,
                    current.CharacterAttributes,
                    _abilityCatalog,
                    out _))
            {
                return AbilityPreparationResult.InvalidPreparedState;
            }

            LocalProfileSnapshot next = current.Clone();
            next.PreparedAbilities = candidate;
            return Commit(next) == StashOperationResult.Success
                ? AbilityPreparationResult.Success
                : AbilityPreparationResult.PersistenceFailed;
        }
    }
    public StashOperationResult TryAcceptMission(MissionDefinition mission)
    {
        if (mission == null || !mission.MissionId.IsValid) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;

        foreach (var active in current.ActiveMissions)
        {
            if (active.MissionId == mission.MissionId)
                return StashOperationResult.AlreadyApplied;
        }

        if (_missionCatalog != null)
        {
            var activeTypes = new List<MissionType>();
            foreach (var active in current.ActiveMissions)
            {
                if (active.State == MissionState.Reclamada || active.State == MissionState.Abandonada)
                    continue;

                if (_missionCatalog.TryGet(active.MissionId.Value, out var activeDef))
                    activeTypes.Add(activeDef.Type);
            }

            if (!MissionLifecycleRules.CanAcceptMission(mission.Type, activeTypes))
                return StashOperationResult.PersistenceFailed;
        }

        var next = current.Clone();
        next.ActiveMissions.Add(new MissionInstanceState(mission.MissionId, MissionState.Activa));
        return Commit(next);
    }

    public StashOperationResult TryAbandonMission(MissionId missionId)
    {
        if (!missionId.IsValid) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;

        int index = current.ActiveMissions.FindIndex(m => m.MissionId == missionId);
        if (index < 0) return StashOperationResult.InvalidInventory;

        var instance = current.ActiveMissions[index];
        if (!MissionLifecycleRules.CanTransitionTo(instance.State, MissionState.Abandonada))
            return StashOperationResult.PersistenceFailed;

        var next = current.Clone();
        next.ActiveMissions[index].State = MissionState.Abandonada;
        return Commit(next);
    }

    public StashOperationResult TryClaimMission(MissionDefinition mission)
    {
        if (mission == null || !mission.MissionId.IsValid) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;

        int index = current.ActiveMissions.FindIndex(m => m.MissionId == mission.MissionId);
        if (index < 0) return StashOperationResult.InvalidInventory;

        var instance = current.ActiveMissions[index];
        if (!MissionLifecycleRules.CanTransitionTo(instance.State, MissionState.Reclamada))
            return StashOperationResult.PersistenceFailed;

        var next = current.Clone();
        next.ActiveMissions[index].State = MissionState.Reclamada;

        if (mission.Rewards != null)
        {
            var itemsToAdd = new List<StashItem>();
            foreach (var reward in mission.Rewards)
            {
                if (reward.Type == RewardDefinition.RewardType.Gold)
                {
                    if (next.Currency > long.MaxValue - reward.Amount) return StashOperationResult.InvalidInventory;
                    next.Currency += reward.Amount;
                    Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount} Oro. (Total: {next.Currency})");
                }
                else if (reward.Type == RewardDefinition.RewardType.Experience)
                {
                    if (CharacterProgressionRules.TryApplyExperience(
                            ProgressionBalanceDefaults.InitialExperienceCurve,
                            next.Level,
                            next.CurrentExperience,
                            reward.Amount,
                            out ExperienceApplicationResult expResult))
                    {
                        next.Level = expResult.ResultingLevel;
                        next.CurrentExperience = expResult.ResultingExperience;
                        
                        next.LastAppliedProgressionResultSequence++;
                        var receipt = new ProgressionReceipt(
                            "mission-claim",
                            _profileId,
                            next.LastAppliedProgressionResultSequence,
                            next.CurrentExperience,
                            next.Level);
                            
                        next.LastProgressionReceipt = receipt;
                        next.AppliedProgressionReceipts.Add(receipt);
                        while (next.AppliedProgressionReceipts.Count > LocalProfileSnapshot.MaxAppliedProgressionReceipts)
                        {
                            next.AppliedProgressionReceipts.RemoveAt(0);
                        }

                        Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount} XP. (Nivel: {next.Level}, XP: {next.CurrentExperience})");
                    }
                    else
                    {
                        Debug.LogWarning($"[MissionBoard] No se pudo aplicar la experiencia de recompensa ({reward.Amount} XP).");
                    }
                }
                else if (reward.Type == RewardDefinition.RewardType.Item || reward.Type == RewardDefinition.RewardType.Equipment)
                {
                    var lootId = new LootId(reward.ReferenceId);
                    if (lootId.IsValid && reward.Amount > 0)
                    {
                        itemsToAdd.Add(new StashItem(lootId, reward.Amount));
                        Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount}x {reward.ReferenceId} para el Stash.");
                    }
                }
                else if (reward.Type == RewardDefinition.RewardType.Ability)
                {
                    // An owned ability is not acquired again; the rest of the package is still claimed.
                    AbilityId.TryCreate(reward.ReferenceId, out AbilityId abilityId);
                    AbilityUnlockResult unlock = ValidateAbilityUnlock(next, abilityId);
                    if (unlock == AbilityUnlockResult.Success)
                    {
                        next.UnlockedAbilities.Add(abilityId);
                    }
                    else if (unlock != AbilityUnlockResult.AlreadyUnlocked)
                    {
                        Debug.LogWarning($"[MissionBoard] Recompensa de habilidad inválida '{reward.ReferenceId}' ({unlock}); la reclamación se rechaza.");
                        return StashOperationResult.InvalidInventory;
                    }
                }
            }

            if (itemsToAdd.Count > 0)
            {
                if (!CanAdmitStashItems(current.Stash, itemsToAdd)) return StashOperationResult.InventoryFull;
                if (!TryMerge(next.Stash, itemsToAdd))
                    return StashOperationResult.PersistenceFailed;
                Debug.Log($"[MissionBoard] {itemsToAdd.Count} items de recompensa añadidos al Stash local exitosamente.");
            }
        }

        var result = Commit(next);
        if (result == StashOperationResult.Success)
        {
            Debug.Log($"[MissionBoard] Misión '{mission.Title}' reclamada y persistida correctamente en disco.");
        }
        return result;
    }

    public StashOperationResult TryApplyMissionProgress(MissionContributionEvent contribution)
    {
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;
        if (_missionCatalog == null) return StashOperationResult.PersistenceFailed;

        var next = current.Clone();
        bool changed = false;

        for (int i = 0; i < next.ActiveMissions.Count; i++)
        {
            var instance = next.ActiveMissions[i];
            if (instance.State != MissionState.Activa) continue;

            if (_missionCatalog.TryGet(instance.MissionId.Value, out var definition))
            {
                if (MissionProgressEngine.TryApplyProgress(instance, definition, contribution))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            return Commit(next);
        }

        return StashOperationResult.Success;
    }

    public bool TryGetCharacterAttributeState(out CharacterAttributeState state)
    {
        lock (_sync)
        {
            state = default;
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return false;
            }

            state = snapshot.CharacterAttributes;
            return true;
        }
    }

    public CharacterAttributeAssignmentCommitResult TryAssignCharacterAttribute(
        CharacterAttribute attribute,
        out CharacterAttributeAssignmentFailure failure)
    {
        lock (_sync)
        {
            failure = CharacterAttributeAssignmentFailure.None;
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return CharacterAttributeAssignmentCommitResult.Unavailable;
            }

            if (!CharacterAttributeAssignmentRules.TryAssign(
                    ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                    current.CharacterAttributes,
                    attribute,
                    out CharacterAttributeState candidate,
                    out failure))
            {
                return CharacterAttributeAssignmentCommitResult.Rejected;
            }

            if (!TryRevalidatePreparedAbilities(
                    current,
                    candidate,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Attribute assignment found invalid prepared abilities: {abilityError}");
                return CharacterAttributeAssignmentCommitResult.PersistenceFailed;
            }

            LocalProfileSnapshot next = current.Clone();
            next.CharacterAttributes = candidate;
            next.PreparedAbilities = revalidatedAbilities;
            return Commit(next) == StashOperationResult.Success
                ? CharacterAttributeAssignmentCommitResult.Success
                : CharacterAttributeAssignmentCommitResult.PersistenceFailed;
        }
    }

    public void SetRemoteRevision(int revision)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (IsAvailable && current != null && current.ProfileId == _profileId)
            {
                LocalProfileSnapshot next = current.Clone();
                next.RemoteRevision = revision;
                Commit(next);
            }
        }
    }

    public StashOperationResult ReconcileRemoteState(
        Grimhold.Backend.InventoryData? inventoryData,
        Grimhold.Backend.ProgressionData? progressionData,
        LootDefinitionCatalog catalog,
        AbilityDefinitionCatalog abilityCatalog)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (IsAvailable && current != null && current.ProfileId == _profileId)
            {
                LocalProfileSnapshot next = current.Clone();
                if (!ApplicationStashServiceBootstrapper.HydrateSnapshot(
                    _profileId, next, inventoryData, progressionData, catalog, abilityCatalog))
                {
                    return StashOperationResult.HydrationFailed;
                }

                // The revision is directly assigned from the backend data, bypassing
                // Math.Max to ensure it matches the server exactly even if it was rolled back or reset.
                if (inventoryData.HasValue)   next.RemoteRevision = inventoryData.Value.revision;
                if (progressionData.HasValue) next.RemoteRevision = progressionData.Value.revision;

                return Commit(next);
            }
            return StashOperationResult.PersistenceFailed;
        }
    }

    public void ForceCharacterAttributeState(CharacterAttributeState state)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
                return;

            if (!TryRevalidatePreparedAbilities(
                    current,
                    state,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Forced attributes found invalid prepared abilities: {abilityError}");
                return;
            }

            LocalProfileSnapshot next = current.Clone();
            next.CharacterAttributes = state;
            next.PreparedAbilities = revalidatedAbilities;
            Commit(next);
        }
    }

    public StashOperationResult TryCreditCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency > long.MaxValue - amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency += amount;
        return Commit(next);
    }

    public StashOperationResult TryDebitCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency < amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency -= amount;
        return Commit(next);
    }

    public StashOperationResult TrySecureLoot(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        if (!IsAvailable || _repository.Snapshot == null) return StashOperationResult.InvalidInventory;
        if (!CanAdmitStashItems(_repository.Snapshot.Stash, items)) return StashOperationResult.InventoryFull;
        var next = _repository.Snapshot.Clone();
        if (!TryMerge(next.Stash, items)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Applies one authority-issued trade to the Inventory (Loadout) and Currency as a single
    /// commit. Sales are checked against the confirmed Inventory, sales may fund purchases, and
    /// Currency and slot capacity are validated on the final state of every line together, the
    /// same semantics as <see cref="MerchantTradePreview"/>. Buy and sell lines of the same loot
    /// stay independent. Any failure leaves the profile unchanged and publishes nothing;
    /// a ticket whose transaction id was already persisted returns
    /// <see cref="StashOperationResult.AlreadyApplied"/> without mutating or publishing.
    /// </summary>
    /// <summary>
    /// Whether the trade with this transaction id is already persisted in the confirmed profile.
    /// A transaction at or below the idempotency watermark counts as applied.
    /// </summary>
    public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId)
    {
        if (!profileId.IsValid || profileId != _profileId || !transactionId.IsValid)
            return false;

        lock (_sync)
        {
            return IsShopReceiptApplied(_repository.Snapshot, new ShopTransactionReceipt(transactionId, _profileId));
        }
    }

    public StashOperationResult TryCommitTrade(ProfileId profileId, MerchantTradeTicket ticket)
    {
        if (ticket == null || !profileId.IsValid || profileId != _profileId || !ticket.IsWellFormed)
            return StashOperationResult.InvalidInventory;

        lock (_sync)
        {
            var receipt = new ShopTransactionReceipt(ticket.TransactionId, _profileId);
            LocalProfileSnapshot current = _repository.Snapshot;
            if (IsShopReceiptApplied(current, receipt))
                return StashOperationResult.AlreadyApplied;

            LocalProfileSnapshot next = current.Clone();
            long saleTotal = 0;
            foreach (MerchantPricedTradeLine sale in ticket.Sales)
            {
                if (FindAmount(current.Loadout, sale.LootId) < sale.Amount ||
                    !TryRemove(next.Loadout, sale.LootId, sale.Amount) ||
                    saleTotal > long.MaxValue - sale.Total)
                    return StashOperationResult.InvalidInventory;
                saleTotal += sale.Total;
            }

            long purchaseTotal = 0;
            var purchased = new List<StashItem>(ticket.Purchases.Count);
            foreach (MerchantPricedTradeLine purchase in ticket.Purchases)
            {
                if (purchaseTotal > long.MaxValue - purchase.Total)
                    return StashOperationResult.InvalidInventory;
                purchaseTotal += purchase.Total;
                purchased.Add(new StashItem(purchase.LootId, purchase.Amount));
            }

            if (next.Currency > long.MaxValue - saleTotal)
                return StashOperationResult.InvalidInventory;
            long projectedCurrency = next.Currency + saleTotal - purchaseTotal;
            if (projectedCurrency < 0 ||
                ExceedsCapacity(next.Loadout, purchased, LocalProfileSnapshot.MaxLoadoutSlots) ||
                !TryMerge(next.Loadout, purchased))
                return StashOperationResult.InvalidInventory;

            next.Currency = projectedCurrency;
            RecordShopReceipt(next, receipt);
            return Commit(next);
        }
    }

    private static bool IsShopReceiptApplied(LocalProfileSnapshot snapshot, ShopTransactionReceipt receipt)
    {
        if (receipt.TransactionId.Timestamp <= snapshot.ShopIdempotencyWatermark)
            return true;

        foreach (var applied in snapshot.AppliedShopTransactionReceipts)
            if (applied.Equals(receipt)) return true;
        return false;
    }

    private static void RecordShopReceipt(LocalProfileSnapshot next, ShopTransactionReceipt receipt)
    {
        next.AppliedShopTransactionReceipts.Add(receipt);
        next.AppliedShopTransactionReceipts.Sort((a, b) => a.TransactionId.Timestamp.CompareTo(b.TransactionId.Timestamp));

        while (next.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            var oldest = next.AppliedShopTransactionReceipts[0];
            next.AppliedShopTransactionReceipts.RemoveAt(0);
            if (oldest.TransactionId.Timestamp > next.ShopIdempotencyWatermark)
                next.ShopIdempotencyWatermark = oldest.TransactionId.Timestamp;
        }
    }

    public StashOperationResult TryConsumeLoot(LootId lootId, int amount)
    {
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (!TryRemove(next.Stash, lootId, amount)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Reads the confirmed snapshot for a Town Stash, Loadout or Equipment mutation. A reservation
    /// owns Inventory and Equipment until it is confirmed or rolled back, so none of these
    /// operations may run while one is pending: a later rollback would overwrite their result.
    /// </summary>
    private StashOperationResult BeginTownMutation(out LocalProfileSnapshot current)
    {
        current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;
        return current.PendingReservation != null
            ? StashOperationResult.ReservationPending
            : StashOperationResult.Success;
    }

    // Each transfer is planned once: the Try method commits the planned snapshot and the Evaluate
    // method discards it, so a dry run always reports exactly what the transfer would return. A
    // plan that succeeds with no snapshot means there is nothing to change.

    public StashOperationResult TryTransferToLoadout(LootId lootId, int amount) =>
        CommitPlan(PlanTransferToLoadout(lootId, amount, out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferToStash(LootId lootId, int amount) =>
        CommitPlan(PlanTransferToStash(lootId, amount, out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferAllToLoadout() =>
        CommitPlan(PlanTransferAllToLoadout(out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferAllToStash() =>
        CommitPlan(PlanTransferAllToStash(out LocalProfileSnapshot next), next);

    /// <summary>Reports what <see cref="TryTransferToLoadout"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferToLoadout(LootId lootId, int amount) =>
        PlanTransferToLoadout(lootId, amount, out _);

    /// <summary>Reports what <see cref="TryTransferToStash"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferToStash(LootId lootId, int amount) =>
        PlanTransferToStash(lootId, amount, out _);

    /// <summary>Reports what <see cref="TryTransferAllToLoadout"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferAllToLoadout() => PlanTransferAllToLoadout(out _);

    /// <summary>Reports what <see cref="TryTransferAllToStash"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferAllToStash() => PlanTransferAllToStash(out _);

    private StashOperationResult CommitPlan(StashOperationResult plan, LocalProfileSnapshot next) =>
        plan == StashOperationResult.Success && next != null ? Commit(next) : plan;

    private StashOperationResult PlanTransferToLoadout(LootId lootId, int amount, out LocalProfileSnapshot next)
    {
        next = null;
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        LocalProfileSnapshot candidate = current.Clone();
        int available = FindAmount(candidate.Stash, lootId);
        if (available <= 0) return StashOperationResult.InvalidInventory;
        int actualAmount = Math.Min(amount, available);
        if (FindIndex(candidate.Loadout, lootId) < 0 &&
            candidate.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.InventoryFull;
        if (!TryRemove(candidate.Stash, lootId, actualAmount) ||
            !TryMerge(candidate.Loadout, new[] { new StashItem(lootId, actualAmount) }))
            return StashOperationResult.InvalidInventory;
        next = candidate;
        return StashOperationResult.Success;
    }

    private StashOperationResult PlanTransferToStash(LootId lootId, int amount, out LocalProfileSnapshot next)
    {
        next = null;
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        LocalProfileSnapshot candidate = current.Clone();
        int available = FindAmount(candidate.Loadout, lootId);
        if (available <= 0) return StashOperationResult.InvalidInventory;
        int actualAmount = Math.Min(amount, available);
        if (!CanAdmitStashItems(current.Stash, new[] { new StashItem(lootId, actualAmount) }))
            return StashOperationResult.InventoryFull;
        if (!TryRemove(candidate.Loadout, lootId, actualAmount) ||
            !TryMerge(candidate.Stash, new[] { new StashItem(lootId, actualAmount) }))
            return StashOperationResult.InvalidInventory;
        next = candidate;
        return StashOperationResult.Success;
    }

    private StashOperationResult PlanTransferAllToLoadout(out LocalProfileSnapshot next)
    {
        next = null;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        if (current.Stash.Count == 0) return StashOperationResult.Success;
        LocalProfileSnapshot candidate = current.Clone();
        int newSlots = 0;
        foreach (StashItem item in candidate.Stash)
            if (FindIndex(candidate.Loadout, item.LootId) < 0) newSlots++;
        if (candidate.Loadout.Count + newSlots > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.InventoryFull;
        if (!TryMerge(candidate.Loadout, candidate.Stash)) return StashOperationResult.InvalidInventory;
        candidate.Stash.Clear();
        next = candidate;
        return StashOperationResult.Success;
    }

    private StashOperationResult PlanTransferAllToStash(out LocalProfileSnapshot next)
    {
        next = null;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        if (current.Loadout.Count == 0) return StashOperationResult.Success;
        LocalProfileSnapshot candidate = current.Clone();
        if (!CanAdmitStashItems(current.Stash, candidate.Loadout)) return StashOperationResult.InventoryFull;
        if (!TryMerge(candidate.Stash, candidate.Loadout)) return StashOperationResult.InvalidInventory;
        candidate.Loadout.Clear();
        next = candidate;
        return StashOperationResult.Success;
    }

    // Capacity is admission-only: accepted remote excess may remain and existing IDs may merge.
    private bool CanAdmitStashItems(IReadOnlyList<StashItem> current, IReadOnlyList<StashItem> incoming)
    {
        var ids = new HashSet<LootId>();
        foreach (StashItem item in current) ids.Add(item.LootId);
        int existing = ids.Count;
        foreach (StashItem item in incoming) ids.Add(item.LootId);
        return ids.Count == existing || ids.Count <= StashCapacity;
    }

    public StashOperationResult TryImportItems(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        int newSlots = 0;
        foreach (StashItem item in items)
            if (FindIndex(next.Loadout, item.LootId) < 0) newSlots++;
        if (next.Loadout.Count + newSlots > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;
        if (!TryMerge(next.Loadout, items)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Moves one unit from the persistent Inventory into an Equipment slot in one atomic commit.
    /// A replaced unit returns to Inventory after the new unit is removed, so capacity is evaluated
    /// against the final swap state rather than an invalid intermediate state.
    /// </summary>
    public StashOperationResult TryAssignPreparedEquipment(EquipmentSlot slot, LootId lootId)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot) || !lootId.IsValid || _lootCatalog == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        if (FindAmount(current.Loadout, lootId) < 1 ||
            !_lootCatalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot next = current.Clone();
        PreparedEquipmentLoadout candidate = next.PreparedEquipment;
        EquipmentSlot displacedSecondSlot = EquipmentSlot.None;
        if (!EquipmentSlotRules.IsCompatible(definition, slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        WeaponSetSlot targetSet = EquipmentSlotRules.GetWeaponSet(slot);
        if (EquipmentSlotRules.IsOffHandSlot(slot) &&
            PreparedEquipmentLoadout.IsOffHandBlocked(candidate, targetSet, _lootCatalog))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (definition.Category == LootCategory.Weapon)
        {
            WeaponDefinition weapon = definition.WeaponDefinition;
            if (weapon.Handedness == WeaponHandedness.TwoHanded)
            {
                displacedSecondSlot = EquipmentSlotRules.GetOffHandSlot(targetSet);
                candidate = candidate.Without(displacedSecondSlot);
            }
        }

        candidate = candidate.With(slot, lootId);

        if (definition.Category == LootCategory.Weapon &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!TryRemove(next.Loadout, lootId, 1))
        {
            return StashOperationResult.InvalidInventory;
        }

        LootId previousLootId = next.PreparedEquipment.Get(slot);
        if (!TryReturnEquippedUnit(next.Loadout, previousLootId))
        {
            return StashOperationResult.PersistenceFailed;
        }

        if (displacedSecondSlot != EquipmentSlot.None &&
            !TryReturnEquippedUnit(next.Loadout, next.PreparedEquipment.Get(displacedSecondSlot)))
        {
            return StashOperationResult.PersistenceFailed;
        }

        next.PreparedEquipment = candidate;
        return Commit(next);
    }

    public StashOperationResult EvaluateEquipFromStash(
        EquipmentSlot slot, LootId lootId, out PreparedEquipmentLoadout equipment)
    {
        StashOperationResult result = PlanEquipFromStash(slot, lootId, out LocalProfileSnapshot next);
        equipment = result == StashOperationResult.Success ? next.PreparedEquipment : default;
        return result;
    }

    public StashOperationResult TryEquipFromStash(EquipmentSlot slot, LootId lootId)
    {
        StashOperationResult result = PlanEquipFromStash(slot, lootId, out LocalProfileSnapshot next);
        return result == StashOperationResult.Success ? Commit(next) : result;
    }

    private StashOperationResult PlanEquipFromStash(
        EquipmentSlot slot, LootId lootId, out LocalProfileSnapshot next)
    {
        next = null;
        if (!EquipmentSlotRules.IsEquipmentSlot(slot) || !lootId.IsValid || _lootCatalog == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        if (FindAmount(current.Stash, lootId) < 1 ||
            !_lootCatalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        next = current.Clone();
        PreparedEquipmentLoadout candidate = next.PreparedEquipment;
        EquipmentSlot displacedSecondSlot = EquipmentSlot.None;
        if (!EquipmentSlotRules.IsCompatible(definition, slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        WeaponSetSlot targetSet = EquipmentSlotRules.GetWeaponSet(slot);
        if (EquipmentSlotRules.IsOffHandSlot(slot) &&
            PreparedEquipmentLoadout.IsOffHandBlocked(candidate, targetSet, _lootCatalog))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (definition.Category == LootCategory.Weapon)
        {
            WeaponDefinition weapon = definition.WeaponDefinition;
            if (weapon.Handedness == WeaponHandedness.TwoHanded)
            {
                displacedSecondSlot = EquipmentSlotRules.GetOffHandSlot(targetSet);
                candidate = candidate.Without(displacedSecondSlot);
            }
        }

        candidate = candidate.With(slot, lootId);

        if (definition.Category == LootCategory.Weapon &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!TryRemove(next.Stash, lootId, 1))
        {
            return StashOperationResult.InvalidInventory;
        }

        LootId previousLootId = next.PreparedEquipment.Get(slot);
        if (!TryReturnEquippedUnitToLoadout(next.Loadout, previousLootId))
        {
            return StashOperationResult.PersistenceFailed;
        }

        if (displacedSecondSlot != EquipmentSlot.None &&
            !TryReturnEquippedUnitToLoadout(next.Loadout, next.PreparedEquipment.Get(displacedSecondSlot)))
        {
            return StashOperationResult.PersistenceFailed;
        }

        // Preserve hydrated excess without permitting it to grow. No intermediate
        // Loadout slot is needed for the incoming Stash unit.
        if (next.Loadout.Count > Math.Max(LocalProfileSnapshot.MaxLoadoutSlots, current.Loadout.Count))
        {
            return StashOperationResult.InventoryFull;
        }

        next.PreparedEquipment = candidate;
        return StashOperationResult.Success;
    }

    private static bool TryReturnEquippedUnitToLoadout(List<StashItem> inventory, LootId lootId)
    {
        if (!lootId.IsValid) return true;
        return TryMerge(inventory, new[] { new StashItem(lootId, 1) });
    }

    private static bool TryReturnEquippedUnit(List<StashItem> inventory, LootId lootId)
    {
        if (!lootId.IsValid) return true;
        return (FindIndex(inventory, lootId) >= 0 ||
                inventory.Count < LocalProfileSnapshot.MaxLoadoutSlots) &&
            TryMerge(inventory, new[] { new StashItem(lootId, 1) });
    }

    /// <summary>Moves one equipped unit back into the persistent Inventory atomically.</summary>
    public StashOperationResult TryClearPreparedEquipment(EquipmentSlot slot)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        LootId previousLootId = current.PreparedEquipment.Get(slot);
        if (!previousLootId.IsValid)
        {
            return StashOperationResult.Success;
        }

        LocalProfileSnapshot next = current.Clone();
        if ((FindIndex(next.Loadout, previousLootId) < 0 &&
             next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots) ||
            !TryMerge(next.Loadout, new[] { new StashItem(previousLootId, 1) }))
        {
            return StashOperationResult.PersistenceFailed;
        }

        next.PreparedEquipment = next.PreparedEquipment.Without(slot);
        return Commit(next);
    }

    /// <summary>
    /// Normalizes the local Loadout and prepared Weapon Equipment so the aggregate holds exactly
    /// one valid effective Main Hand weapon before a raid reservation is attempted.
    /// The operation is atomic, deterministic and idempotent: a profile that is already prepared
    /// commits nothing, so retrying a launch never grants or duplicates a recovery weapon.
    /// </summary>
    public ExpeditionPreparationResult TryPrepareExpeditionEquipment()
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (!IsAvailable || current == null || _lootCatalog == null)
        {
            return ExpeditionPreparationResult.ProfileUnavailable;
        }

        // A reservation already owns the prepared equipment; re-preparing would double-grant.
        if (current.PendingReservation != null)
        {
            return ExpeditionPreparationResult.Success;
        }

        PreparedEquipmentLoadout prepared = current.PreparedEquipment;

        // A persisted assignment that no longer resolves to a usable weapon is corruption.
        // It fails explicitly instead of being overwritten or hidden behind a recovery grant.
        if (prepared.HasAnyWeapon && !PreparedEquipmentLoadout.TryValidate(
                prepared,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return ExpeditionPreparationResult.InvalidPreparedWeapon;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                prepared,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        // Admission carries the active Set explicitly, so either valid Main Hand is sufficient.
        if (prepared.HasAnyMainHand)
        {
            return ExpeditionPreparationResult.Success;
        }

        return TryPrepareRecoveryWeapon(current);
    }

    /// <summary>
    /// Guarantees Town access to exactly one recovery weapon when no weapon is prepared. A unit
    /// already owned is reused before a new one is granted, so the grant cannot accumulate.
    /// </summary>
    private ExpeditionPreparationResult TryPrepareRecoveryWeapon(LocalProfileSnapshot current)
    {
        if (!PreparedEquipmentLoadout.IsUsableWeaponDefinition(_recoveryWeaponLootId, _lootCatalog))
        {
            return ExpeditionPreparationResult.RecoveryWeaponUnavailable;
        }

        var recovery = new PreparedEquipmentLoadout(_recoveryWeaponLootId, default);
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                recovery,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        LocalProfileSnapshot next = current.Clone();
        if (FindAmount(next.Loadout, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Loadout, _recoveryWeaponLootId, 1);
        }
        else if (FindAmount(next.Stash, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Stash, _recoveryWeaponLootId, 1);
        }

        // Only Set A Main Hand is granted: all other prepared Equipment remains untouched.
        next.PreparedEquipment = next.PreparedEquipment
            .With(EquipmentSlot.WeaponSetAMainHand, _recoveryWeaponLootId);
        return Commit(next) == StashOperationResult.Success
            ? ExpeditionPreparationResult.Success
            : ExpeditionPreparationResult.PersistenceFailed;
    }

    public StashOperationResult TryCreateLoadoutReservation(
        string reservationId,
        out PendingLoadoutReservation reservation)
    {
        reservation = null;
        if (string.IsNullOrWhiteSpace(reservationId) || !IsAvailable)
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. string.IsNullOrWhiteSpace(reservationId)={string.IsNullOrWhiteSpace(reservationId)}, !IsAvailable={!IsAvailable}");
            return StashOperationResult.InvalidInventory;
        }
        
        var current = _repository.Snapshot;
        if (current.PendingReservation != null)
        {
            if (!string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            {
                Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Pending reservation ID mismatch. Existing: {current.PendingReservation.ReservationId}, Requested: {reservationId}");
                return StashOperationResult.InvalidInventory;
            }

            if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                    current.PendingReservation.PreparedEquipment,
                    current.CharacterAttributes,
                    _lootCatalog,
                    out string validationError1))
            {
                Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Attribute requirements not met: {validationError1}");
                return StashOperationResult.AttributeRequirementsNotMet;
            }

            reservation = current.PendingReservation.Clone();
            return StashOperationResult.Success;
        }

        // Backend data may leave the Loadout over capacity; the excess must be moved
        // to the Stash before it can enter a raid, whose inventory is capped.
        if (current.Loadout.Count > LocalProfileSnapshot.MaxLoadoutSlots)
        {
            return StashOperationResult.InventoryFull;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                current.PreparedEquipment,
                _lootCatalog,
                requireWeapon: true,
                out string validationError2))
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Validation failed: {validationError2}");
            return StashOperationResult.InvalidInventory;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                current.PreparedEquipment,
                current.CharacterAttributes,
                _lootCatalog,
                out string validationError3))
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Attribute requirements not met: {validationError3}");
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        var next = current.Clone();
        next.PendingReservation = new PendingLoadoutReservation(
            reservationId,
            next.Loadout,
            next.PreparedEquipment);
        next.Loadout.Clear();
        next.PreparedEquipment = default;
        StashOperationResult result = Commit(next);
        if (result == StashOperationResult.Success)
        {
            reservation = _repository.Snapshot.PendingReservation.Clone();
        }
        return result;
    }

    public StashOperationResult TryConfirmLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        next.PendingReservation = null;
        return Commit(next);
    }

    public StashOperationResult TryRollbackLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        if (ExceedsCapacity(next.Loadout, next.PendingReservation.Items, LocalProfileSnapshot.MaxLoadoutSlots) ||
            !TryMerge(next.Loadout, next.PendingReservation.Items)) return StashOperationResult.PersistenceFailed;
        PreparedEquipmentLoadout restoredWeapons = next.PendingReservation.PreparedEquipment;
        if (!PreparedEquipmentLoadout.TryValidate(
                restoredWeapons,
                _lootCatalog,
                requireWeapon: true,
                out _))
        {
            return StashOperationResult.PersistenceFailed;
        }
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                restoredWeapons,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }
        next.PreparedEquipment = restoredWeapons;
        next.PendingReservation = null;
        return Commit(next);
    }

    /// <summary>
    /// Commits the exact authoritative raid inventory into the persistent Loadout.
    ///
    /// A valid raid admission consumes the pending reservation and leaves the
    /// persistent Loadout empty. A new receipt therefore fails atomically when
    /// that invariant is not true; extracted items are never redirected to Stash.
    /// </summary>
    public StashOperationResult TryCommitExtraction(
        ExtractionReceipt receipt, 
        IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment,
        long consolidatedExperience,
        int resultingLevel,
        long resultingExperience)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !HasValidItems(items, allowEmpty: true))
            return StashOperationResult.InvalidInventory;

        if (items.Count > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot current = _repository.Snapshot;
        foreach (ExtractionReceipt applied in current.AppliedExtractionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadySecured;

        if (current.Loadout.Count != 0)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot next = current.Clone();
        next.Loadout.AddRange(items);
        next.AppliedExtractionReceipts.Add(receipt);
        while (next.AppliedExtractionReceipts.Count > LocalProfileSnapshot.MaxAppliedExtractionReceipts)
            next.AppliedExtractionReceipts.RemoveAt(0);

        next.Level = resultingLevel;
        next.CurrentExperience = resultingExperience;
        next.LastAppliedProgressionResultSequence = receipt.ResultSequence;

        var progressionReceipt = new ProgressionReceipt(
            receipt.RaidId,
            _profileId,
            receipt.ResultSequence,
            resultingExperience,
            resultingLevel);

        next.LastProgressionReceipt = progressionReceipt;
        next.AppliedProgressionReceipts.Add(progressionReceipt);
        while (next.AppliedProgressionReceipts.Count > LocalProfileSnapshot.MaxAppliedProgressionReceipts)
            next.AppliedProgressionReceipts.RemoveAt(0);

        next.PreparedEquipment = preparedEquipment;

        next.PendingExtractionCommit = new PendingExtractionCommit(
            receipt,
            items,
            preparedEquipment,
            consolidatedExperience,
            resultingLevel);

        return Commit(next);
    }
    public void ClearPendingExtractionCommit()
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (current == null || current.PendingExtractionCommit == null)
            return;
            
        LocalProfileSnapshot next = current.Clone();
        next.PendingExtractionCommit = null;
        Commit(next);
    }

    public StashOperationResult TrySyncProgression(int level, long experience, Grimhold.Backend.CharacterAttributesData attributes)
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (current == null) return StashOperationResult.InvalidInventory;

        LocalProfileSnapshot next = current.Clone();
        next.Level = level;
        next.CurrentExperience = experience;

        if (CharacterAttributeState.TryCreate(
                attributes.vitality, attributes.resistance, attributes.strength,
                attributes.dexterity, attributes.intelligence, attributes.luck,
                attributes.availablePoints,
                out var state))
        {
            if (!TryRevalidatePreparedAbilities(
                    current,
                    state,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Progression sync found invalid prepared abilities: {abilityError}");
                return StashOperationResult.InvalidInventory;
            }

            next.CharacterAttributes = state;
            next.PreparedAbilities = revalidatedAbilities;
        }

        return Commit(next);
    }

    private bool TryRevalidatePreparedAbilities(
        LocalProfileSnapshot snapshot,
        in CharacterAttributeState attributes,
        out PreparedAbilityLoadout revalidated,
        out string error) =>
        PreparedAbilityLoadout.TryRevalidateAfterAttributeChange(
            snapshot.PreparedAbilities,
            snapshot.UnlockedAbilities,
            attributes,
            _abilityCatalog,
            out revalidated,
            out error);

    private StashOperationResult Commit(LocalProfileSnapshot next)
    {
        lock (_sync)
        {
            string error = null;
            bool saved = IsAvailable && _repository.TrySave(next, out error);
            if (!saved)
            {
                Debug.LogError(
                    $"[LocalProfileStore] Commit failed. Available={IsAvailable}; " +
                    $"Status={Status}; Error={error ?? _repository.LastError ?? "none"}.");
                return StashOperationResult.PersistenceFailed;
            }
            ProfileCommitted?.Invoke(_profileId);
            return StashOperationResult.Success;
        }
    }

    private static bool HasValidItems(IReadOnlyList<StashItem> items, bool allowEmpty = false)
    {
        if (items == null || (!allowEmpty && items.Count == 0)) return false;
        var seen = new HashSet<LootId>();
        foreach (StashItem item in items)
            if (!item.IsValid || !seen.Add(item.LootId)) return false;
        return true;
    }

}

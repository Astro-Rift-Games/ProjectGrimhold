using System;
using System.Threading.Tasks;

/// <summary>
/// One consent-bound asynchronous stage. Polling shares its task and request identity. Cancellation
/// invalidates publication, never issues an unguarded DELETE of an unknown remote reservation.
/// </summary>
public sealed class RecoveryWeaponReservationPreparation
{
    private readonly LocalProfileStore _store;
    private readonly RecoveryWeaponPreparation _consent;
    private readonly Func<bool> _isLaunchCurrent;
    private bool _cancelled;
    private Task<ExpeditionPreparationResult> _completion;
    private Task<bool> _recovery;
    private IRecoveryWeaponReservationService _remote;
    private IRecoveryWeaponAuthorityReader _authority;
    private RecoveryWeaponPreparation _acceptedBinding;
    public bool HasRecovered => _recovery != null && _recovery.Status == TaskStatus.RanToCompletion && _recovery.Result;
    public string ReservationId { get; } = Guid.NewGuid().ToString("N");
    public Task<ExpeditionPreparationResult> Completion => _completion;
    public bool IsCancelled => _cancelled;

    public RecoveryWeaponReservationPreparation(LocalProfileStore store, RecoveryWeaponPreparation consent, Func<bool> isLaunchCurrent)
    {
        _store = store;
        _consent = consent;
        _isLaunchCurrent = isLaunchCurrent;
    }

    public Task<ExpeditionPreparationResult> Start(IRecoveryWeaponReservationService remote, IRecoveryWeaponAuthorityReader authority)
    {
        if (_completion != null) return _completion;
        _remote = remote;
        _authority = authority;
        // Publish the task before running even synchronously completed transports or commit callbacks.
        var completion = new TaskCompletionSource<ExpeditionPreparationResult>();
        _completion = completion.Task;
        _ = CompleteAsync(completion, remote, authority);
        return _completion;
    }

    private async Task CompleteAsync(TaskCompletionSource<ExpeditionPreparationResult> completion,
        IRecoveryWeaponReservationService remote, IRecoveryWeaponAuthorityReader authority)
    {
        try { completion.SetResult(await RunAsync(remote, authority)); }
        catch (Exception) { completion.SetResult(ExpeditionPreparationResult.ReservationFailed); }
    }

    /// <summary>Wait out the original POST, conditionally release that exact ID, then hydrate full state.</summary>
    public Task<bool> RecoverAsync(Func<bool> isOwnerCurrent)
    {
        Cancel();
        if (_recovery != null) return _recovery;
        var completion = new TaskCompletionSource<bool>();
        _recovery = completion.Task;
        _ = CompleteRecoveryAsync(completion, isOwnerCurrent);
        return _recovery;
    }

    private async Task CompleteRecoveryAsync(TaskCompletionSource<bool> completion, Func<bool> isOwnerCurrent)
    {
        try
        {
            if (_completion != null) await _completion;
            var binding = _acceptedBinding ?? _consent;
            if (!isOwnerCurrent() || _remote == null || _authority == null ||
                !_store.IsRecoveryWeaponPreparationCurrent(binding) ||
                (_store.PendingReservation != null && _store.PendingReservation.ReservationId != ReservationId))
            { completion.SetResult(false); return; }
            bool Current() => isOwnerCurrent() && _store.IsRecoveryWeaponPreparationCurrent(binding);
            try
            {
                var response = await _remote.RollbackRecoveryWeaponAsync(ReservationId, Current);
                if (!response.success && (response.error.error == "RESERVATION_MISMATCH" || response.error.error == "UNAUTHORIZED"))
                { completion.SetResult(false); return; }
            }
            catch (Exception) { /* An ambiguous DELETE is resolved by a full read, not another mutation. */ }
            if (!Current()) { completion.SetResult(false); return; }
            var restored = await _authority.FetchRecoveryWeaponAuthorityAsync();
            completion.SetResult(Current() && _store.TryApplyRecoveredReservation(binding, ReservationId, restored));
        }
        catch (Exception) { completion.TrySetResult(false); }
    }

    public void Cancel() => _cancelled = true;
    public bool CanPublish(LocalProfileStore store) =>
        !_cancelled && ReferenceEquals(_store, store) && _isLaunchCurrent();
    private bool IsCurrent => !_cancelled && _isLaunchCurrent() && _store.IsRecoveryWeaponPreparationCurrent(_consent);

    private async Task<ExpeditionPreparationResult> RunAsync(IRecoveryWeaponReservationService remote, IRecoveryWeaponAuthorityReader authority)
    {
        if (!IsCurrent || remote == null || authority == null || !_store.CanReserveRecoveryWeapon(_consent))
            return ExpeditionPreparationResult.ProfileUnavailable;
        int minimumRevision = _consent.RemoteRevision;
        PreparedEquipmentLoadout? confirmedEquipment = null;
        try
        {
            var response = await remote.ReserveRecoveryWeaponAsync(ReservationId, () => IsCurrent);
            if (!IsCurrent) return ExpeditionPreparationResult.ProfileUnavailable;
            if (response.success)
            {
                if (!string.Equals(response.data.pendingReservation.reservationId, ReservationId, StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(response.data.pendingReservation.admittedAt) ||
                    response.data.pendingReservation.items == null ||
                    !response.data.pendingReservation.preparedEquipment.TryToLoadout(_store.LootCatalog, true, out var equipment, out _) ||
                    equipment.WeaponSetAMainHand != new LootId("arming_sword") ||
                    equipment.GetInstance(EquipmentSlot.WeaponSetAMainHand)?.HasIdentity != true)
                    return ExpeditionPreparationResult.ReservationFailed;
                confirmedEquipment = equipment;
                minimumRevision = Math.Max(minimumRevision, response.data.revision);
            }
            // A lost/ambiguous response is resolved by a full read, never by another mutating POST.
        }
        catch (Exception)
        {
            if (!IsCurrent) return ExpeditionPreparationResult.ProfileUnavailable;
        }

        try
        {
            RecoveryWeaponAuthorityState state = await authority.FetchRecoveryWeaponAuthorityAsync();
            if (!IsCurrent || state == null) return ExpeditionPreparationResult.ProfileUnavailable;
            if (confirmedEquipment.HasValue &&
                (!state.Inventory.pendingReservation.preparedEquipment.TryToLoadout(_store.LootCatalog, true, out var fetchedEquipment, out _) ||
                 !confirmedEquipment.Value.Equals(fetchedEquipment)))
                return ExpeditionPreparationResult.ReservationFailed;
            var result = _store.TryApplyRecoveryWeaponReservation(_consent, ReservationId, minimumRevision, state);
            if (result == ExpeditionPreparationResult.Success)
                _store.InspectRecoveryWeaponPreparation(out _acceptedBinding);
            return result;
        }
        catch (Exception)
        {
            // The backend may own the reservation. Fail closed and retain the request identity;
            // normal login reconciliation can recover it without deleting a newer reservation.
            return ExpeditionPreparationResult.ReservationFailed;
        }
    }
}

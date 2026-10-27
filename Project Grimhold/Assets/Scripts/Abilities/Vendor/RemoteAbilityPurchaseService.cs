using System;
using System.Threading.Tasks;
using Grimhold.Backend;
using UnityEngine;

/// <summary>
/// Remote-first ability purchase boundary: preliminary local evaluation, await backend acknowledgement,
/// then commit the local unlock and remote revision.
/// </summary>
public sealed class RemoteAbilityPurchaseService : MonoBehaviour, IAbilityPurchaseService
{
    public delegate Task<(bool success, PurchaseAbilityResult data, BackendError error)> BackendCall(
        string transactionId, string abilityId, int expectedRevision);

    private LocalProfileStore _store;
    private BackendCall _backend;
    private Func<Task<bool>> _reconcile;

    public void Initialize(LocalProfileStore store, ProfileReconciliationService reconciliationService)
    {
        if (reconciliationService == null) throw new ArgumentNullException(nameof(reconciliationService));
        BackendConfiguration config = LoginFlowController.Instance != null ? LoginFlowController.Instance.Config : null;
        if (config == null) config = Resources.Load<BackendConfiguration>("BackendConfiguration");
        if (config == null)
        {
            Debug.LogError("[RemoteAbilityPurchaseService] BackendConfiguration is missing; remote ability commerce is unavailable.");
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _backend = null;
            return;
        }

        Initialize(store, (transactionId, abilityId, revision) =>
            ProgressionClient.PurchaseAbilityAsync(config, ApplicationAuthContext.Instance?.Token, transactionId, abilityId, revision),
            reconciliationService.ReconcileAsync);
    }

    /// <summary>Explicit infrastructure injection, also used by deterministic tests.</summary>
    public void Initialize(LocalProfileStore store, BackendCall backend, Func<Task<bool>> reconcile)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _backend = backend;
        _reconcile = reconcile;
    }

    public async Task<AbilityPurchaseResult> PurchaseAbilityAsync(ProfileId profileId, AbilityId abilityId, long price)
    {
        if (_store == null || !_store.IsAvailable)
        {
            return AbilityPurchaseResult.ProfileUnavailable;
        }

        if (!profileId.IsValid || _store.ProfileId != profileId)
        {
            return AbilityPurchaseResult.ProfileUnavailable;
        }

        var localEval = _store.EvaluateAbilityPurchase(abilityId, price);
        if (localEval != AbilityPurchaseResult.Success)
        {
            return localEval;
        }

        if (_backend == null)
        {
            return _store.TryPurchaseAbility(abilityId, price);
        }

        string transactionId = Guid.NewGuid().ToString("N");
        (bool success, PurchaseAbilityResult data, BackendError error) response;
        try
        {
            response = await _backend(transactionId, abilityId.Value, _store.RemoteRevision);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RemoteAbilityPurchaseService] Backend outcome unknown: {exception.Message}");
            _ = ReconcileSafelyAsync();
            return AbilityPurchaseResult.PersistenceFailed;
        }

        if (response.success)
        {
            try
            {
                var commitResult = _store.ApplyConfirmedAbilityPurchase(
                    abilityId,
                    response.data.price,
                    response.data.revision,
                    response.data.currency);

                if (commitResult != AbilityPurchaseResult.Success)
                {
                    Debug.LogError($"[RemoteAbilityPurchaseService] Backend persisted ability purchase but local commit failed ({commitResult}); reconciling.");
                    _ = ReconcileSafelyAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RemoteAbilityPurchaseService] Backend persisted ability purchase but local persistence threw: {exception.Message}");
                _ = ReconcileSafelyAsync();
            }

            return AbilityPurchaseResult.Success;
        }

        if (response.error.error == BackendErrorUtility.Unauthorized)
        {
            ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            return AbilityPurchaseResult.PersistenceFailed;
        }

        _ = ReconcileSafelyAsync();
        return response.error.error switch
        {
            "RESERVATION_PENDING" => AbilityPurchaseResult.ReservationPending,
            "ALREADY_UNLOCKED" => AbilityPurchaseResult.AlreadyUnlocked,
            "INSUFFICIENT_FUNDS" => AbilityPurchaseResult.InsufficientCurrency,
            "REVISION_CONFLICT" => AbilityPurchaseResult.RevisionConflict,
            "ABILITY_NOT_OFFERED" or "INVALID_ABILITY_ID" => AbilityPurchaseResult.UnknownAbility,
            _ => AbilityPurchaseResult.PersistenceFailed
        };
    }

    private async Task ReconcileSafelyAsync()
    {
        try
        {
            if (_reconcile != null && !await _reconcile())
            {
                Debug.LogWarning("[RemoteAbilityPurchaseService] Reconciliation failed.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RemoteAbilityPurchaseService] Reconciliation failed: {exception.Message}");
        }
    }
}

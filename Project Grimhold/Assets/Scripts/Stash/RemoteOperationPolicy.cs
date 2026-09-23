using System;
using System.Threading.Tasks;
using Grimhold.Backend;

public static class RemoteOperationPolicy
{
    /// <summary>
    /// Executes a remote operation. If a REVISION_CONFLICT or Transport Failure occurs, 
    /// it triggers reconciliation. Does NOT automatically retry the mutation if reconciliation succeeds.
    /// If reconciliation fails, it propagates the failure.
    /// </summary>
    public static async Task<(bool success, T result, BackendError error)> ExecuteWithReconciliationAsync<T>(
        Func<Task<(bool success, T result, BackendError error)>> operation,
        Func<Task<bool>> reconciliationFunc)
    {
        var (success, result, error) = await operation();
        if (success)
        {
            return (true, result, default);
        }

        if (error.error == "REVISION_CONFLICT" || BackendErrorUtility.IsTransportFailure(error.error))
        {
            UnityEngine.Debug.LogWarning($"[RemoteOperationPolicy] {error.error} detected. Reconciling...");
            
            bool reconciled = await reconciliationFunc();
            if (!reconciled)
            {
                return (false, default, new BackendError { error = "RECONCILIATION_FAILED", message = "Failed to reconcile state with remote server." });
            }

            // Return the original error so the caller knows the mutation was rejected/undetermined and needs user action
            return (false, default, error);
        }

        return (false, default, error);
    }

    public static async Task<(bool success, BackendError error)> ExecuteWithReconciliationAsync(
        Func<Task<(bool success, BackendError error)>> operation,
        Func<Task<bool>> reconciliationFunc)
    {
        var (success, result, error) = await ExecuteWithReconciliationAsync(
            async () =>
            {
                var (opSuccess, opError) = await operation();
                return (opSuccess, true, opError);
            },
            reconciliationFunc);

        return (success, error);
    }
}

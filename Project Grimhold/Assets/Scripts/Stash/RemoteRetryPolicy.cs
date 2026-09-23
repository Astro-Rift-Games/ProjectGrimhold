using System;
using System.Threading.Tasks;
using Grimhold.Backend;

public static class RemoteRetryPolicy
{
    public const int MaxRetries = 3;

    /// <summary>
    /// Executes a remote operation with optimistic concurrency.
    /// If a REVISION_CONFLICT is detected, it triggers a reconciliation function and retries up to MaxRetries.
    /// </summary>
    public static async Task<(bool success, T result, BackendError error)> ExecuteWithRetryAsync<T>(
        Func<Task<(bool success, T result, BackendError error)>> operation,
        Func<Task<bool>> reconciliationFunc,
        Func<int> currentRevisionProvider = null)
    {
        int retries = 0;
        while (retries < MaxRetries)
        {
            int sentRevision = currentRevisionProvider?.Invoke() ?? -1;

            var (success, result, error) = await operation();
            if (success)
            {
                return (true, result, default);
            }

            if (error.error == "REVISION_CONFLICT")
            {
                UnityEngine.Debug.LogWarning($"[RemoteRetryPolicy] Revision conflict detected. Reconciling... (Attempt {retries + 1}/{MaxRetries})");
                
                bool reconciled = await reconciliationFunc();
                if (!reconciled)
                {
                    return (false, default, new BackendError { error = "RECONCILIATION_FAILED", message = "Failed to reconcile state with remote server." });
                }

                int postReconciliationRevision = currentRevisionProvider?.Invoke() ?? -1;
                if (currentRevisionProvider != null && sentRevision == postReconciliationRevision)
                {
                    UnityEngine.Debug.LogError($"[RemoteRetryPolicy] Revision did not advance after reconciliation (stuck at {postReconciliationRevision}). Aborting retries.");
                    return (false, default, new BackendError { error = "REVISION_NOT_ADVANCING", message = "Revision did not change after reconciliation." });
                }

                retries++;
                continue;
            }

            return (false, default, error);
        }

        return (false, default, new BackendError { error = "MAX_RETRIES_EXCEEDED", message = "Exceeded maximum retries for operation." });
    }
}

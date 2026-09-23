using System;
using System.Threading.Tasks;
using Grimhold.Backend;

public static class RemoteIdempotentRetryPolicy
{
    public const int MaxRetries = 3;
    public const int BackoffMs = 1000;

    /// <summary>
    /// Executes an idempotent remote operation. If a REVISION_CONFLICT or Transport Failure occurs, 
    /// it retries up to MaxRetries.
    /// Do NOT use this for non-idempotent endpoints or endpoints that rely on expectedRevision matching.
    /// </summary>
    public static async Task<(bool success, T result, BackendError error)> ExecuteWithRetryAsync<T>(
        Func<Task<(bool success, T result, BackendError error)>> operation)
    {
        int retries = 0;
        while (retries < MaxRetries)
        {
            var (success, result, error) = await operation();
            if (success)
            {
                return (true, result, default);
            }

            if (error.error == "REVISION_CONFLICT" || BackendErrorUtility.IsTransportFailure(error.error))
            {
                retries++;
                if (retries >= MaxRetries)
                {
                    UnityEngine.Debug.LogError($"[RemoteIdempotentRetryPolicy] Max retries reached for error {error.error}. Aborting.");
                    return (false, default, error);
                }

                UnityEngine.Debug.LogWarning($"[RemoteIdempotentRetryPolicy] {error.error} detected. Retrying {retries}/{MaxRetries} in {BackoffMs}ms...");
                await Task.Delay(BackoffMs);
                continue;
            }

            // Fatal error, do not retry
            return (false, default, error);
        }

        return (false, default, new BackendError { error = "MAX_RETRIES_EXCEEDED", message = "Exceeded maximum retries for operation." });
    }

    public static async Task<(bool success, BackendError error)> ExecuteWithRetryAsync(
        Func<Task<(bool success, BackendError error)>> operation)
    {
        var (success, result, error) = await ExecuteWithRetryAsync(
            async () =>
            {
                var (opSuccess, opError) = await operation();
                return (opSuccess, true, opError);
            });

        return (success, error);
    }
}

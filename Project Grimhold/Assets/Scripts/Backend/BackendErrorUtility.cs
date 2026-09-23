using UnityEngine;

namespace Grimhold.Backend
{
    public static class BackendErrorUtility
    {
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "TIMEOUT";
        public const string RequestCancelled = "REQUEST_CANCELLED";

        public static string ClassifyConnectionError(string requestError)
        {
            if (string.IsNullOrEmpty(requestError))
                return NetworkError;

            var lower = requestError.ToLowerInvariant();
            if (lower.Contains("timeout")) return Timeout;
            if (lower.Contains("abort") || lower.Contains("cancel")) return RequestCancelled;
            
            return NetworkError;
        }

        public static bool IsTransportFailure(string errorCode)
        {
            return errorCode == NetworkError || errorCode == Timeout || errorCode == RequestCancelled;
        }
    }
}

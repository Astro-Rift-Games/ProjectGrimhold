using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Grimhold.Backend
{
    /// <summary>
    /// HTTP client for the progression persistence endpoints.
    /// </summary>
    public static class ProgressionClient
    {
        // ------------------------------------------------------------------
        // GET progression — called at login to hydrate the local Unity state
        // ------------------------------------------------------------------

        public static async Task<(bool success, ProgressionData data, BackendError error)>
            GetProgressionAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me/progression";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            return ProcessResponse<ProgressionData>(request);
        }

        // ------------------------------------------------------------------
        // Commit progression
        // ------------------------------------------------------------------

        public static async Task<(bool success, CommitProgressionResult data, BackendError error)>
            CommitProgressionAsync(BackendConfiguration config, string token, CommitProgressionRequest request)
        {
            var url = $"{config.BaseUrl}/character/me/progression/commit";
            return await PostJson<CommitProgressionRequest, CommitProgressionResult>(config, token, url, request);
        }

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------

        private static async Task<(bool success, TResult data, BackendError error)>
            PostJson<TRequest, TResult>(BackendConfiguration config, string token, string url, TRequest body)
        {
            var json = JsonUtility.ToJson(body);
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            return ProcessResponse<TResult>(request);
        }

        private static (bool success, T data, BackendError error) ProcessResponse<T>(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                var result = JsonUtility.FromJson<T>(request.downloadHandler.text);
                return (true, result, default);
            }

            var err = ParseError(request);
            return (false, default, err);
        }

        private static BackendError ParseError(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(request.downloadHandler?.text))
            {
                try   { return JsonUtility.FromJson<BackendError>(request.downloadHandler.text); }
                catch { /* fall through */ }
            }
            return new BackendError { error = "NETWORK_ERROR", message = request.error };
        }
    }
}

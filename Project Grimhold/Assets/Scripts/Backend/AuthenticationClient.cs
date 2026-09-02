using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Grimhold.Backend
{
    public static class AuthenticationClient
    {
        public static async Task<(bool success, LoginResult result, BackendError error)> PostLoginAsync(BackendConfiguration config, string username, string password)
        {
            var url = $"{config.BaseUrl}/auth/login";
            var requestData = new LoginRequest { username = username, password = password };
            var json = JsonUtility.ToJson(requestData);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                var result = JsonUtility.FromJson<LoginResult>(request.downloadHandler.text);
                return (true, result, default);
            }
            else
            {
                BackendError backendError = default;
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try
                    {
                        backendError = JsonUtility.FromJson<BackendError>(request.downloadHandler.text);
                    }
                    catch
                    {
                        backendError = new BackendError { error = "UNKNOWN", message = request.error };
                    }
                }
                else
                {
                    backendError = new BackendError { error = "NETWORK_ERROR", message = request.error };
                }
                return (false, default, backendError);
            }
        }
        public static async Task<(bool success, LoginResult result, BackendError error)> PostRegisterAsync(BackendConfiguration config, string username, string password)
        {
            var url = $"{config.BaseUrl}/auth/register";
            var requestData = new LoginRequest { username = username, password = password };
            var json = JsonUtility.ToJson(requestData);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                var result = JsonUtility.FromJson<LoginResult>(request.downloadHandler.text);
                return (true, result, default);
            }
            else
            {
                BackendError backendError = default;
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try
                    {
                        backendError = JsonUtility.FromJson<BackendError>(request.downloadHandler.text);
                    }
                    catch
                    {
                        backendError = new BackendError { error = "UNKNOWN", message = request.error };
                    }
                }
                else
                {
                    backendError = new BackendError { error = "NETWORK_ERROR", message = request.error };
                }
                return (false, default, backendError);
            }
        }

        public static async Task<bool> PostLogoutAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/auth/logout";
            using var request = new UnityWebRequest(url, "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[{nameof(AuthenticationClient)}] Logout failed (network or server error), proceeding with local logout. Error: {request.error}");
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Grimhold.Backend
{
    public static class CharacterClient
    {
        public static async Task<(bool success, CharacterData data, BackendError error)> GetCharacterAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return ProcessResponse<CharacterData>(request);
        }

        public static async Task<(bool success, CharacterProfileData data, BackendError error)> GetProfileAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me/profile";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return ProcessResponse<CharacterProfileData>(request);
        }

        public static async Task<(bool success, CharacterProfileData data, BackendError error)> PatchProfileAsync(BackendConfiguration config, string token, string customNote)
        {
            var url = $"{config.BaseUrl}/character/me/profile";
            var requestData = new PatchProfileRequest { customNote = customNote };
            var json = JsonUtility.ToJson(requestData);

            using var request = new UnityWebRequest(url, "PATCH");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return ProcessResponse<CharacterProfileData>(request);
        }

        public static async Task<(bool success, CreateCharacterResult data, BackendError error)> PostCreateCharacterAsync(BackendConfiguration config, string token, string name)
        {
            var url = $"{config.BaseUrl}/character/me";
            var requestData = new CreateCharacterRequest { name = name };
            var json = JsonUtility.ToJson(requestData);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return ProcessResponse<CreateCharacterResult>(request);
        }

        public static async Task<(bool success, TownSessionTicketResponse data, BackendError error)> GetTownSessionTicketAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me/session-ticket/town";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return ProcessResponse<TownSessionTicketResponse>(request);
        }

        public static async Task<(bool success, VerifyTownTicketResponse data, BackendError error)> VerifyTownSessionTicketAsync(BackendConfiguration config, string token, string rawTicket)
        {
            var url = $"{config.BaseUrl}/character/verify-session-ticket";
            var body = new VerifyTownTicketRequest { ticket = rawTicket };
            var json = JsonUtility.ToJson(body);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return ProcessResponse<VerifyTownTicketResponse>(request);
        }

        public static async Task<(bool success, AuthoritativeRaidAdmissionTicketResponse data, BackendError error)> GetAuthoritativeRaidAdmissionTicketAsync(
            BackendConfiguration config,
            string token,
            string raidCode,
            string reservationId)
        {
            var url = $"{config.BaseUrl}/character/me/raid/admission-ticket";
            var body = new IssueRaidAdmissionTicketRequest { raidCode = raidCode, reservationId = reservationId };
            var json = JsonUtility.ToJson(body);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return ProcessResponse<AuthoritativeRaidAdmissionTicketResponse>(request);
        }

        public static async Task<(bool success, VerifyRaidAdmissionTicketResponse data, BackendError error)> VerifyAuthoritativeRaidAdmissionTicketAsync(
            BackendConfiguration config,
            string token,
            string rawTicket,
            string raidCode)
        {
            var url = $"{config.BaseUrl}/character/raid/verify-admission";
            var body = new VerifyRaidAdmissionTicketRequest { ticket = rawTicket, raidCode = raidCode };
            var json = JsonUtility.ToJson(body);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return ProcessResponse<VerifyRaidAdmissionTicketResponse>(request);
        }

        private static (bool success, T data, BackendError error) ProcessResponse<T>(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                var result = JsonUtility.FromJson<T>(request.downloadHandler.text);
                return (true, result, default);
            }
            
            BackendError backendError = default;
            if (!string.IsNullOrEmpty(request.downloadHandler?.text))
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
                backendError = new BackendError { error = BackendErrorUtility.ClassifyConnectionError(request.error), message = request.error };
            }
            return (false, default, backendError);
        }
    }
}

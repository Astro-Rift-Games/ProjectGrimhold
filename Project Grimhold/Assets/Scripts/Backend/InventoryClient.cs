using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Grimhold.Backend
{
    /// <summary>
    /// HTTP client for the inventory persistence endpoints.
    ///
    /// All methods follow the same (bool success, T data, BackendError error) tuple
    /// convention used by AuthenticationClient and CharacterClient.
    /// </summary>
    public static class InventoryClient
    {
        // ------------------------------------------------------------------
        // GET inventory — called at login to hydrate the local Unity state
        // ------------------------------------------------------------------

        /// <summary>
        /// Fetches the full inventory snapshot (stash, loadout, preparedEquipment,
        /// pendingReservation) for the authenticated character.
        /// </summary>
        public static async Task<(bool success, InventoryData data, BackendError error)>
            GetInventoryAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me/inventory";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            return ProcessResponse<InventoryData>(request);
        }

        // ------------------------------------------------------------------
        // Move operations — triggered by player UI actions (save-on-move)
        // ------------------------------------------------------------------

        /// <summary>
        /// Moves <paramref name="amount"/> units of <paramref name="lootId"/> from the
        /// stash to the loadout. Returns the updated stash and loadout on success.
        /// </summary>
        public static async Task<(bool success, MoveItemResult data, BackendError error)>
            MoveToLoadoutAsync(BackendConfiguration config, string token, string lootId, int amount)
        {
            var url  = $"{config.BaseUrl}/character/me/inventory/stash/move-to-loadout";
            var body = new MoveItemRequest { lootId = lootId, amount = amount };
            return await PostJson<MoveItemRequest, MoveItemResult>(config, token, url, body);
        }

        /// <summary>
        /// Moves <paramref name="amount"/> units of <paramref name="lootId"/> from the
        /// loadout to the stash. Returns the updated stash and loadout on success.
        /// </summary>
        public static async Task<(bool success, MoveItemResult data, BackendError error)>
            MoveToStashAsync(BackendConfiguration config, string token, string lootId, int amount)
        {
            var url  = $"{config.BaseUrl}/character/me/inventory/loadout/move-to-stash";
            var body = new MoveItemRequest { lootId = lootId, amount = amount };
            return await PostJson<MoveItemRequest, MoveItemResult>(config, token, url, body);
        }

        // ------------------------------------------------------------------
        // Equipment slots
        // ------------------------------------------------------------------

        /// <summary>
        /// Replaces all six equipment slot assignments atomically.
        /// Each slot must reference a lootId present in the loadout, or be empty.
        /// </summary>
        public static async Task<(bool success, UpdatePreparedEquipmentResult data, BackendError error)>
            UpdatePreparedEquipmentAsync(BackendConfiguration config, string token, UpdatePreparedEquipmentRequest slots)
        {
            var url = $"{config.BaseUrl}/character/me/inventory/prepared-equipment";
            return await PutJson<UpdatePreparedEquipmentRequest, UpdatePreparedEquipmentResult>(config, token, url, slots);
        }

        // ------------------------------------------------------------------
        // Raid reservation (disconnection recovery)
        // ------------------------------------------------------------------

        /// <summary>
        /// Persists a raid reservation snapshot so Unity can recover the correct
        /// inventory state after a disconnection.
        /// </summary>
        public static async Task<(bool success, SaveReservationResult data, BackendError error)>
            SavePendingReservationAsync(BackendConfiguration config, string token, SaveReservationRequest reservation)
        {
            var url = $"{config.BaseUrl}/character/me/inventory/reservation";
            return await PostJson<SaveReservationRequest, SaveReservationResult>(config, token, url, reservation);
        }

        /// <summary>
        /// Clears the pending reservation once a raid completes or the player exits voluntarily.
        /// </summary>
        public static async Task<(bool success, BackendError error)>
            ClearPendingReservationAsync(BackendConfiguration config, string token)
        {
            var url = $"{config.BaseUrl}/character/me/inventory/reservation";
            using var request = UnityWebRequest.Delete(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            // DELETE needs a download handler to read the response body.
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = config.TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return (true, default);
            }

            var err = ParseError(request);
            return (false, err);
        }

        // ------------------------------------------------------------------
        // Extraction loot commit
        // ------------------------------------------------------------------

        /// <summary>
        /// Persists the loot items from a successful raid extraction to the backend loadout.
        /// Idempotent: replaying the same (raidId, resultSequence) returns alreadySecured = true.
        /// </summary>
        public static async Task<(bool success, CommitExtractionResult data, BackendError error)>
            CommitExtractionAsync(BackendConfiguration config, string token, CommitExtractionRequest request)
        {
            var url = $"{config.BaseUrl}/character/me/inventory/extraction";
            return await PostJson<CommitExtractionRequest, CommitExtractionResult>(config, token, url, request);
        }

        // ------------------------------------------------------------------
        // Shared helpers (mirror the pattern in CharacterClient)
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

        private static async Task<(bool success, TResult data, BackendError error)>
            PutJson<TRequest, TResult>(BackendConfiguration config, string token, string url, TRequest body)
        {
            var json = JsonUtility.ToJson(body);
            using var request = new UnityWebRequest(url, "PUT");
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

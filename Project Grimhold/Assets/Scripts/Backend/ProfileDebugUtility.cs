using Grimhold.Backend;
using UnityEngine;

/// <summary>
/// Utility to test bidirectional persistence of the Character Profile customNote.
/// Uses the active ApplicationAuthContext token.
/// </summary>
public sealed class ProfileDebugUtility : MonoBehaviour
{
    [SerializeField] private BackendConfiguration _config;
    
    [Header("Test Data")]
    [SerializeField] private string noteToWrite = "persistence-test-v1";

    private void Awake()
    {
        if (_config == null)
        {
            _config = ScriptableObject.CreateInstance<BackendConfiguration>();
            Debug.LogWarning($"[{nameof(ProfileDebugUtility)}] No BackendConfiguration assigned. Using defaults.");
        }
    }

    [ContextMenu("Test: Save Custom Note")]
    public async void WriteTestNote()
    {
        var authContext = ApplicationAuthContext.Instance;
        if (authContext == null || !authContext.IsAuthenticated)
        {
            Debug.LogError($"[{nameof(ProfileDebugUtility)}] Cannot write note: Not authenticated.");
            return;
        }

        Debug.Log($"[{nameof(ProfileDebugUtility)}] Sending PATCH with customNote: '{noteToWrite}'...");

        var (success, data, error) = await CharacterClient.PatchProfileAsync(
            _config, 
            authContext.Token, 
            noteToWrite);

        if (success)
        {
            // Update the local instance to reflect the new state.
            authContext.UpdateProfile(data.profile);

            Debug.Log($"[{nameof(ProfileDebugUtility)}] PATCH Successful!");
            Debug.Log($"[{nameof(ProfileDebugUtility)}] New customNote: {data.profile.customNote}");
            Debug.Log($"[{nameof(ProfileDebugUtility)}] New lastSeen: {data.profile.lastSeen}");
        }
        else
        {
            Debug.LogError($"[{nameof(ProfileDebugUtility)}] PATCH Failed! Error: {error.error} - {error.message}");
        }
    }
}

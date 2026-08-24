using UnityEngine;
using Grimhold.Backend;

public class BackendTestBehaviour : MonoBehaviour
{
    [SerializeField] private BackendConfiguration _config;
    [SerializeField] private string _username = "tester_a";
    [SerializeField] private string _password = "test1234";
    [SerializeField] private string _testNote = "test-note-etapa4";

    private async void Start()
    {
        if (_config == null)
        {
            _config = ScriptableObject.CreateInstance<BackendConfiguration>();
        }

        Debug.Log("[BackendTest] 1. Starting login...");
        var loginResult = await AuthenticationClient.PostLoginAsync(_config, _username, _password);
        if (!loginResult.success)
        {
            Debug.LogError($"[BackendTest] Login failed: {loginResult.error.error} - {loginResult.error.message}");
            return;
        }

        var token = loginResult.result.token;
        Debug.Log($"[BackendTest] Login successful. Token: {token.Substring(0, 15)}...");

        Debug.Log("[BackendTest] 2. Getting character...");
        var charResult = await CharacterClient.GetCharacterAsync(_config, token);
        if (!charResult.success)
        {
            Debug.LogError($"[BackendTest] GetCharacter failed: {charResult.error.error}");
            return;
        }

        Debug.Log($"[BackendTest] Character retrieved: {charResult.data.name} (ID: {charResult.data.characterId})");

        Debug.Log("[BackendTest] 3. Setting LocalProfileProvider...");
        LocalProfileProvider.SetRemoteCharacterId(new ProfileId(charResult.data.characterId));

        var localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        Debug.Log($"[BackendTest] 4. LocalProfileProvider ID: {localProfile.Value} (Match? {localProfile.Value == charResult.data.characterId})");

        Debug.Log("[BackendTest] 5. Patching profile...");
        var patchResult = await CharacterClient.PatchProfileAsync(_config, token, _testNote);
        if (!patchResult.success)
        {
            Debug.LogError($"[BackendTest] PatchProfile failed: {patchResult.error.error}");
            return;
        }

        Debug.Log($"[BackendTest] 6. Profile patched! New Note: {patchResult.data.profile.customNote}");
    }
}

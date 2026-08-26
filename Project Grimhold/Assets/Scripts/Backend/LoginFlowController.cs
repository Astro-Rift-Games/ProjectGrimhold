using System.Threading.Tasks;
using Grimhold.Backend;
using UnityEngine;

public enum LoginFlowStatus
{
    Success,
    AuthFailed,
    CharacterFailed,
    NetworkError
}

public readonly struct LoginFlowResult
{
    public LoginFlowStatus Status { get; }
    public bool IsSuccess => Status == LoginFlowStatus.Success;
    public string ErrorMessage { get; }

    private LoginFlowResult(LoginFlowStatus status, string errorMessage)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public static LoginFlowResult Success() => new(LoginFlowStatus.Success, null);
    public static LoginFlowResult Failure(LoginFlowStatus status, string message) => new(status, message);
}

/// <summary>
/// Orchestrates the full login sequence:
/// POST login → GET character → GET profile → inject identity → initialize stash.
/// Has no direct dependency on UI; results are returned to the caller.
/// </summary>
public sealed class LoginFlowController : MonoBehaviour
{
    [SerializeField] private BackendConfiguration _config;
    [SerializeField] private ApplicationAuthContext _authContext;

    private void Awake()
    {
        if (_config == null)
        {
            _config = ScriptableObject.CreateInstance<BackendConfiguration>();
            Debug.LogWarning($"[{nameof(LoginFlowController)}] No BackendConfiguration assigned. Using defaults.");
        }

        if (_authContext == null)
        {
            _authContext = ApplicationAuthContext.Instance ?? FindAnyObjectByType<ApplicationAuthContext>();
            if (_authContext == null)
            {
                var authObj = new GameObject(nameof(ApplicationAuthContext));
                _authContext = authObj.AddComponent<ApplicationAuthContext>();
            }
        }
    }

    public async Task<LoginFlowResult> ExecuteAsync(string username, string password)
    {
        // Clear any previous identity before attempting a new login.
        // This guarantees no stale CharacterId survives a failed attempt.
        LocalProfileProvider.ClearRemoteCharacterId();
        _authContext?.Clear();

        // Step 1: Authenticate
        var (loginOk, loginResult, loginError) = await AuthenticationClient.PostLoginAsync(_config, username, password);
        if (!loginOk)
        {
            var isNetwork = loginError.error == "NETWORK_ERROR";
            var message = isNetwork
                ? "Cannot reach the server. Check your connection."
                : "Invalid username or password.";
            return LoginFlowResult.Failure(
                isNetwork ? LoginFlowStatus.NetworkError : LoginFlowStatus.AuthFailed,
                message);
        }

        var token = loginResult.token;

        // Step 2: Fetch character identity
        var (charOk, charData, charError) = await CharacterClient.GetCharacterAsync(_config, token);
        if (!charOk)
        {
            return LoginFlowResult.Failure(LoginFlowStatus.CharacterFailed,
                "Login succeeded but character data could not be loaded.");
        }

        // Step 3: Fetch profile snapshot
        var (profileOk, profileData, _) = await CharacterClient.GetProfileAsync(_config, token);
        // Profile fetch failure is non-fatal; we proceed with an empty profile snapshot.
        if (!profileOk)
        {
            Debug.LogWarning($"[{nameof(LoginFlowController)}] Profile fetch failed. Proceeding with empty profile.");
        }

        // Step 4: Inject identity into local systems
        var characterId = new ProfileId(charData.characterId);
        LocalProfileProvider.SetRemoteCharacterId(characterId);

        if (_authContext != null)
        {
            _authContext.Initialize(token, charData, profileData);
        }

        // Step 5: Initialize the stash with the now-valid ProfileId
        ApplicationStashServiceBootstrapper.InitializeWithProfile(characterId);

        return LoginFlowResult.Success();
    }
}

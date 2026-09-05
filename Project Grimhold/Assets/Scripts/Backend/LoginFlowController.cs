using System.Threading.Tasks;
using Grimhold.Backend;
using UnityEngine;

public enum LoginFlowStatus
{
    Success,
    AuthFailed,
    CharacterFailed,
    NetworkError,
    NeedsCharacterCreation,
    RegistrationFailed,
    CharacterCreationFailed
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
    public static LoginFlowController Instance { get; private set; }

    [SerializeField] private BackendConfiguration _config;
    public BackendConfiguration Config => _config;

    [SerializeField] private ApplicationAuthContext _authContext;

    private string _pendingToken;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Detach from any scene hierarchy so DontDestroyOnLoad works on the root.
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        DontDestroyOnLoad(gameObject);

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public async Task<LoginFlowResult> ExecuteLoginAsync(string username, string password)
    {
        ClearState();

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

        return await CompleteAuthenticationAndInjectIdentity(loginResult.token);
    }

    public async Task<LoginFlowResult> ExecuteRegisterAsync(string username, string password)
    {
        ClearState();

        var (registerOk, loginResult, registerError) = await AuthenticationClient.PostRegisterAsync(_config, username, password);
        if (!registerOk)
        {
            var isNetwork = registerError.error == "NETWORK_ERROR";
            if (isNetwork)
            {
                return LoginFlowResult.Failure(LoginFlowStatus.NetworkError, "Cannot reach the server. Check your connection.");
            }

            string message = "Failed to register account.";
            if (registerError.error == "USERNAME_TAKEN")
            {
                message = "Username is already taken.";
            }
            else if (registerError.error == "VALIDATION_FAILED")
            {
                message = "Invalid username or password format (e.g. no special characters like '_', minimum 6 chars for password).";
            }

            return LoginFlowResult.Failure(LoginFlowStatus.RegistrationFailed, message);
        }

        return await CompleteAuthenticationAndInjectIdentity(loginResult.token);
    }

    public async Task<LoginFlowResult> CreateCharacterAsync(string name)
    {
        if (string.IsNullOrEmpty(_pendingToken))
        {
            return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "No authentication token available.");
        }

        var (ok, data, err) = await CharacterClient.PostCreateCharacterAsync(_config, _pendingToken, name);
        if (!ok)
        {
            return LoginFlowResult.Failure(LoginFlowStatus.CharacterCreationFailed, err.message ?? "Failed to create character.");
        }

        var result = await CompleteAuthenticationAndInjectIdentity(_pendingToken);
        if (result.IsSuccess)
        {
            _pendingToken = null;
        }
        return result;
    }

    private void ClearState()
    {
        _pendingToken = null;
        LocalProfileProvider.ClearRemoteCharacterId();
        _authContext?.Clear();
    }

    private async Task<LoginFlowResult> CompleteAuthenticationAndInjectIdentity(string token)
    {
        // Step 2: Fetch character identity
        var (charOk, charData, charError) = await CharacterClient.GetCharacterAsync(_config, token);
        if (!charOk)
        {
            if (charError.error == "CHARACTER_NOT_FOUND")
            {
                _pendingToken = token;
                return LoginFlowResult.Failure(LoginFlowStatus.NeedsCharacterCreation, "Account has no character.");
            }
            return LoginFlowResult.Failure(LoginFlowStatus.CharacterFailed,
                "Login succeeded but character data could not be loaded.");
        }

        // Step 3: Fetch profile snapshot
        var (profileOk, profileData, _) = await CharacterClient.GetProfileAsync(_config, token);
        if (!profileOk)
        {
            Debug.LogWarning($"[{nameof(LoginFlowController)}] Profile fetch failed. Proceeding with empty profile.");
        }

        // Step 4: Fetch inventory snapshot
        InventoryData? inventoryData = null;
        var (invOk, invData, _) = await InventoryClient.GetInventoryAsync(_config, token);
        if (invOk)
        {
            inventoryData = invData;
        }
        else
        {
            Debug.LogWarning($"[{nameof(LoginFlowController)}] Inventory fetch failed. Proceeding with empty inventory.");
        }

        // Step 4b: Fetch progression snapshot
        ProgressionData? progressionData = null;
        var (progOk, progData, _) = await ProgressionClient.GetProgressionAsync(_config, token);
        if (progOk)
        {
            progressionData = progData;
        }
        else
        {
            Debug.LogWarning($"[{nameof(LoginFlowController)}] Progression fetch failed. Proceeding with defaults.");
        }

        // Step 5: Inject identity into local systems
        var characterId = new ProfileId(charData.characterId);
        LocalProfileProvider.SetRemoteCharacterId(characterId);

        if (_authContext != null)
        {
            _authContext.Initialize(token, charData, profileData);
        }

        // Step 6: Initialize the stash with the now-valid ProfileId and hydrated inventory
        ApplicationStashServiceBootstrapper.InitializeWithProfile(characterId, inventoryData, progressionData);

        return LoginFlowResult.Success();
    }

    public async Task<bool> ExecuteLogoutAsync()
    {
        var token = _authContext?.Token ?? _pendingToken;
        if (!string.IsNullOrEmpty(token))
        {
            await AuthenticationClient.PostLogoutAsync(_config, token);
        }

        ApplicationStashServiceBootstrapper.ResetForLogout();
        ClearState();
        return true;
    }
}

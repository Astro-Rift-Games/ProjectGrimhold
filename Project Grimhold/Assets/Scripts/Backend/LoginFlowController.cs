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
    CharacterCreationFailed,
    HydrationFailed
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

    public string PendingUsername => _pendingUsername;
    public bool HasHydrationFailed => _hydrationFailed;

    private string _pendingToken;
    private string _pendingUsername;
    private bool _hydrationFailed;

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
            if (BackendErrorUtility.IsTransportFailure(loginError.error))
            {
                return LoginFlowResult.Failure(LoginFlowStatus.NetworkError, "Cannot reach the server. Check your connection.");
            }
            return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "Invalid username or password.");
        }

        return await CompleteAuthenticationAndInjectIdentity(loginResult.token, username);
    }

    public async Task<LoginFlowResult> ExecuteRegisterAsync(string username, string password)
    {
        ClearState();

        var (registerOk, loginResult, registerError) = await AuthenticationClient.PostRegisterAsync(_config, username, password);
        if (!registerOk)
        {
            if (BackendErrorUtility.IsTransportFailure(registerError.error))
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

        return await CompleteAuthenticationAndInjectIdentity(loginResult.token, username);
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

        var result = await CompleteAuthenticationAndInjectIdentity(_pendingToken, _pendingUsername);
        if (result.IsSuccess)
        {
            _pendingToken = null;
            _pendingUsername = null;
        }
        return result;
    }

    public async Task<LoginFlowResult> RetryHydrationAsync()
    {
        if (string.IsNullOrEmpty(_pendingToken))
        {
            return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "No authentication token available.");
        }
        return await CompleteAuthenticationAndInjectIdentity(_pendingToken, _pendingUsername);
    }

    private void ClearState()
    {
        _pendingToken = null;
        _pendingUsername = null;
        _hydrationFailed = false;
        LocalProfileProvider.ClearRemoteCharacterId();
        _authContext?.Clear();
    }

    private async Task<(bool invOk, InventoryData invData, BackendError invError, bool progOk, ProgressionData progData, BackendError progError)> FetchInventoryAndProgressionAsync(string token)
    {
        var inventoryTask = InventoryClient.GetInventoryAsync(_config, token);
        var progressionTask = ProgressionClient.GetProgressionAsync(_config, token);

        await Task.WhenAll(inventoryTask, progressionTask);

        var invResult = inventoryTask.Result;
        var progResult = progressionTask.Result;

        return (invResult.success, invResult.data, invResult.error, progResult.success, progResult.data, progResult.error);
    }

    private async Task<LoginFlowResult> CompleteAuthenticationAndInjectIdentity(string token, string username, bool isRetry = false)
    {
        // Step 2: Fetch character identity
        var (charOk, charData, charError) = await CharacterClient.GetCharacterAsync(_config, token);
        if (!charOk)
        {
            if (charError.error == "CHARACTER_NOT_FOUND")
            {
                _pendingToken = token;
                _pendingUsername = username;
                _hydrationFailed = false;
                return LoginFlowResult.Failure(LoginFlowStatus.NeedsCharacterCreation, "Account has no character.");
            }
            if (charError.error == "UNAUTHORIZED")
            {
                ClearState();
                return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "Session expired.");
            }
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginHydrationFailureClassifier.ClassifyHydrationFailure(charError, "character profile");
        }

        // Step 3: Fetch profile snapshot
        var (profileOk, profileData, profileError) = await CharacterClient.GetProfileAsync(_config, token);
        if (!profileOk)
        {
            if (profileError.error == "UNAUTHORIZED")
            {
                ClearState();
                return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "Session expired.");
            }
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginHydrationFailureClassifier.ClassifyHydrationFailure(profileError, "character profile");
        }

        // Step 4: Fetch inventory and progression snapshots
        var (invOk, invData, invError, progOk, progData, progError) = await FetchInventoryAndProgressionAsync(token);

        if (invOk && progOk && invData.revision != progData.revision && !isRetry)
        {
            Debug.LogWarning($"[{nameof(LoginFlowController)}] Hydration revision mismatch ({invData.revision} vs {progData.revision}). Retrying once...");
            (invOk, invData, invError, progOk, progData, progError) = await FetchInventoryAndProgressionAsync(token);
        }

        if (!invOk)
        {
            if (invError.error == "UNAUTHORIZED") { ClearState(); return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "Session expired."); }
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginHydrationFailureClassifier.ClassifyHydrationFailure(invError, "inventory");
        }

        if (!progOk)
        {
            if (progError.error == "UNAUTHORIZED") { ClearState(); return LoginFlowResult.Failure(LoginFlowStatus.AuthFailed, "Session expired."); }
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginHydrationFailureClassifier.ClassifyHydrationFailure(progError, "progression");
        }

        if (invData.revision != progData.revision)
        {
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginHydrationFailureClassifier.ClassifyRevisionMismatch(invData.revision, progData.revision);
        }

        // Step 5: Inject identity into local systems.
        var characterId = new ProfileId(charData.characterId);
        LocalProfileProvider.SetRemoteCharacterId(characterId);

        if (_authContext != null)
        {
            _authContext.Initialize(token, charData, profileData);
        }

        // Step 6: Initialize the stash with the now-valid ProfileId and hydrated data.
        bool initialized = ApplicationStashServiceBootstrapper.InitializeWithProfile(characterId, invData, progData);
        if (!initialized)
        {
            LocalProfileProvider.ClearRemoteCharacterId();
            _authContext?.Clear();
            _pendingToken = token;
            _pendingUsername = username;
            _hydrationFailed = true;
            return LoginFlowResult.Failure(LoginFlowStatus.HydrationFailed, "Failed to load character data. Please try again.");
        }

        _pendingToken = null;
        _pendingUsername = null;
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

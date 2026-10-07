using Fusion;
using UnityEngine;

public sealed class FusionInputProvider : NetworkRunnerCallbacksAdapter
{
    [SerializeField]
    private PlayerInputReader _inputReader;

    private NetworkRunner _runner;
    private LocalInputContext _inputContext;
    private bool _callbacksRegistered;
    private float _findRunnerTimer;

    private void Start()
    {
        TryFindAndRegisterRunner();
    }

    private void Update()
    {
        if (!_callbacksRegistered || _runner == null || !_runner.IsRunning)
        {
            _findRunnerTimer -= Time.deltaTime;
            if (_findRunnerTimer <= 0f)
            {
                _findRunnerTimer = 0.5f;
                TryFindAndRegisterRunner();
            }
        }
    }

    private void TryFindAndRegisterRunner()
    {
        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
        NetworkRunner activeRunner = null;
        foreach (var r in runners)
        {
            if (r != null && r.IsRunning)
            {
                activeRunner = r;
                break;
            }
        }

        if (activeRunner == null)
        {
            return;
        }

        if (_runner == activeRunner)
        {
            return;
        }

        ReleaseRunnerRegistration();

        _runner = activeRunner;
        _runner.AddCallbacks(this);
        _callbacksRegistered = true;

        _inputContext = _runner.GetComponent<LocalInputContext>();
        if (_inputContext == null)
        {
            Debug.LogError($"{nameof(LocalInputContext)} was not found on the active runner.", this);
            return;
        }

        if (_inputReader != null && !_inputContext.TryRegister(_inputReader))
        {
            Debug.LogError($"{nameof(FusionInputProvider)} could not register its local input reader.", this);
        }
    }

    private void OnDestroy()
    {
        ReleaseRunnerRegistration();
    }

    public override void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        if (_inputReader == null)
        {
            input.Set(default(PlayerNetworkInput));
            return;
        }

        PlayerNetworkInput networkInput =
            _inputReader.ConsumeNetworkInput();

        input.Set(networkInput);
    }

    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner != _runner)
        {
            return;
        }

        ReleaseRunnerRegistration();
    }

    private void ReleaseRunnerRegistration()
    {
        if (_inputContext != null)
        {
            _inputContext.TryUnregister(_inputReader);
            _inputContext = null;
        }

        if (_callbacksRegistered && _runner != null)
        {
            _runner.RemoveCallbacks(this);
            _callbacksRegistered = false;
        }

        _runner = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_inputReader == null)
        {
            _inputReader =
                FindFirstObjectByType<PlayerInputReader>();
        }
    }
#endif
}

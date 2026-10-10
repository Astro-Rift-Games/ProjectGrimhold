using Fusion;
using UnityEngine;

/// <summary>
/// Projects the replicated Dungeon Pressure timer and phase into the local HUD.
/// Bound by <see cref="LocalPlayerHudBinder"/>; resolves the controller through the
/// runner's <see cref="NetworkSpawnManager"/> instead of scanning the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonPressureHudPresenter : MonoBehaviour
{
    [SerializeField] private DungeonPressureHudView _view;

    [Header("Phase Colors")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _reinforcementsColor = Color.yellow;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color _collapseColor = Color.red;

    private NetworkSpawnManager _spawnManager;
    private NetworkRunner _runner;
    private DungeonPressureController _controller;
    private bool _isBound;

    private bool _hasPresented;
    private bool _presentedAvailable;
    private int _lastSeconds = -1;
    private DungeonPressurePhase _lastPhase;

    public void Bind(NetworkRunner runner, NetworkSpawnManager spawnManager)
    {
        Unbind();

        if (runner == null || spawnManager == null)
        {
            return;
        }

        _runner = runner;
        _spawnManager = spawnManager;
        _isBound = true;
        Refresh();
    }

    public void Unbind()
    {
        _spawnManager = null;
        _runner = null;
        _controller = null;
        _isBound = false;
        ResetPresentedState();
        if (_view != null)
        {
            _view.PresentUnavailable();
        }
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        ResetPresentedState();
        Refresh();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (_isBound)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            Unbind();
            return;
        }

        if (!IsValidController(_controller) && !TryResolveController())
        {
            Present(false, 0, DungeonPressurePhase.Normal);
            return;
        }

        Present(true, _controller.RemainingSeconds, _controller.Phase);
    }

    private bool TryResolveController()
    {
        _controller = null;
        NetworkMatchController match = _spawnManager != null ? _spawnManager.MatchController : null;
        if (match == null)
        {
            return false;
        }

        DungeonPressureController candidate = match.GetComponent<DungeonPressureController>();
        if (!IsValidController(candidate))
        {
            return false;
        }

        _controller = candidate;
        return true;
    }

    private bool IsValidController(DungeonPressureController controller) =>
        controller != null && controller.Object != null && controller.Object.IsValid &&
        controller.Runner == _runner;

    internal void Present(bool hasSource, int remainingSeconds, DungeonPressurePhase phase)
    {
        if (_view == null)
        {
            return;
        }

        if (!hasSource)
        {
            if (_hasPresented && !_presentedAvailable)
            {
                return;
            }

            ResetPresentedState();
            _hasPresented = true;
            _presentedAvailable = false;
            _view.PresentUnavailable();
            return;
        }

        bool firstValidRead = !_hasPresented || !_presentedAvailable;
        if (firstValidRead || remainingSeconds != _lastSeconds)
        {
            _view.UpdateTimer(remainingSeconds);
            _lastSeconds = remainingSeconds;
        }

        if (firstValidRead || phase != _lastPhase)
        {
            _view.UpdatePhase(GetPhaseText(phase), GetPhaseColor(phase));
            _lastPhase = phase;
        }

        _hasPresented = true;
        _presentedAvailable = true;
    }

    private void ResetPresentedState()
    {
        _hasPresented = false;
        _presentedAvailable = false;
        _lastSeconds = -1;
        _lastPhase = DungeonPressurePhase.Normal;
    }

    internal static string GetPhaseText(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => "Exploración",
            DungeonPressurePhase.Reinforcements => "Refuerzos",
            DungeonPressurePhase.CriticalPressure => "Presión Crítica",
            DungeonPressurePhase.Collapse => "COLAPSO",
            _ => phase.ToString()
        };
    }

    private Color GetPhaseColor(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => _normalColor,
            DungeonPressurePhase.Reinforcements => _reinforcementsColor,
            DungeonPressurePhase.CriticalPressure => _criticalColor,
            DungeonPressurePhase.Collapse => _collapseColor,
            _ => Color.white
        };
    }
}

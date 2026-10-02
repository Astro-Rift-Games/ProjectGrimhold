using UnityEngine;

public class DungeonPressureHudPresenter : MonoBehaviour
{
    [SerializeField] private DungeonPressureHudView _view;

    [Header("Phase Colors")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _reinforcementsColor = Color.yellow;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color _collapseColor = Color.red;

    private DungeonPressureController _controller;
    private float _searchTimer = 0f;

    private int _lastSeconds = -1;
    private DungeonPressurePhase? _lastPhase = null;

    private void Update()
    {
        if (_view == null) return;

        // Automatically find the controller if it's missing (useful since it spawns dynamically)
        if (_controller == null)
        {
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                _controller = FindObjectOfType<DungeonPressureController>();
                _searchTimer = 1f; // Poll every 1 second if not found
            }
            if (_controller == null) return;
        }

        // Handle case where controller was despawned
        if (_controller.Object == null || !_controller.Object.IsValid)
        {
            _controller = null;
            return;
        }

        // Convert networked Ticks back to Seconds
        int currentSeconds = 0;
        if (_controller.Runner != null && _controller.Runner.DeltaTime > 0)
        {
            currentSeconds = Mathf.CeilToInt(_controller.RemainingTicks * _controller.Runner.DeltaTime);
        }

        if (currentSeconds != _lastSeconds)
        {
            _view.UpdateTimer(currentSeconds);
            _lastSeconds = currentSeconds;
        }

        DungeonPressurePhase currentPhase = _controller.Phase;
        if (currentPhase != _lastPhase)
        {
            Color phaseColor = GetPhaseColor(currentPhase);
            _view.UpdatePhase(currentPhase.ToString(), phaseColor);
            _lastPhase = currentPhase;
        }
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

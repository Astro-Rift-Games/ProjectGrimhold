#if UNITY_EDITOR
using UnityEngine;

public class DungeonPressureDebugOverlay : MonoBehaviour
{
    private DungeonPressureController _controller;
    private EnemyReinforcementDirector _director;
    private NetworkSpawnManager _spawnManager;
    private NetworkMatchController _matchController;

    private float _searchTimer;
    private bool _showOverlay = true;
    private bool _syncTimeScale = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var go = new GameObject("DungeonPressureDebugOverlay");
        go.AddComponent<DungeonPressureDebugOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _showOverlay = !_showOverlay;
        }

        if (_controller == null)
        {
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                _controller = FindObjectOfType<DungeonPressureController>();
                if (_controller != null)
                {
                    _director = _controller.GetComponent<EnemyReinforcementDirector>();
                    _matchController = _controller.GetComponent<NetworkMatchController>();
                    if (_controller.Runner != null)
                    {
                        _spawnManager = _controller.Runner.GetComponent<NetworkSpawnManager>();
                    }
                }
                _searchTimer = 1f;
            }
        }
        else if (_controller.Object == null || !_controller.Object.IsValid)
        {
            _controller = null;
            _director = null;
            _spawnManager = null;
            _matchController = null;
        }
    }

    private void OnDisable()
    {
        if (_syncTimeScale)
        {
            Time.timeScale = 1f;
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        float width = 360f;
        float height = 450f;
        float x = Mathf.Max(10f, Screen.width - width - 10f);
        float y = Mathf.Max(10f, Screen.height - height - 10f);

        GUILayout.BeginArea(new Rect(x, y, width, height), "Dungeon Pressure Debug (F9)", GUI.skin.window);

        if (_controller == null || _director == null)
        {
            GUILayout.Label("Waiting for DungeonPressureController...");
            GUILayout.Label("(Spawns when entering Raid match)");
        }
        else
        {
            if (_matchController != null)
            {
                GUILayout.Label($"Match Phase: {_matchController.Phase}");
            }
            GUILayout.Label($"Pressure State: {_controller.State}");
            GUILayout.Label($"Pressure Phase: {_controller.Phase}");
            GUILayout.Label($"Time Remaining: {_controller.RemainingSeconds}s ({_controller.RemainingTicks} ticks)");
            
            if (_spawnManager != null && _spawnManager.PopulationTracker != null)
            {
                var pop = _spawnManager.PopulationTracker;
                GUILayout.Label($"Active Total Pop: {pop.TotalActivePopulation}");
                GUILayout.Label($"Active Reinforcements: {pop.ActiveReinforcements}");
            }

            GUILayout.Space(8);
            GUILayout.Label($"Total Spawns Generated: {_director.TotalSpawnsGenerated}");
            GUILayout.Label($"Eval Timer: {_director.EditorEvaluationTimerTicks} ticks");
            GUILayout.Label($"Min Spawn Timer: {_director.EditorMinSpawnTimerTicks} ticks");
            GUILayout.Label($"Last Rejection: {_director.LastRejection.ToString()}");

            GUILayout.Space(8);
            
            bool isAuth = _controller.HasStateAuthority;
            if (!isAuth)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("(No State Authority - Commands Disabled)");
                GUI.color = Color.white;
            }

            GUI.enabled = isAuth;

            if (_controller.State == DungeonPressureState.NotStarted)
            {
                if (GUILayout.Button("Force Start Timer (State -> Running)"))
                {
                    _controller.ForceStartTimer();
                }
            }

            if (GUILayout.Button("Advance Phase (Instant)"))
            {
                _controller.AdvancePhase();
            }

            if (GUILayout.Button("Force Spawn Attempt"))
            {
                _director.ForceAttempt();
            }
            
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Timer Multiplier:", GUILayout.Width(100));
            _controller.EditorTimeMultiplier = GUILayout.HorizontalSlider(_controller.EditorTimeMultiplier, 1f, 10f);
            GUILayout.Label($"{_controller.EditorTimeMultiplier:F1}x", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            bool prevSync = _syncTimeScale;
            _syncTimeScale = GUILayout.Toggle(_syncTimeScale, "Sync Unity Time.timeScale with Multiplier");
            if (_syncTimeScale != prevSync || _syncTimeScale)
            {
                Time.timeScale = _syncTimeScale ? _controller.EditorTimeMultiplier : 1f;
            }

            GUI.enabled = true;
        }

        GUILayout.EndArea();
    }
}
#endif

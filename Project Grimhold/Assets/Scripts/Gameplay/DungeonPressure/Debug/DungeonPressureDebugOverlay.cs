#if UNITY_EDITOR
using UnityEngine;

public class DungeonPressureDebugOverlay : MonoBehaviour
{
    private DungeonPressureController _controller;
    private EnemyReinforcementDirector _director;
    private NetworkSpawnManager _spawnManager;

    private float _searchTimer;
    private bool _showOverlay = true;

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
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 400), "Dungeon Pressure Debug (F9)", GUI.skin.window);

        if (_controller == null || _director == null)
        {
            GUILayout.Label("Waiting for DungeonPressureController...");
        }
        else
        {
            GUILayout.Label($"Phase: {_controller.Phase}");
            GUILayout.Label($"Time Remaining: {_controller.RemainingSeconds}s");
            
            if (_spawnManager != null)
            {
                var pop = _spawnManager.PopulationTracker;
                GUILayout.Label($"Total Active Pop: {pop.TotalActivePopulation}");
                GUILayout.Label($"Active Reinforcements: {pop.ActiveReinforcements}");
            }

            GUILayout.Space(10);
            GUILayout.Label($"Director Budget Used: {_director.EditorSpawnsConsumedThisPhase}");
            GUILayout.Label($"Next Spawn Attempt in: {_director.EditorSpawnCooldownTimer:F1}s");
            GUILayout.Label($"Last Rejection: {_director.LastRejectionReason}");

            GUILayout.Space(10);
            
            bool isAuth = _controller.HasStateAuthority;
            if (!isAuth)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("(No State Authority - Commands Disabled)");
                GUI.color = Color.white;
            }

            GUI.enabled = isAuth;

            if (GUILayout.Button("Advance Phase"))
            {
                _controller.AdvancePhase();
            }

            if (GUILayout.Button("Force Spawn Attempt"))
            {
                _director.ForceAttempt();
            }
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Time Multiplier:", GUILayout.Width(100));
            _controller.EditorTimeMultiplier = GUILayout.HorizontalSlider(_controller.EditorTimeMultiplier, 1f, 10f);
            GUILayout.Label($"{_controller.EditorTimeMultiplier:F1}x", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        GUILayout.EndArea();
    }
}
#endif

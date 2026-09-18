using UnityEngine;

/// <summary>
/// Hotkey listener to toggle the Mission Board UI during testing.
/// Can be placed on any GameObject that remains active in the scene (e.g. Systems, Manager, or an empty GameObject).
/// </summary>
[DisallowMultipleComponent]
public sealed class MissionBoardHotkeyToggle : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.M;
    [SerializeField] private MissionBoardUI _missionBoardUI;

    private void Awake()
    {
        EnsureMissionBoardUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            EnsureMissionBoardUI();
            if (_missionBoardUI != null)
            {
                _missionBoardUI.Toggle();
            }
        }
    }

    private void EnsureMissionBoardUI()
    {
        if (_missionBoardUI == null)
        {
            _missionBoardUI = FindAnyObjectByType<MissionBoardUI>(FindObjectsInactive.Include);
        }
    }
}

using UnityEngine;
using TMPro;

public class DungeonPressureHudView : MonoBehaviour
{
    [Tooltip("Text component to display the MM:SS timer.")]
    [SerializeField] private TMP_Text _timerText;
    
    [Tooltip("Text component to display the current pressure phase name.")]
    [SerializeField] private TMP_Text _phaseText;

    public void UpdateTimer(int totalSeconds)
    {
        if (_timerText == null) return;
        
        int m = totalSeconds / 60;
        int s = totalSeconds % 60;
        
        // Format as MM:SS
        _timerText.text = $"{m:00}:{s:00}";
    }

    public void UpdatePhase(string phaseName, Color phaseColor)
    {
        if (_phaseText == null) return;
        
        _phaseText.text = phaseName;
        _phaseText.color = phaseColor;
    }
}

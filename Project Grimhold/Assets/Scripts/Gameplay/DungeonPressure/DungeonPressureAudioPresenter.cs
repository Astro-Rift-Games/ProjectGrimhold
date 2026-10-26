using UnityEngine;
using Fusion;

/// <summary>
/// Detects changes in the DungeonPressurePhase and plays the corresponding SFX and music transitions via the AudioManager.
/// Uses Render() to ensure all clients (not just the host) hear the audio cues when the networked phase changes.
/// </summary>
public sealed class DungeonPressureAudioPresenter : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private DungeonPressureController _pressureController;

    [Header("Audio Clips")]
    [Tooltip("Sound played when entering the Reinforcements phase.")]
    [SerializeField] private CustomClip _reinforcementsSfx;
    
    [Tooltip("Sound played when entering the Critical Pressure phase.")]
    [SerializeField] private CustomClip _criticalPressureSfx;
    
    [Tooltip("Sound played when the dungeon Collapses.")]
    [SerializeField] private CustomClip _collapseSfx;

    [Header("Music Transition")]
    [Tooltip("Optional music track played during Collapse phase with crossfade.")]
    [SerializeField] private CustomClip _collapseMusic;

    [Tooltip("Crossfade duration in seconds when transitioning to Collapse music.")]
    [SerializeField] private float _collapseMusicCrossfade = 2.5f;

    private DungeonPressurePhase _lastPhase = DungeonPressurePhase.Normal;

    public CustomClip CollapseMusic => _collapseMusic;
    public float CollapseMusicCrossfade => _collapseMusicCrossfade;

    public override void Spawned()
    {
        if (_pressureController == null)
        {
            _pressureController = GetComponent<DungeonPressureController>();
        }
        
        if (_pressureController != null)
        {
            _lastPhase = _pressureController.Phase;
            if (_lastPhase == DungeonPressurePhase.Collapse && _collapseMusic.IsValid && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(_collapseMusic, _collapseMusicCrossfade);
            }
        }
    }

    public override void Render()
    {
        if (_pressureController == null) return;

        // Detect phase changes on the client side for audio/visuals
        if (_pressureController.Phase != _lastPhase)
        {
            DungeonPressurePhase newPhase = _pressureController.Phase;
            
            // Only play sound if the pressure is escalating (ignore resets or backward rollbacks)
            if (newPhase > _lastPhase)
            {
                PlayPhaseChangeSfx(newPhase);
            }
            
            _lastPhase = newPhase;
        }
    }

    private void PlayPhaseChangeSfx(DungeonPressurePhase phase)
    {
        if (AudioManager.Instance == null) return;

        CustomClip clipToPlay = default;

        switch (phase)
        {
            case DungeonPressurePhase.Reinforcements:
                clipToPlay = _reinforcementsSfx;
                break;
            case DungeonPressurePhase.CriticalPressure:
                clipToPlay = _criticalPressureSfx;
                break;
            case DungeonPressurePhase.Collapse:
                clipToPlay = _collapseSfx;
                if (_collapseMusic.IsValid)
                {
                    AudioManager.Instance.PlayMusic(_collapseMusic, _collapseMusicCrossfade);
                }
                break;
        }

        if (clipToPlay.IsValid)
        {
            // Play at the camera's position to ensure the local player hears it clearly as a "global" event
            Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioManager.Instance.PlaySfx(clipToPlay, playPos);
        }
    }
}

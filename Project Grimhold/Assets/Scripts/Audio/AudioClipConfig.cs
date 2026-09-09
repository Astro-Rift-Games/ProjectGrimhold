using UnityEngine;

/// <summary>
/// Configuración de audio base para un clip individual.
/// Soporta volumen, pitch, mezcla espacial y loop.
/// </summary>
[System.Serializable]
public struct AudioClipConfig
{
    [SerializeField] private AudioClip _clip;
    
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplicador de volumen (0 a 1).")]
    private float _volume;

    [SerializeField, Range(-3f, 3f)]
    [Tooltip("Multiplicador de pitch.")]
    private float _pitch;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 = 2D (sin espacialización), 1 = 3D (espacializado completamente).")]
    private float _spatialBlend;

    [SerializeField] private bool _loop;

    public AudioClip Clip => _clip;
    
    // Si los valores están en 0 por default de struct, asumimos 1. 
    // Si se desea mutear, se puede usar un volumen muy bajo (ej. 0.001f).
    public float Volume => _volume == 0f ? 1f : _volume;
    public float Pitch => _pitch == 0f ? 1f : _pitch;
    
    public float SpatialBlend => _spatialBlend;
    public bool Loop => _loop;
    
    public bool IsValid => _clip != null;
}

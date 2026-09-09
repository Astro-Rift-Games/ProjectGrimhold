using UnityEngine;

/// <summary>
/// Configuración de audio para un conjunto de clips.
/// Permite definir variaciones aleatorias para un mismo efecto de sonido,
/// controlando su volumen, pitch, mezcla espacial y loop.
/// </summary>
[System.Serializable]
public struct CustomClip
{
    [SerializeField] private AudioClip[] _clips;
    
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

    // Si los valores están en 0 por default de struct, asumimos 1. 
    public float Volume => _volume == 0f ? 1f : _volume;
    public float Pitch => _pitch == 0f ? 1f : _pitch;
    public float SpatialBlend => _spatialBlend;
    public bool Loop => _loop;
    
    public bool IsValid => _clips != null && _clips.Length > 0;

    /// <summary>
    /// Devuelve un clip aleatorio del arreglo, o null si no hay ninguno válido.
    /// </summary>
    public AudioClip GetRandomClip()
    {
        if (!IsValid) return null;
        return _clips[Random.Range(0, _clips.Length)];
    }
}

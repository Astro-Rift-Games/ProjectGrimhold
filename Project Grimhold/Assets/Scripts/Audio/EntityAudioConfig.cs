using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct AudioEntry
{
    public string Key;
    public CustomClip Clip;
}

/// <summary>
/// Clase base para configuraciones de audio. Expone un diccionario serializable de CustomClips.
/// </summary>
public abstract class EntityAudioConfig : ScriptableObject
{
    [SerializeField] private List<AudioEntry> _entries = new List<AudioEntry>();

    private Dictionary<string, CustomClip> _clipDictionary;

    private void OnEnable()
    {
        InitializeDictionary();
    }

    private void InitializeDictionary()
    {
        _clipDictionary = new Dictionary<string, CustomClip>(System.StringComparer.OrdinalIgnoreCase);
        
        foreach (var entry in _entries)
        {
            if (string.IsNullOrEmpty(entry.Key)) continue;
            
            // Si hay duplicados en el inspector, prevalece el primero que cargó
            if (!_clipDictionary.ContainsKey(entry.Key))
            {
                _clipDictionary.Add(entry.Key, entry.Clip);
            }
        }
    }

    public bool TryGetClip(string key, out CustomClip clip)
    {
        if (_clipDictionary == null)
        {
            InitializeDictionary();
        }

        return _clipDictionary.TryGetValue(key, out clip);
    }
}

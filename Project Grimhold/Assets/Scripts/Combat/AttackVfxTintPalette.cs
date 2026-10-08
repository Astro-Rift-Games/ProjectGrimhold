using System;
using UnityEngine;

/// <summary>
/// Maps each <see cref="DamageType"/> to the vertex color that tints a neutral attack VFX sprite, so elemental
/// variants need no new art. Local presentation only: it holds static configuration and no runtime state.
/// </summary>
[CreateAssetMenu(fileName = "AttackVfxTintPalette", menuName = "Grimhold/Combat/Attack VFX Tint Palette")]
public sealed class AttackVfxTintPalette : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [SerializeField] private DamageType _damageType;
        [SerializeField] private Color _tint;

        public DamageType DamageType => _damageType;
        public Color Tint => _tint;
    }

    [SerializeField] private Entry[] _entries;

    /// <summary>Allocation-free lookup of the tint configured for a damage type.</summary>
    public bool TryGetTint(DamageType damageType, out Color tint)
    {
        if (_entries != null)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].DamageType != damageType) continue;
                tint = _entries[i].Tint;
                return true;
            }
        }
        tint = default;
        return false;
    }

    /// <summary>Requires exactly one finite entry for every <see cref="DamageType"/> value.</summary>
    public bool TryValidate(out string error)
    {
        if (_entries == null)
        {
            error = "Attack VFX tint palette requires one entry per damage type.";
            return false;
        }
        foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
        {
            int count = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].DamageType == type) count++;
            }
            if (count != 1)
            {
                error = count == 0
                    ? $"Attack VFX tint palette is missing an entry for damage type {type}."
                    : $"Attack VFX tint palette has {count} entries for damage type {type}; exactly one is required.";
                return false;
            }
        }
        for (int i = 0; i < _entries.Length; i++)
        {
            Color tint = _entries[i].Tint;
            if (!Enum.IsDefined(typeof(DamageType), _entries[i].DamageType))
            {
                error = $"Attack VFX tint palette entry {i} uses an undefined damage type.";
                return false;
            }
            if (!IsFinite(tint.r) || !IsFinite(tint.g) || !IsFinite(tint.b) || !IsFinite(tint.a))
            {
                error = $"Attack VFX tint palette entry {i} must have a finite color.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

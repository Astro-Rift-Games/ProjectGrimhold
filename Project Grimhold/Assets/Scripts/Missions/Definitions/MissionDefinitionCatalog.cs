using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo de definiciones de misiones que permite buscar definiciones estáticas mediante su ID único.
/// </summary>
[CreateAssetMenu(fileName = "MissionDefinitionCatalog", menuName = "Grimhold/Missions/Mission Definition Catalog")]
public sealed class MissionDefinitionCatalog : ScriptableObject
{
    [SerializeField]
    private List<MissionDefinition> _definitions = new();

    [NonSerialized]
    private Dictionary<string, MissionDefinition> _definitionsById;

    [NonSerialized]
    private List<MissionDefinition> _sortedDefinitions;

    [NonSerialized]
    private Dictionary<MissionId, int> _indicesById;

    [NonSerialized]
    private bool _isCacheDirty = true;

    /// <summary>
    /// Gets the number of unique definitions available through the catalog.
    /// </summary>
    public int DefinitionCount
    {
        get
        {
            EnsureCache();
            return _sortedDefinitions.Count;
        }
    }

    private void OnEnable()
    {
        _isCacheDirty = true;
    }

    private void OnValidate()
    {
        _isCacheDirty = true;
    }

    /// <summary>
    /// Intenta obtener la definición de misión correspondiente al ID especificado.
    /// </summary>
    public bool TryGet(string id, out MissionDefinition definition)
    {
        definition = null;

        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        EnsureCache();

        return _definitionsById.TryGetValue(id, out definition);
    }

    /// <summary>
    /// Attempts to resolve the deterministic network index assigned to a mission definition.
    /// Indices are assigned by sorting valid unique IDs with ordinal comparison.
    /// </summary>
    public bool TryGetIndex(MissionId missionId, out int index)
    {
        index = default;

        if (string.IsNullOrWhiteSpace(missionId.Value))
        {
            return false;
        }

        EnsureCache();
        return _indicesById.TryGetValue(missionId, out index);
    }

    /// <summary>
    /// Attempts to resolve a definition from its deterministic network index.
    /// </summary>
    public bool TryGetByIndex(int index, out MissionDefinition definition)
    {
        EnsureCache();

        if (index < 0 || index >= _sortedDefinitions.Count)
        {
            definition = null;
            return false;
        }

        definition = _sortedDefinitions[index];
        return true;
    }

    private void EnsureCache()
    {
        if (_isCacheDirty || _definitionsById == null || _sortedDefinitions == null || _indicesById == null)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        var rebuilt = new Dictionary<string, MissionDefinition>(StringComparer.Ordinal);

        if (_definitions != null)
        {
            foreach (MissionDefinition definition in _definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                {
                    continue;
                }

                if (rebuilt.ContainsKey(definition.Id))
                {
                    continue;
                }

                rebuilt.Add(definition.Id, definition);
            }
        }

        _definitionsById = rebuilt;

        _sortedDefinitions = new List<MissionDefinition>(rebuilt.Values);
        _sortedDefinitions.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

        _indicesById = new Dictionary<MissionId, int>();
        for (int i = 0; i < _sortedDefinitions.Count; i++)
        {
            _indicesById.Add(_sortedDefinitions[i].MissionId, i);
        }

        _isCacheDirty = false;
    }

    /// <summary>
    /// Valida que el catálogo de misiones sea consistente y libre de errores o duplicados.
    /// </summary>
    public bool TryValidate(out string error)
    {
        error = null;

        if (_definitions == null || _definitions.Count == 0)
        {
            error = "Catalog has no entries.";
            return false;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenReferences = new HashSet<MissionDefinition>();

        foreach (MissionDefinition definition in _definitions)
        {
            if (definition == null)
            {
                error = "Catalog contains a null definition reference.";
                return false;
            }

            if (!definition.TryValidate(out string definitionError))
            {
                error = $"Catalog contains an invalid definition: {definitionError}";
                return false;
            }

            if (!seenReferences.Add(definition))
            {
                error = $"Catalog contains duplicate reference for mission definition '{definition.Id}'.";
                return false;
            }

            if (!seenIds.Add(definition.Id))
            {
                error = $"Catalog has duplicate entry for mission ID '{definition.Id}'.";
                return false;
            }
        }

        return true;
    }
}

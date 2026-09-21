using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authoritative runtime lookup for all static ability definitions.</summary>
[CreateAssetMenu(fileName = "AbilityDefinitionCatalog", menuName = "Grimhold/Abilities/Ability Definition Catalog")]
public sealed class AbilityDefinitionCatalog : ScriptableObject
{
    [SerializeField]
    private List<AbilityDefinition> _definitions = new();

    [NonSerialized]
    private Dictionary<AbilityId, AbilityDefinition> _definitionsById;

    [NonSerialized]
    private bool _isCacheDirty = true;

    public int DefinitionCount
    {
        get
        {
            EnsureCache();
            return _definitionsById.Count;
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

    public bool TryGet(AbilityId abilityId, out AbilityDefinition definition)
    {
        definition = null;
        if (!abilityId.IsValid)
        {
            return false;
        }

        EnsureCache();
        return _definitionsById.TryGetValue(abilityId, out definition);
    }

    public bool TryGetId(AbilityDefinition definition, out AbilityId abilityId)
    {
        abilityId = default;
        if (definition == null || !definition.TryValidate(out _))
        {
            return false;
        }

        EnsureCache();
        AbilityId candidate = definition.AbilityId;
        if (!_definitionsById.TryGetValue(candidate, out AbilityDefinition resolved) || resolved != definition)
        {
            return false;
        }

        abilityId = candidate;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (_definitions == null || _definitions.Count == 0)
        {
            error = "Ability definition catalog has no entries.";
            return false;
        }

        var seenIds = new HashSet<AbilityId>();
        var seenReferences = new HashSet<AbilityDefinition>();

        for (int index = 0; index < _definitions.Count; index++)
        {
            AbilityDefinition definition = _definitions[index];
            if (definition == null)
            {
                error = $"Ability definition catalog contains a null definition at index {index}.";
                return false;
            }

            if (!definition.TryValidate(out string definitionError))
            {
                error = $"Ability definition catalog contains an invalid definition: {definitionError}";
                return false;
            }

            if (!seenReferences.Add(definition))
            {
                error = $"Ability definition catalog contains duplicate reference for '{definition.Id}'.";
                return false;
            }

            if (!seenIds.Add(definition.AbilityId))
            {
                error = $"Ability definition catalog contains duplicate ID '{definition.Id}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private void EnsureCache()
    {
        if (_isCacheDirty || _definitionsById == null)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        var rebuilt = new Dictionary<AbilityId, AbilityDefinition>();
        if (_definitions != null)
        {
            foreach (AbilityDefinition definition in _definitions)
            {
                if (definition == null || !definition.AbilityId.IsValid || rebuilt.ContainsKey(definition.AbilityId))
                {
                    continue;
                }

                rebuilt.Add(definition.AbilityId, definition);
            }
        }

        _definitionsById = rebuilt;
        _isCacheDirty = false;
    }
}

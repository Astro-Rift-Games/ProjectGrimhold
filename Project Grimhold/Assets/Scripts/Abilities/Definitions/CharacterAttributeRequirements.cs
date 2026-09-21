using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable authored rule that evaluates all configured character attribute minimums.</summary>
[Serializable]
public sealed class CharacterAttributeRequirements
{
    private static readonly IReadOnlyList<CharacterAttributeRequirement> EmptyRequirements =
        Array.Empty<CharacterAttributeRequirement>();

    [SerializeField]
    private List<CharacterAttributeRequirement> _requirements = new();

    public IReadOnlyList<CharacterAttributeRequirement> Requirements =>
        _requirements ?? EmptyRequirements;

    public CharacterAttributeRequirements()
    {
    }

    public CharacterAttributeRequirements(params CharacterAttributeRequirement[] requirements)
    {
        _requirements = requirements != null
            ? new List<CharacterAttributeRequirement>(requirements)
            : null;
    }

    public bool TryValidate(out string error)
    {
        if (_requirements == null)
        {
            error = "Character attribute requirements collection is missing.";
            return false;
        }

        var seenAttributes = new HashSet<CharacterAttribute>();
        for (int index = 0; index < _requirements.Count; index++)
        {
            CharacterAttributeRequirement requirement = _requirements[index];
            if (!requirement.TryValidate(out string requirementError))
            {
                error = $"Requirement at index {index} is invalid: {requirementError}";
                return false;
            }

            if (!seenAttributes.Add(requirement.Attribute))
            {
                error = $"Character attribute requirements contain duplicate attribute '{requirement.Attribute}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public bool IsSatisfiedBy(in CharacterAttributeState attributes)
    {
        if (!TryValidate(out _))
        {
            return false;
        }

        for (int index = 0; index < _requirements.Count; index++)
        {
            if (!_requirements[index].IsSatisfiedBy(attributes))
            {
                return false;
            }
        }

        return true;
    }
}

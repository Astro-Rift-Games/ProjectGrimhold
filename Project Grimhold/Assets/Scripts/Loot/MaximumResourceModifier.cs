using System;
using UnityEngine;

/// <summary>Flat bonus contributed by an equipment template to one maximum resource.</summary>
[Serializable]
public struct MaximumResourceModifier
{
    [SerializeField] private MaximumResourceType _resource;
    [SerializeField, Min(0)] private int _amount;

    public MaximumResourceType Resource => _resource;
    public int Amount => _amount;
    public bool IsPresent => _resource != MaximumResourceType.None;

    public MaximumResourceModifier(MaximumResourceType resource, int amount)
    {
        _resource = resource;
        _amount = amount;
    }

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(MaximumResourceType), _resource))
        {
            error = $"Maximum resource modifier has unsupported resource '{(int)_resource}'.";
            return false;
        }

        if (_resource == MaximumResourceType.None)
        {
            if (_amount != 0)
            {
                error = "An absent maximum resource modifier must have an amount of zero.";
                return false;
            }

            error = null;
            return true;
        }

        if (_amount <= 0)
        {
            error = "A present maximum resource modifier must have a positive amount.";
            return false;
        }

        error = null;
        return true;
    }
}

using System;
using UnityEngine;

/// <summary>One authored sale of the Ability Vendor: an ability and its gold price.</summary>
[Serializable]
public struct AbilityVendorOffer
{
    [SerializeField]
    private AbilityDefinition _ability;

    [SerializeField, Min(1)]
    private long _price;

    public AbilityDefinition Ability => _ability;
    public long Price => _price;

    public AbilityVendorOffer(AbilityDefinition ability, long price)
    {
        _ability = ability;
        _price = price;
    }
}

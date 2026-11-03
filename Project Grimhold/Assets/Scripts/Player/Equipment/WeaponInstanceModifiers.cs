using System;
using UnityEngine;

/// <summary>Canonical optional scaling slots and gem sockets owned by a future unique weapon instance.</summary>
[Serializable]
public struct WeaponInstanceModifiers
{
    public const int MaxGemSocketCount = 2;

    [SerializeField] private WeaponScalingModifier _primary;
    [SerializeField] private WeaponScalingModifier _secondary;
    [SerializeField] private int _gemSocketCount;
    [SerializeField] private WeaponGemSocket _firstGemSocket;
    [SerializeField] private WeaponGemSocket _secondGemSocket;

    public WeaponScalingModifier Primary => _primary.Normalize();
    public WeaponScalingModifier Secondary => _secondary.Normalize();
    public bool HasPrimary => _primary.IsPresent;
    public bool HasSecondary => _secondary.IsPresent;
    public int GemSocketCount => _gemSocketCount;

    private WeaponInstanceModifiers(
        WeaponScalingModifier primary,
        WeaponScalingModifier secondary,
        int gemSocketCount,
        WeaponGemSocket firstGemSocket,
        WeaponGemSocket secondGemSocket)
    {
        _primary = primary.Normalize();
        _secondary = secondary.Normalize();
        _gemSocketCount = gemSocketCount;
        _firstGemSocket = firstGemSocket.Normalize();
        _secondGemSocket = secondGemSocket.Normalize();
    }

    public static bool TryCreate(
        WeaponScalingModifier primary,
        WeaponScalingModifier secondary,
        CharacterAttribute naturalAttribute,
        out WeaponInstanceModifiers modifiers,
        out string error) =>
        TryCreate(primary, secondary, naturalAttribute, 0, default, default, out modifiers, out error);

    public static bool TryCreate(
        WeaponScalingModifier primary,
        WeaponScalingModifier secondary,
        CharacterAttribute naturalAttribute,
        int gemSocketCount,
        WeaponGemSocket firstGemSocket,
        WeaponGemSocket secondGemSocket,
        out WeaponInstanceModifiers modifiers,
        out string error)
    {
        modifiers = new WeaponInstanceModifiers(
            primary,
            secondary,
            gemSocketCount,
            firstGemSocket,
            secondGemSocket);
        if (!modifiers.TryValidate(naturalAttribute, out error))
        {
            modifiers = default;
            return false;
        }

        return true;
    }

    /// <summary>Removes ignored attributes from absent serialized slots.</summary>
    public WeaponInstanceModifiers Normalize() =>
        new(_primary, _secondary, _gemSocketCount, _firstGemSocket, _secondGemSocket);

    /// <summary>Returns one of the instance's existing gem sockets, empty or occupied.</summary>
    public bool TryGetGemSocket(int index, out WeaponGemSocket socket)
    {
        if (index < 0 || index >= _gemSocketCount || index >= MaxGemSocketCount)
        {
            socket = default;
            return false;
        }

        socket = index == 0 ? _firstGemSocket.Normalize() : _secondGemSocket.Normalize();
        return true;
    }

    public bool TryValidate(CharacterAttribute naturalAttribute, out string error)
    {
        return TryValidateScaling(naturalAttribute, out error) && TryValidateGemSockets(out error);
    }

    private bool TryValidateScaling(CharacterAttribute naturalAttribute, out string error)
    {
        WeaponScalingModifier primary = _primary.Normalize();
        WeaponScalingModifier secondary = _secondary.Normalize();

        if (primary.IsPresent)
        {
            if (!primary.TryValidatePresent(out error))
            {
                return false;
            }

            if (primary.Attribute != naturalAttribute)
            {
                error = $"Primary weapon scaling attribute '{primary.Attribute}' must match natural attribute '{naturalAttribute}'.";
                return false;
            }
        }

        if (!secondary.IsPresent)
        {
            error = null;
            return true;
        }

        if (!primary.IsPresent)
        {
            error = "Secondary weapon scaling cannot exist without a primary scaling modifier.";
            return false;
        }

        if (!secondary.TryValidatePresent(out error))
        {
            return false;
        }

        if (secondary.Attribute == primary.Attribute)
        {
            error = "Primary and secondary weapon scaling modifiers cannot use the same attribute.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryValidateGemSockets(out string error)
    {
        if (_gemSocketCount < 0 || _gemSocketCount > MaxGemSocketCount)
        {
            error = $"Weapon gem socket count must be between 0 and {MaxGemSocketCount}, not '{_gemSocketCount}'.";
            return false;
        }

        if ((_gemSocketCount < 1 && _firstGemSocket.IsOccupied) ||
            (_gemSocketCount < 2 && _secondGemSocket.IsOccupied))
        {
            error = $"A weapon with {_gemSocketCount} gem sockets cannot hold a gem beyond its last socket.";
            return false;
        }

        error = null;
        return true;
    }
}

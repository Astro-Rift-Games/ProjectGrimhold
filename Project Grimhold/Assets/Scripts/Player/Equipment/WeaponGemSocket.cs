using System;
using UnityEngine;

/// <summary>One gem socket owned by a weapon instance: empty or holding one installed gem.</summary>
[Serializable]
public struct WeaponGemSocket
{
    [SerializeField] private string _gemLootId;

    public bool IsOccupied => !string.IsNullOrWhiteSpace(_gemLootId);
    public LootId GemLootId => IsOccupied ? new LootId(_gemLootId) : default;

    private WeaponGemSocket(LootId gemLootId)
    {
        _gemLootId = gemLootId.Value;
    }

    public static bool TryCreateOccupied(LootId gemLootId, out WeaponGemSocket socket, out string error)
    {
        if (!gemLootId.IsValid)
        {
            socket = default;
            error = "An occupied gem socket requires a valid gem LootId.";
            return false;
        }

        socket = new WeaponGemSocket(gemLootId);
        error = null;
        return true;
    }

    /// <summary>Canonicalizes blank serialized identifiers as an empty socket.</summary>
    public WeaponGemSocket Normalize() => IsOccupied ? this : default;
}

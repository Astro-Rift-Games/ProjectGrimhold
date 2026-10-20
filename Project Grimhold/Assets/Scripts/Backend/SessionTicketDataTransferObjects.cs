using System;

namespace Grimhold.Backend
{
    [Serializable]
    public struct TownSessionTicketResponse
    {
        public string ticket;
        public string characterId;
        public string characterName;
        public string sessionName;
        public long expiresAt;
    }

    [Serializable]
    public struct VerifyTownTicketRequest
    {
        public string ticket;
    }

    [Serializable]
    public struct VerifyTownTicketResponse
    {
        public bool valid;
        public string characterId;
        public string characterName;
        public string sessionName;
        public long expiresAt;
        public string error;
    }

    [Serializable]
    public struct IssueRaidAdmissionTicketRequest
    {
        public string raidCode;
        public string reservationId;
    }

    [Serializable]
    public struct CharacterAttributeStateDto
    {
        public short vitality;
        public short resistance;
        public short strength;
        public short dexterity;
        public short intelligence;
        public short luck;
        public short availablePoints;
    }

    [Serializable]
    public struct PreparedAbilitiesDto
    {
        public string slot1;
        public string slot2;
    }

    [Serializable]
    public struct LootEntryDto
    {
        public string lootId;
        public int amount;
    }

    [Serializable]
    public struct PreparedEquipmentDto
    {
        public string weaponSlot1;
        public string weaponSlot2;
        public string offHand1;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
        public string offHand2;
    }

    [Serializable]
    public struct AuthoritativeRaidAdmissionSnapshotDto
    {
        public int level;
        public long experience;
        public CharacterAttributeStateDto characterAttributes;
        public PreparedAbilitiesDto preparedAbilities;
        public LootEntryDto[] items;
        public PreparedEquipmentDto preparedEquipment;
        public int lastAppliedProgressionResultSequence;
    }

    [Serializable]
    public struct AuthoritativeRaidAdmissionTicketResponse
    {
        public string ticket;
        public string characterId;
        public string raidCode;
        public string reservationId;
        public AuthoritativeRaidAdmissionSnapshotDto snapshot;
        public long expiresAt;
    }

    [Serializable]
    public struct VerifyRaidAdmissionTicketRequest
    {
        public string ticket;
        public string raidCode;
    }

    [Serializable]
    public struct VerifyRaidAdmissionTicketResponse
    {
        public bool valid;
        public string characterId;
        public string raidCode;
        public string reservationId;
        public AuthoritativeRaidAdmissionSnapshotDto snapshot;
        public long expiresAt;
        public string error;
    }
}

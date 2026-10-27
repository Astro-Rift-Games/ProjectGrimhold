using System;

namespace Grimhold.Backend
{
    // ---------------------------------------------------------------------------
    // Inbound DTOs (responses from the backend)
    // ---------------------------------------------------------------------------

    [Serializable]
    public struct ProgressionData
    {
        public int  level;
        public long experience;
        public int  lastAppliedProgressionResultSequence;
        public CharacterAttributesData characterAttributes;
        public string[] unlockedAbilities;
        public int  revision;
    }

    [Serializable]
    public struct CharacterAttributesData
    {
        public int vitality;
        public int resistance;
        public int strength;
        public int dexterity;
        public int intelligence;
        public int luck;
        public int availablePoints;
    }

    [Serializable]
    public struct CommitProgressionResult
    {
        public CharacterAttributesData characterAttributes;
        public int revision;
    }

    [Serializable]
    public struct PurchaseAbilityResult
    {
        public bool alreadySecured;
        public string transactionId;
        public string abilityId;
        public long price;
        public long currency;
        public string[] unlockedAbilities;
        public int revision;
    }

    // ---------------------------------------------------------------------------
    // Outbound DTOs (requests to the backend)
    // ---------------------------------------------------------------------------

    [Serializable]
    public struct CommitProgressionRequest
    {
        public string attribute;
        public int expectedRevision;
    }

    [Serializable]
    public struct PurchaseAbilityRequest
    {
        public string transactionId;
        public string abilityId;
        public int expectedRevision;
    }
}

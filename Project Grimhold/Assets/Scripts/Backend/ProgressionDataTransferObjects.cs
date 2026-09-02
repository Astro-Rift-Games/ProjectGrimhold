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
        public bool alreadyApplied;
        public int  level;
        public long experience;
    }

    // ---------------------------------------------------------------------------
    // Outbound DTOs (requests to the backend)
    // ---------------------------------------------------------------------------

    [Serializable]
    public struct CommitProgressionRequest
    {
        public string raidId;
        public int    resultSequence;
        public long   consolidatedExperience;
        public int    resultingLevel;
        public int    newLevel;
        public long   newExperience;
        public CharacterAttributesData characterAttributes;
    }
}

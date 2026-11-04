using System;
using UnityEngine;

namespace Grimhold.Backend
{
    /// <summary>Town entries carry slots; extraction wire payloads deliberately remain aggregate.</summary>
    public static class InventoryRequestJson
    {
        [Serializable]
        private struct AggregateItem
        {
            public string lootId;
            public int amount;
        }

        [Serializable]
        private sealed class Extraction
        {
            public string raidId;
            public int resultSequence;
            public AggregateItem[] items;
        }

        [Serializable]
        private sealed class UnifiedExtraction
        {
            public string raidId;
            public int resultSequence;
            public AggregateItem[] items;
            public PreparedEquipmentData preparedEquipment;
            public ExtractionProgressionData progression;
        }

        [Serializable]
        private sealed class PublishedExtraction
        {
            public string raidId;
            public string resultId;
            public int resultSequence;
            public string targetCharacterId;
            public AggregateItem[] items;
            public PreparedEquipmentData preparedEquipment;
            public long experienceGranted;
            public string issuedAt;
            public string hostSignature;
        }

        public static string Encode<T>(T body)
        {
            if (body is CommitExtractionRequest legacy)
                return JsonUtility.ToJson(new Extraction
                { raidId = legacy.raidId, resultSequence = legacy.resultSequence, items = Aggregate(legacy.items) });
            if (body is CommitExtractionUnifiedRequest unified)
                return JsonUtility.ToJson(new UnifiedExtraction
                {
                    raidId = unified.raidId, resultSequence = unified.resultSequence, items = Aggregate(unified.items),
                    preparedEquipment = unified.preparedEquipment, progression = unified.progression
                });
            if (body is PublishExtractionResultRequest published)
                return JsonUtility.ToJson(new PublishedExtraction
                {
                    raidId = published.raidId, resultId = published.resultId, resultSequence = published.resultSequence,
                    targetCharacterId = published.targetCharacterId, items = Aggregate(published.items),
                    preparedEquipment = published.preparedEquipment, experienceGranted = published.experienceGranted,
                    issuedAt = published.issuedAt, hostSignature = published.hostSignature
                });
            return JsonUtility.ToJson(body);
        }

        private static AggregateItem[] Aggregate(InventoryItemData[] items)
        {
            if (items == null) return null;
            var result = new AggregateItem[items.Length];
            for (int i = 0; i < items.Length; i++)
                result[i] = new AggregateItem { lootId = items[i].lootId, amount = items[i].amount };
            return result;
        }
    }
}

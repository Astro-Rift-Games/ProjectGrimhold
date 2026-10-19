using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Grimhold.Backend
{
    /// <summary>
    /// Utility for constructing the canonical representation of an authoritative extraction result
    /// and generating HMAC-SHA256 signatures matching the backend verification contract (US-58 / SEC-58.1).
    /// </summary>
    public static class RaidResultCanonicalizer
    {
        private static readonly string[] SlotOrder = new[]
        {
            "armor", "boots", "gloves", "helmet", "offHand1", "offHand2", "weaponSlot1", "weaponSlot2"
        };

        public static string BuildCanonicalResultString(
            string raidId,
            string resultId,
            string targetId,
            InventoryItemData[] items,
            PreparedEquipmentData eq,
            long experienceGranted,
            string issuedAt)
        {
            string normRaidId = (raidId ?? "").Trim();
            string normResultId = (resultId ?? "").Trim();
            string normTargetId = (targetId ?? "").Trim();

            // Items: grouped/normalized, sorted alphabetically by lootId ascending
            string canonicalItems = "";
            if (items != null && items.Length > 0)
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.lootId) || item.amount <= 0) continue;
                    string key = item.lootId.Trim().ToLowerInvariant();
                    map[key] = map.TryGetValue(key, out int current) ? current + item.amount : item.amount;
                }

                var sorted = map.OrderBy(kv => kv.Key, StringComparer.Ordinal);
                canonicalItems = string.Join(",", sorted.Select(kv => $"{kv.Key}:{kv.Value}"));
            }

            // Equipment: 8 slots in fixed alphabetical order
            var eqMap = new Dictionary<string, string>
            {
                { "armor", (eq.armor ?? "").Trim().ToLowerInvariant() },
                { "boots", (eq.boots ?? "").Trim().ToLowerInvariant() },
                { "gloves", (eq.gloves ?? "").Trim().ToLowerInvariant() },
                { "helmet", (eq.helmet ?? "").Trim().ToLowerInvariant() },
                { "offHand1", (eq.offHand1 ?? "").Trim().ToLowerInvariant() },
                { "offHand2", (eq.offHand2 ?? "").Trim().ToLowerInvariant() },
                { "weaponSlot1", (eq.weaponSlot1 ?? "").Trim().ToLowerInvariant() },
                { "weaponSlot2", (eq.weaponSlot2 ?? "").Trim().ToLowerInvariant() }
            };

            string canonicalEq = string.Join(";", SlotOrder.Select(slot => $"{slot}={eqMap[slot]}"));
            long normXp = experienceGranted < 0 ? 0 : experienceGranted;
            string normIssuedAt = (issuedAt ?? "").Trim();

            return $"{normRaidId}|{normResultId}|{normTargetId}|{canonicalItems}|{canonicalEq}|{normXp}|{normIssuedAt}";
        }

        public static string ComputeHmacSha256(string message, string secret)
        {
            if (string.IsNullOrEmpty(secret)) return "";
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message ?? ""));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}

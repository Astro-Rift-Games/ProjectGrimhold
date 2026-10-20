using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    public enum RuleTileTemplateKind { Floor, Orthogonal, FullContour }

    public sealed class RuleTileTemplateSlot
    {
        public readonly string Key;
        public readonly string Label;
        public readonly string Group;
        public readonly int Mask;
        public readonly int ConditionMask;

        internal RuleTileTemplateSlot(string key, string label, string group, int mask, int conditions)
        {
            Key = key;
            Label = label;
            Group = group;
            Mask = mask;
            ConditionMask = conditions;
        }
    }

    /// <summary>Static authoring definitions; the native RuleTile remains the only draft model.</summary>
    public static class RuleTileBuilderTemplates
    {
        private const int Cardinals = (1 << 1) | (1 << 3) | (1 << 4) | (1 << 6);
        private static readonly Vector3Int[] Positions =
        {
            new(-1, 1, 0), new(0, 1, 0), new(1, 1, 0), new(-1, 0, 0),
            new(1, 0, 0), new(-1, -1, 0), new(0, -1, 0), new(1, -1, 0)
        };
        private static readonly IReadOnlyList<RuleTileTemplateSlot> Floor = Array.AsReadOnly(new[]
        {
            new RuleTileTemplateSlot("floor", "Continuous floor", "Center", 0, 0)
        });
        private static readonly IReadOnlyList<RuleTileTemplateSlot> Orthogonal = Build(false);
        private static readonly IReadOnlyList<RuleTileTemplateSlot> FullContour = Build(true);

        public static Vector3Int GetPosition(int bit)
        {
            if (bit < 0 || bit >= Positions.Length) throw new ArgumentOutOfRangeException(nameof(bit));
            return Positions[bit];
        }

        public static IReadOnlyList<RuleTileTemplateSlot> GetSlots(RuleTileTemplateKind kind)
        {
            return kind switch
            {
                RuleTileTemplateKind.Floor => Floor,
                RuleTileTemplateKind.Orthogonal => Orthogonal,
                RuleTileTemplateKind.FullContour => FullContour,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        public static RuleTile.TilingRule FindRule(RuleTile tile, RuleTileTemplateSlot slot)
        {
            if (tile == null || slot == null || tile.m_TilingRules == null) return null;
            foreach (var rule in tile.m_TilingRules)
                if (IsSlot(rule, slot)) return rule;
            return null;
        }

        public static void SetSprites(RuleTile tile, RuleTileTemplateSlot slot, Sprite[] sprites)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (sprites == null) throw new ArgumentNullException(nameof(sprites));
            foreach (var sprite in sprites)
                if (sprite == null) throw new ArgumentException("Assign valid sprites or clear the optional slot.", nameof(sprites));
            string compatibility = RuleTileBuilderValidation.CheckCompatibility(tile);
            if (compatibility != null) throw new ArgumentException(compatibility, nameof(tile));
            var rule = FindRule(tile, slot);
            if (sprites.Length == 0)
            {
                if (rule != null) tile.m_TilingRules.Remove(rule);
                tile.UpdateNeighborPositions();
                return;
            }
            if (rule == null)
            {
                int id = 0;
                var usedIds = new HashSet<int>();
                foreach (var existing in tile.m_TilingRules) usedIds.Add(existing.m_Id);
                while (usedIds.Contains(id)) id++;
                rule = new RuleTile.TilingRule
                {
                    m_Id = id,
                    m_Neighbors = new List<int>(),
                    m_NeighborPositions = new List<Vector3Int>(),
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_Output = sprites.Length > 1 ? RuleTile.TilingRuleOutput.OutputSprite.Random : RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_ColliderType = tile.m_DefaultColliderType
                };
                for (int i = 0; i < 8; i++)
                {
                    if ((slot.ConditionMask & (1 << i)) == 0) continue;
                    rule.m_NeighborPositions.Add(Positions[i]);
                    rule.m_Neighbors.Add((slot.Mask & (1 << i)) != 0 ? 1 : 2);
                }
                tile.m_TilingRules.Add(rule);
            }
            rule.m_Sprites = (Sprite[])sprites.Clone();
            tile.UpdateNeighborPositions();
        }

        public static bool TryRecognize(RuleTile tile, out RuleTileTemplateKind kind)
        {
            kind = RuleTileTemplateKind.Floor;
            if (RuleTileBuilderValidation.CheckCompatibility(tile) != null) return false;
            foreach (RuleTileTemplateKind candidate in Enum.GetValues(typeof(RuleTileTemplateKind)))
            {
                var assigned = new HashSet<string>();
                bool compatible = true;
                foreach (var rule in tile.m_TilingRules)
                {
                    RuleTileTemplateSlot found = null;
                    foreach (var slot in GetSlots(candidate))
                        if (IsSlot(rule, slot)) { found = slot; break; }
                    if (found == null || !assigned.Add(found.Key)) { compatible = false; break; }
                }
                if (!compatible) continue;
                kind = candidate;
                return true;
            }
            return false;
        }

        private static bool IsSlot(RuleTile.TilingRule rule, RuleTileTemplateSlot slot)
        {
            if (rule == null || rule.m_RuleTransform != RuleTile.TilingRuleOutput.Transform.Fixed ||
                rule.m_Neighbors == null || rule.m_NeighborPositions == null ||
                rule.m_Neighbors.Count != rule.m_NeighborPositions.Count) return false;
            int conditions = 0;
            int mask = 0;
            for (int i = 0; i < rule.m_Neighbors.Count; i++)
            {
                int neighbor = rule.m_Neighbors[i];
                if (neighbor == 0) continue;
                int bit = Array.IndexOf(Positions, rule.m_NeighborPositions[i]);
                if (bit < 0 || (conditions & (1 << bit)) != 0 || (neighbor != 1 && neighbor != 2)) return false;
                conditions |= 1 << bit;
                if (neighbor == 1) mask |= 1 << bit;
            }
            return conditions == slot.ConditionMask && mask == slot.Mask;
        }

        private static IReadOnlyList<RuleTileTemplateSlot> Build(bool diagonals)
        {
            var slots = new List<RuleTileTemplateSlot>();
            for (int mask = 0; mask < 256; mask++)
            {
                int supported = SupportedDiagonals(mask);
                int conditions = Cardinals | (diagonals ? supported : 0);
                if ((mask & ~conditions) != 0) continue;
                string group = Group(mask, diagonals ? supported & ~mask : 0);
                slots.Add(new RuleTileTemplateSlot((diagonals ? "contour-" : "orthogonal-") + mask.ToString("X2"),
                    Describe(mask, group, diagonals ? supported & ~mask : 0), group, mask, conditions));
            }
            return slots.AsReadOnly();
        }

        private static int SupportedDiagonals(int mask)
        {
            int result = 0;
            if ((mask & 10) == 10) result |= 1;
            if ((mask & 18) == 18) result |= 4;
            if ((mask & 72) == 72) result |= 32;
            if ((mask & 80) == 80) result |= 128;
            return result;
        }

        private static string Group(int mask, int missingCorners)
        {
            int count = 0;
            foreach (int bit in new[] { 1, 3, 4, 6 }) if ((mask & (1 << bit)) != 0) count++;
            if (missingCorners != 0) return "Inner corners";
            if (count == 0) return "Isolated";
            if (count == 1) return "Ends";
            if (count == 2) return (mask & 66) == 66 || (mask & 24) == 24 ? "Corridors" : "Outer corners";
            if (count == 3) return "Edges and junctions";
            return "Center";
        }

        private static string Describe(int mask, string group, int missingCorners)
        {
            var directions = new List<string>();
            foreach (int bit in new[] { 1, 4, 6, 3 })
                if ((mask & (1 << bit)) != 0) directions.Add(bit == 1 ? "N" : bit == 4 ? "E" : bit == 6 ? "S" : "W");
            string label = group + (directions.Count == 0 ? " piece" : " — " + string.Join("/", directions));
            if (missingCorners == 0) return label;
            var corners = new List<string>();
            foreach (int bit in new[] { 0, 2, 5, 7 })
                if ((missingCorners & (1 << bit)) != 0) corners.Add(bit == 0 ? "NW" : bit == 2 ? "NE" : bit == 5 ? "SW" : "SE");
            return label + "; inset " + string.Join("/", corners);
        }
    }
}

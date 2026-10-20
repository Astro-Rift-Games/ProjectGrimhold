using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    /// <summary>Illustrative terrain only. Assignments always live in the native RuleTile draft.</summary>
    public static class RuleTileBuilderAssignmentScenarios
    {
        private static readonly IReadOnlyList<RuleTileAssignmentScenario> Floor = Build(RuleTileTemplateKind.Floor);
        private static readonly IReadOnlyList<RuleTileAssignmentScenario> Orthogonal = Build(RuleTileTemplateKind.Orthogonal);
        private static readonly IReadOnlyList<RuleTileAssignmentScenario> Contour = Build(RuleTileTemplateKind.FullContour);

        public static IReadOnlyList<RuleTileAssignmentScenario> GetScenarios(RuleTileTemplateKind kind) => kind switch
        {
            RuleTileTemplateKind.Floor => Floor,
            RuleTileTemplateKind.Orthogonal => Orthogonal,
            RuleTileTemplateKind.FullContour => Contour,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static Sprite GetSprite(RuleTile draft, RuleTileTemplateSlot slot)
        {
            var rule = RuleTileBuilderTemplates.FindRule(draft, slot);
            return rule?.m_Sprites != null && rule.m_Sprites.Length > 0 && rule.m_Sprites[0]
                ? rule.m_Sprites[0] : draft.m_DefaultSprite;
        }

        public static string GetPieceName(RuleTileTemplateSlot slot)
        {
            if (slot.ConditionMask == 0) return "Continuous floor";
            bool n = Has(slot.Mask, 1), w = Has(slot.Mask, 3), e = Has(slot.Mask, 4), s = Has(slot.Mask, 6);
            int count = (n ? 1 : 0) + (w ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0);
            string name;
            if (count == 0) name = "Isolated piece";
            else if (count == 1) name = "End — connects " + (n ? "above" : s ? "below" : w ? "left" : "right");
            else if (count == 2 && n && s) name = "Vertical corridor";
            else if (count == 2 && w && e) name = "Horizontal corridor";
            else if (count == 2) name = "Outer corner — " + (!n ? "top" : "bottom") + "-" + (!w ? "left" : "right");
            else if (count == 3) name = !n ? "Top edge" : !s ? "Bottom edge" : !w ? "Left edge" : "Right edge";
            else name = "Center / cross junction";
            var notches = MissingCorners(slot);
            if (notches.Count > 0) name = (count == 4 ? "Inner corner" + (notches.Count > 1 ? "s" : "") : name + " · inner corner") + " — " + string.Join(", ", notches);
            return name;
        }

        public static string GetDescription(RuleTileTemplateSlot slot)
        {
            if (slot.ConditionMask == 0) return "The same floor artwork can appear anywhere in this surface. No border pieces are required.";
            var connections = new List<string>();
            var boundaries = new List<string>();
            var words = new[] { "above", "left", "right", "below" };
            var bits = new[] { 1, 3, 4, 6 };
            for (int i = 0; i < bits.Length; i++) (Has(slot.Mask, bits[i]) ? connections : boundaries).Add(words[i]);
            string text = connections.Count > 0 ? "Surface continues " + string.Join(", ", connections) + ". " : "This piece stands alone. ";
            if (boundaries.Count > 0) text += "Empty or other surface " + string.Join(", ", boundaries) + ". ";
            var notches = MissingCorners(slot);
            if (notches.Count > 0) text += "Inward recess at " + string.Join(", ", notches) + ". ";
            return text + "Highlighted cells use this same piece. Artwork is never rotated automatically.";
        }

        private static List<string> MissingCorners(RuleTileTemplateSlot slot)
        {
            var result = new List<string>();
            var bits = new[] { 0, 2, 5, 7 };
            var words = new[] { "top-left", "top-right", "bottom-left", "bottom-right" };
            for (int i = 0; i < bits.Length; i++)
                if (Has(slot.ConditionMask, bits[i]) && !Has(slot.Mask, bits[i])) result.Add(words[i]);
            return result;
        }

        private static bool Has(int mask, int bit) => (mask & (1 << bit)) != 0;

        private static IReadOnlyList<RuleTileAssignmentScenario> Build(RuleTileTemplateKind kind)
        {
            var examples = new List<RuleTileAssignmentScenario>();
            var room = Rectangle(1, 1, 9, 7);
            examples.Add(new RuleTileAssignmentScenario("Room", "Click the room's top edge, corners or center to choose the piece that belongs there.", 11, 9, room));
            if (kind == RuleTileTemplateKind.Floor) return examples.AsReadOnly();

            var corridors = new HashSet<Vector3Int>();
            for (int x = 1; x <= 6; x++) corridors.Add(new Vector3Int(x, 6, 0));
            for (int y = 1; y <= 6; y++) corridors.Add(new Vector3Int(10, y, 0));
            corridors.Add(new Vector3Int(2, 2, 0));
            examples.Add(new RuleTileAssignmentScenario("Corridors & ends", "Horizontal and vertical paths, their ends, and an isolated piece.", 13, 9, corridors));

            var junctions = new HashSet<Vector3Int>();
            for (int i = 1; i <= 5; i++)
            {
                junctions.Add(new Vector3Int(i, 3, 0));
                junctions.Add(new Vector3Int(3, i, 0));
                junctions.Add(new Vector3Int(i + 7, 3, 0));
            }
            for (int y = 3; y <= 6; y++) junctions.Add(new Vector3Int(10, y, 0));
            examples.Add(new RuleTileAssignmentScenario("Cross & T junctions", "Select the center of a junction to assign its meeting piece. The same rule can also appear on a room edge.", 14, 9, junctions));

            var bend = Rectangle(1, 1, 9, 7);
            bend.ExceptWith(Rectangle(5, 4, 5, 4));
            examples.Add(new RuleTileAssignmentScenario("L-shaped room", "A broad bend makes outside corners and the inward corner visible in context.", 11, 9, bend));
            var holes = Rectangle(1, 1, 11, 9);
            holes.ExceptWith(Rectangle(4, 4, 2, 2));
            holes.ExceptWith(Rectangle(8, 3, 1, 4));
            examples.Add(new RuleTileAssignmentScenario("Room with holes", "Walk around the empty courtyards to find inward-facing edges and corners.", 13, 11, holes));

            var covered = new HashSet<string>(examples.SelectMany(example => example.Cells.Select(cell => example.GetSlot(kind, cell).Key)));
            var missing = RuleTileBuilderTemplates.GetSlots(kind).Where(slot => !covered.Contains(slot.Key)).ToArray();
            // Four actual terrain patches per page, not a list of neighbor matrices. The center's
            // adjacent terrain is carved out of a larger courtyard to demonstrate rarer combinations.
            for (int start = 0; start < missing.Length; start += 4)
            {
                var terrain = new HashSet<Vector3Int>();
                var featured = new List<Vector3Int>();
                for (int index = start; index < Math.Min(start + 4, missing.Length); index++)
                {
                    int panel = index - start;
                    var origin = new Vector3Int((panel % 2) * 7, (1 - panel / 2) * 7, 0);
                    var center = origin + new Vector3Int(3, 3, 0);
                    var patch = Rectangle(origin.x + 1, origin.y + 1, 5, 5);
                    var slot = missing[index];
                    for (int bit = 0; bit < 8; bit++)
                        if (Has(slot.ConditionMask, bit) && !Has(slot.Mask, bit)) patch.Remove(center + RuleTileBuilderTemplates.GetPosition(bit));
                    terrain.UnionWith(patch);
                    featured.Add(center);
                }
                examples.Add(new RuleTileAssignmentScenario("Courtyards & recesses " + (start / 4 + 1),
                    "Small terrain studies for combined recesses and unusual junctions. Numbers mark featured pieces; every solid cell is editable.",
                    14, 14, terrain, featured));
            }
            return examples.AsReadOnly();
        }

        private static HashSet<Vector3Int> Rectangle(int x, int y, int width, int height)
        {
            var result = new HashSet<Vector3Int>();
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++) result.Add(new Vector3Int(column, row, 0));
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    public sealed class RuleTileValidationReport
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public readonly int[] FirstRuleByMask = new int[256];
        public readonly int[] AnimationRuleByMask = new int[256];
        public bool HasErrors => Errors.Count != 0;

        public RuleTileValidationReport()
        {
            Array.Fill(FirstRuleByMask, -1);
            Array.Fill(AnimationRuleByMask, -1);
        }
    }

    /// <summary>Read-only Editor diagnostics; the native Tilemap owns actual rendering.</summary>
    public static class RuleTileBuilderValidation
    {
        public static string CheckCompatibility(RuleTile tile)
        {
            if (tile == null) return "Select a native RuleTile asset or create a draft.";
            if (tile.GetType() != typeof(RuleTile)) return "Only the standard square-grid RuleTile is supported. Edit this derived tile in its own Inspector.";
            if (tile.m_TilingRules == null) return "The rule list is missing. Restore the native RuleTile rule list before opening it.";
            for (int index = 0; index < tile.m_TilingRules.Count; index++)
            {
                var rule = tile.m_TilingRules[index];
                string label = "Rule " + (index + 1) + ": ";
                if (rule == null) return label + "remove the missing rule entry.";
                if (rule.m_Neighbors == null || rule.m_NeighborPositions == null || rule.m_Neighbors.Count != rule.m_NeighborPositions.Count)
                    return label + "neighbor conditions and positions must have equal lengths.";
                var positions = new HashSet<Vector3Int>();
                for (int i = 0; i < rule.m_Neighbors.Count; i++)
                {
                    var position = rule.m_NeighborPositions[i];
                    if (position.z != 0 || Mathf.Abs(position.x) > 1 || Mathf.Abs(position.y) > 1 || position == Vector3Int.zero)
                        return label + "only the eight non-center cells in a 3×3 square neighborhood are supported.";
                    if (!positions.Add(position)) return label + "remove duplicate neighbor positions.";
                    if (rule.m_Neighbors[i] < 0 || rule.m_Neighbors[i] > 2)
                        return label + "use Same surface, Other surface, or Any; custom neighbor conditions are unsupported.";
                }
            }
            return null;
        }

        public static RuleTileValidationReport Analyze(RuleTile tile)
        {
            var report = new RuleTileValidationReport();
            string compatibility = CheckCompatibility(tile);
            if (compatibility != null) { report.Errors.Add(compatibility); return report; }
            ValidateMissingReferences(tile, report);
            ValidateSprite(tile.m_DefaultSprite, "Fallback sprite", report);
            if (!Enum.IsDefined(typeof(Tile.ColliderType), tile.m_DefaultColliderType))
                report.Errors.Add("Fallback collider is invalid; select None, Sprite, or Grid.");
            ValidatePrefab(tile.m_DefaultGameObject, "Fallback object", report);
            var coverages = new List<bool[]>();
            var ids = new HashSet<int>();
            for (int index = 0; index < tile.m_TilingRules.Count; index++)
            {
                var rule = tile.m_TilingRules[index];
                string label = "Rule " + (index + 1);
                ValidateOutput(rule, label, report);
                if (!ids.Add(rule.m_Id)) report.Warnings.Add(label + " repeats a rule ID; use unique IDs when duplicating rules.");
                var coverage = new bool[256];
                int matched = 0;
                int visible = 0;
                int animated = 0;
                for (int mask = 0; mask < 256; mask++)
                {
                    if (!Matches(tile, rule, mask)) continue;
                    coverage[mask] = true;
                    matched++;
                    if (report.FirstRuleByMask[mask] < 0) { report.FirstRuleByMask[mask] = index; visible++; }
                    if (rule.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Animation && report.AnimationRuleByMask[mask] < 0)
                    { report.AnimationRuleByMask[mask] = index; animated++; }
                }
                for (int earlier = 0; earlier < coverages.Count; earlier++)
                {
                    if (!SameCoverage(coverage, coverages[earlier])) continue;
                    report.Warnings.Add(label + " duplicates the effective neighborhood masks of rule " + (earlier + 1) + ". Remove it or refine its conditions.");
                    break;
                }
                if (matched == 0) report.Warnings.Add(label + " matches no neighborhood; check its conditions and transform.");
                else if (visible == 0)
                    report.Warnings.Add(label + " is hidden visually by earlier rules. Reorder or refine it." + (animated > 0 ? " Its animation still matches independently." : ""));
                else if (visible < matched)
                    report.Warnings.Add(label + " overlaps earlier rules in " + (matched - visible) + " neighborhoods; earlier rules supply the visual output there.");
                coverages.Add(coverage);
            }
            int fallback = 0;
            for (int mask = 0; mask < 256; mask++) if (report.FirstRuleByMask[mask] < 0) fallback++;
            if (fallback > 0) report.Warnings.Add(fallback + " of 256 neighborhoods use the fallback sprite. Assign optional pieces or intentionally keep this fallback.");
            if (RuleTileBuilderTemplates.TryRecognize(tile, out var kind))
            {
                int missing = RuleTileBuilderTemplates.GetSlots(kind).Count - tile.m_TilingRules.Count;
                if (missing > 0) report.Warnings.Add(missing + " optional " + kind + " pieces are unassigned; their neighborhoods use fallback. Complete them later if needed.");
            }
            return report;
        }

        public static bool Matches(RuleTile tile, RuleTile.TilingRule rule, int mask)
        {
            if (tile == null || rule == null || mask < 0 || mask > 255 || rule.m_Neighbors == null ||
                rule.m_NeighborPositions == null || rule.m_Neighbors.Count != rule.m_NeighborPositions.Count) return false;
            if (MatchAt(tile, rule, mask, 0, false, false)) return true;
            switch (rule.m_RuleTransform)
            {
                case RuleTile.TilingRuleOutput.Transform.Rotated:
                    for (int angle = tile.m_RotationAngle; angle < 360; angle += tile.m_RotationAngle)
                        if (MatchAt(tile, rule, mask, angle, false, false)) return true;
                    break;
                case RuleTile.TilingRuleOutput.Transform.MirrorX:
                    return MatchAt(tile, rule, mask, 0, true, false);
                case RuleTile.TilingRuleOutput.Transform.MirrorY:
                    return MatchAt(tile, rule, mask, 0, false, true);
                case RuleTile.TilingRuleOutput.Transform.MirrorXY:
                    return MatchAt(tile, rule, mask, 0, true, true) || MatchAt(tile, rule, mask, 0, true, false) || MatchAt(tile, rule, mask, 0, false, true);
                case RuleTile.TilingRuleOutput.Transform.RotatedMirror:
                    for (int angle = 0; angle < 360; angle += tile.m_RotationAngle)
                        if (MatchAt(tile, rule, mask, angle, true, false) || (angle != 0 && MatchAt(tile, rule, mask, angle, false, false))) return true;
                    break;
            }
            return false;
        }

        private static bool MatchAt(RuleTile tile, RuleTile.TilingRule rule, int mask, int angle, bool mirrorX, bool mirrorY)
        {
            for (int index = 0; index < rule.m_Neighbors.Count; index++)
            {
                Vector3Int position = rule.m_NeighborPositions[index];
                if (mirrorX || mirrorY) position = tile.GetMirroredPosition(position, mirrorX, mirrorY);
                if (angle != 0) position = tile.GetRotatedPosition(position, angle);
                int bit = -1;
                for (int i = 0; i < 8; i++) if (RuleTileBuilderTemplates.GetPosition(i) == position) { bit = i; break; }
                if (bit < 0) return false;
                if (!tile.RuleMatch(rule.m_Neighbors[index], (mask & (1 << bit)) != 0 ? tile : null)) return false;
            }
            return true;
        }

        private static void ValidateOutput(RuleTile.TilingRule rule, string label, RuleTileValidationReport report)
        {
            if (rule.m_Sprites == null || rule.m_Sprites.Length == 0) report.Errors.Add(label + ": assign at least one output sprite or remove the optional rule.");
            else for (int i = 0; i < rule.m_Sprites.Length; i++) ValidateSprite(rule.m_Sprites[i], label + " sprite " + (i + 1), report);
            if (!Enum.IsDefined(typeof(RuleTile.TilingRuleOutput.OutputSprite), rule.m_Output)) report.Errors.Add(label + ": choose Single, Random, or Animation output.");
            if (!Enum.IsDefined(typeof(RuleTile.TilingRuleOutput.Transform), rule.m_RuleTransform) || !Enum.IsDefined(typeof(RuleTile.TilingRuleOutput.Transform), rule.m_RandomTransform))
                report.Errors.Add(label + ": select a valid native transform.");
            if (!Enum.IsDefined(typeof(Tile.ColliderType), rule.m_ColliderType)) report.Errors.Add(label + ": select a valid collider type.");
            if (rule.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Random && (!Finite(rule.m_PerlinScale) || rule.m_PerlinScale <= 0))
                report.Errors.Add(label + ": random noise scale must be finite and greater than zero.");
            if (rule.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Animation &&
                (!Finite(rule.m_MinAnimationSpeed) || !Finite(rule.m_MaxAnimationSpeed) || rule.m_MinAnimationSpeed <= 0 || rule.m_MaxAnimationSpeed < rule.m_MinAnimationSpeed))
                report.Errors.Add(label + ": animation speeds must be finite, positive, and maximum must be at least minimum.");
            ValidatePrefab(rule.m_GameObject, label + " object", report);
        }

        private static void ValidateSprite(Sprite sprite, string label, RuleTileValidationReport report)
        {
            if (sprite == null) report.Errors.Add(label + " is missing; assign an imported sprite asset.");
            else if (!EditorUtility.IsPersistent(sprite) || !AssetDatabase.Contains(sprite) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                report.Errors.Add(label + " is temporary; assign a persistent sprite asset before saving.");
        }

        private static void ValidatePrefab(GameObject prefab, string label, RuleTileValidationReport report)
        {
            if (prefab != null && (!EditorUtility.IsPersistent(prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab)))
                report.Errors.Add(label + " must reference a persistent prefab asset, not a scene object.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateMissingReferences(RuleTile tile, RuleTileValidationReport report)
        {
            using var serialized = new SerializedObject(tile);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                    property.objectReferenceEntityIdValue.IsValid())
                    report.Errors.Add(property.propertyPath + " references a missing asset; clear or replace that reference before saving.");
            }
        }

        private static bool SameCoverage(bool[] first, bool[] second)
        {
            for (int i = 0; i < 256; i++) if (first[i] != second[i]) return false;
            return true;
        }
    }
}

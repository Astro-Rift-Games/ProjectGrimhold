using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    public static class RuleTileBuilderAssets
    {
        public static RuleTile CloneDraft(RuleTile source)
        {
            var incompatibility = RuleTileBuilderValidation.CheckCompatibility(source);
            if (incompatibility != null) throw new InvalidOperationException(incompatibility);
            var draft = UnityEngine.Object.Instantiate(source);
            draft.name = source.name;
            draft.hideFlags = HideFlags.HideAndDontSave;
            draft.UpdateNeighborPositions();
            return draft;
        }

        // Include unsaved Inspector edits as well as on-disk changes. Neither may be overwritten silently.
        public static string Fingerprint(RuleTile source)
        {
            if (!source) return string.Empty;
            var path = AssetDatabase.GetAssetPath(source);
            var disk = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "missing";
            var text = AssetDatabase.AssetPathToGUID(path) + "\n" + EditorJsonUtility.ToJson(source) + "\n" + disk;
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static string ValidateNewPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("\\") || path.Contains("//") || path.Contains("/../") || path.Contains("/./") ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return "Choose an .asset path inside an existing Assets folder.";
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith("."))
                return "Choose a valid asset name without reserved filename characters.";
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) return "Select an existing destination folder inside Assets.";
            if (AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                return "That path is already occupied. Choose another name, or open the existing RuleTile explicitly.";
            return null;
        }

        public static RuleTile Create(RuleTile draft, string path, bool warningsConfirmed = false)
        {
            var error = ValidateNewPath(path);
            if (error != null) throw new InvalidOperationException(error);
            ValidateForSave(draft, warningsConfirmed);
            var asset = UnityEngine.Object.Instantiate(draft);
            asset.name = Path.GetFileNameWithoutExtension(path);
            asset.hideFlags = HideFlags.None;
            try
            {
                AssetDatabase.CreateAsset(asset, path);
                Undo.IncrementCurrentGroup();
                Undo.RegisterCreatedObjectUndo(asset, "Create RuleTile");
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                Undo.IncrementCurrentGroup();
                return asset;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }

        public static void Update(RuleTile source, RuleTile draft, string expectedFingerprint, bool warningsConfirmed = false)
        {
            if (!source || !EditorUtility.IsPersistent(source) || AssetDatabase.IsSubAsset(source))
                throw new InvalidOperationException("Open a standalone RuleTile asset before updating it.");
            var incompatibility = RuleTileBuilderValidation.CheckCompatibility(source);
            if (incompatibility != null) throw new InvalidOperationException(incompatibility);
            if (string.IsNullOrEmpty(expectedFingerprint) || Fingerprint(source) != expectedFingerprint)
                throw new InvalidOperationException("The original asset changed outside this draft. Reopen it before saving; your draft has not been applied.");
            foreach (var guid in AssetDatabase.FindAssets("t:RuleOverrideTile"))
            {
                var dependent = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.RuleOverrideTile>(AssetDatabase.GUIDToAssetPath(guid));
                if (dependent && dependent.m_Tile == source)
                    throw new InvalidOperationException("This tile is used by RuleOverrideTile '" + dependent.name +
                        "'. Updating its generated dependents is not supported here. Use the standard Inspector or save a new asset.");
            }
            ValidateForSave(draft, warningsConfirmed);
            var name = source.name;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(source, "Update RuleTile");
            EditorUtility.CopySerialized(draft, source);
            source.name = name;
            source.hideFlags = HideFlags.None;
            source.UpdateNeighborPositions();
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
            Undo.IncrementCurrentGroup();
        }

        private static void ValidateForSave(RuleTile draft, bool warningsConfirmed)
        {
            var report = RuleTileBuilderValidation.Analyze(draft);
            if (report.HasErrors) throw new InvalidOperationException(string.Join("\n", report.Errors));
            if (report.Warnings.Count != 0 && !warningsConfirmed)
                throw new InvalidOperationException("Review the warnings and explicitly confirm saving with fallback before generating this asset.");
        }
    }
}

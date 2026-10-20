using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.RuleTileBuilder
{
    public sealed class RuleTileBuilderWindowTests
    {
        private const string Prefix = "ProjectGrimhold.EditorTools.RuleTiles.";

        [Test]
        public void SpriteCatalog_AcceptsSlicedAtlasAndIndividualSpritesWithoutReimporting()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(Prefix + "RuleTileBuilderSpriteCatalog"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Sprite catalog must exist.");
            const string path = "Assets/Art/Tilesets/Catacombs/Catacombs-BaseTileset.png";
            var before = AssetDatabase.GetAssetDependencyHash(path);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var resolve = type.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
            var sprites = (Sprite[])resolve.Invoke(null, new object[] { new Object[] { atlas } });
            Assert.That(sprites.Length, Is.EqualTo(54));
            var mixed = (Sprite[])resolve.Invoke(null, new object[] { new Object[] { atlas, sprites[0] } });
            Assert.That(mixed.Length, Is.EqualTo(54), "Duplicate sprite sources should be deduplicated.");
            Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(before));
        }

        [Test]
        public void Window_HasMenuAndCreatesNativeDraftWithoutChangingSelection()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(Prefix + "RuleTileBuilderWindow"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Guided EditorWindow must exist.");
            var menu = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SelectMany(m => m.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>()).ToArray();
            Assert.That(menu.Any(m => m.menuItem == "Tools/Grimhold/Rule Tile Builder"), Is.True);
            var selection = Selection.activeObject;
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            try
            {
                var draft = type.GetProperty("Draft").GetValue(window) as RuleTile;
                Assert.That(draft, Is.Not.Null);
                Assert.That(draft.GetType(), Is.EqualTo(typeof(RuleTile)));
                Assert.That(EditorUtility.IsPersistent(draft), Is.False);
                Assert.That(Selection.activeObject, Is.EqualTo(selection));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void PreviewHover_DifferentLayoutAndRepaintPositions_DoNotChangeControlCount()
        {
            var type = typeof(ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderWindow);
            var window = (ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderWindow)ScriptableObject.CreateInstance(type);
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png").OfType<Sprite>().First();
            try
            {
                window.Draft.m_DefaultSprite = sprite;
                window.ApplySlot(ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderTemplates.GetSlots(ProjectGrimhold.EditorTools.RuleTiles.RuleTileTemplateKind.Floor)[0], new[] { sprite });
                using (var serialized = new SerializedObject(window))
                {
                    serialized.FindProperty("_step").intValue = 3;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                window.ShowUtility();
                window.position = new Rect(100, 80, 1120, 820);
                window.SendEvent(new Event { type = EventType.Layout, mousePosition = new Vector2(-100, -100) });
                window.SendEvent(new Event { type = EventType.Repaint, mousePosition = new Vector2(600, 430) });
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [Test]
        public void GuidedAssignment_SaveReopenCompleteAndSave_PreservesGuid()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(Prefix + "RuleTileBuilderWindow"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null);
            string folder = "Assets/__RuleTileBuilderWindowTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png").OfType<Sprite>().First();
            try
            {
                var draft = (RuleTile)type.GetProperty("Draft").GetValue(window);
                draft.m_DefaultSprite = sprite;
                var slot = ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderTemplates.GetSlots(ProjectGrimhold.EditorTools.RuleTiles.RuleTileTemplateKind.FullContour)[0];
                type.GetMethod("ApplySlot").Invoke(window, new object[] { slot, new[] { sprite } });
                Assert.That(window.hasUnsavedChanges, Is.True);
                string path = folder + "/Guided.asset";
                type.GetMethod("SaveTo").Invoke(window, new object[] { path, true });
                var source = (RuleTile)type.GetProperty("Source").GetValue(window);
                string guid = AssetDatabase.AssetPathToGUID(path);
                type.GetMethod("LoadAsset").Invoke(window, new object[] { source });
                var second = ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderTemplates.GetSlots(ProjectGrimhold.EditorTools.RuleTiles.RuleTileTemplateKind.FullContour)[1];
                type.GetMethod("ApplySlot").Invoke(window, new object[] { second, new[] { sprite } });
                Assert.That(source.m_TilingRules.Count, Is.EqualTo(1), "Only Save changes the original.");
                type.GetMethod("SaveTo").Invoke(window, new object[] { null, true });
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Assert.That(AssetDatabase.LoadAssetAtPath<RuleTile>(path).m_TilingRules.Count, Is.EqualTo(2));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
            finally
            {
                window.DiscardChanges();
                Object.DestroyImmediate(window);
                Undo.ClearAll();
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}

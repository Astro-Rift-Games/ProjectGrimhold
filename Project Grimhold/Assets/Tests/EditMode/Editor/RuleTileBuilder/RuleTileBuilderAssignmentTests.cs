using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectGrimhold.EditorTools.RuleTiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Tests.EditMode.RuleTileBuilder
{
    public sealed class RuleTileBuilderAssignmentTests
    {
        private static Type Scenarios => typeof(RuleTileBuilderWindow).Assembly.GetType(
            "ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderAssignmentScenarios");

        private static object Call(string method, params object[] args)
        {
            Assert.That(Scenarios, Is.Not.Null, "Illustrative assignment scenarios must exist.");
            return Scenarios.GetMethod(method).Invoke(null, args);
        }

        private static object[] Examples(RuleTileTemplateKind kind) => ((IEnumerable)Call("GetScenarios", kind)).Cast<object>().ToArray();
        private static Vector3Int[] Cells(object example) => ((IEnumerable)example.GetType().GetProperty("Cells").GetValue(example)).Cast<Vector3Int>().ToArray();
        private static RuleTileTemplateSlot Slot(object example, RuleTileTemplateKind kind, Vector3Int position) =>
            (RuleTileTemplateSlot)example.GetType().GetMethod("GetSlot").Invoke(example, new object[] { kind, position });

        [TestCase(RuleTileTemplateKind.Floor, 1)]
        [TestCase(RuleTileTemplateKind.Orthogonal, 16)]
        [TestCase(RuleTileTemplateKind.FullContour, 47)]
        public void Examples_CoverEverySlotAndEachCellHasExactlyOneNativeCondition(RuleTileTemplateKind kind, int count)
        {
            var reached = new HashSet<string>();
            foreach (var example in Examples(kind))
            {
                var cells = new HashSet<Vector3Int>(Cells(example));
                foreach (var cell in cells)
                {
                    int mask = 0;
                    for (int bit = 0; bit < 8; bit++)
                        if (cells.Contains(cell + RuleTileBuilderTemplates.GetPosition(bit))) mask |= 1 << bit;
                    var matches = RuleTileBuilderTemplates.GetSlots(kind).Where(s => (mask & s.ConditionMask) == s.Mask).ToArray();
                    Assert.That(matches.Length, Is.EqualTo(1));
                    Assert.That(Slot(example, kind, cell).Key, Is.EqualTo(matches[0].Key));
                    reached.Add(matches[0].Key);
                }
                Assert.That(Slot(example, kind, new Vector3Int(-1, -1, 0)), Is.Null);
            }
            Assert.That(reached.Count, Is.EqualTo(count));
        }

        [Test]
        public void PieceNames_DescribeExposedEdgesRatherThanConnectionDirection()
        {
            var top = RuleTileBuilderTemplates.GetSlots(RuleTileTemplateKind.Orthogonal).Single(s => s.Mask == (8 | 16 | 64));
            Assert.That(Call("GetPieceName", top), Is.EqualTo("Top edge"));
            Assert.That((string)Call("GetDescription", top), Does.Contain("left").And.Contain("right").And.Contain("below"));
        }

        [TestCase(RuleTileTemplateKind.Orthogonal)]
        [TestCase(RuleTileTemplateKind.FullContour)]
        public void EveryIllustratedCell_MatchesNativeTilemapOutput(RuleTileTemplateKind kind)
        {
            var tile = ScriptableObject.CreateInstance<RuleTile>();
            var texture = new Texture2D(4, 4);
            var sprites = new List<Sprite>();
            using (var preview = new RuleTileBuilderPreview(tile))
            try
            {
                foreach (var slot in RuleTileBuilderTemplates.GetSlots(kind))
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
                    sprites.Add(sprite);
                    RuleTileBuilderTemplates.SetSprites(tile, slot, new[] { sprite });
                }
                foreach (var example in Examples(kind))
                {
                    preview.Tilemap.ClearAllTiles();
                    foreach (var cell in Cells(example)) preview.Tilemap.SetTile(cell, tile);
                    preview.Refresh();
                    foreach (var cell in Cells(example))
                    {
                        var rule = RuleTileBuilderTemplates.FindRule(tile, Slot(example, kind, cell));
                        Assert.That(preview.Tilemap.GetSprite(cell), Is.SameAs(rule.m_Sprites[0]));
                    }
                }
            }
            finally
            {
                foreach (var sprite in sprites) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void SceneSelection_AssignReplaceClearUndoAndSave_PreservesSourceAndEquivalentCells()
        {
            var type = typeof(RuleTileBuilderWindow);
            var select = type.GetMethod("SelectAssignmentCell");
            Assert.That(select, Is.Not.Null, "Window must expose the same scenario selection used by its canvas.");
            var assign = type.GetMethod("AssignSelectedPiece");
            var selected = type.GetProperty("SelectedAssignmentSlot");
            string folder = "Assets/__RuleTileAssignment_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var window = ScriptableObject.CreateInstance<RuleTileBuilderWindow>();
            var sample = AssetDatabase.LoadAssetAtPath<RuleTile>("Assets/Art/Tilesets/Catacombs/RuleTiles/CatacombsFloorRuleTile.asset");
            var source = Object.Instantiate(sample);
            AssetDatabase.CreateAsset(source, folder + "/Source.asset");
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png").OfType<Sprite>().Take(2).ToArray();
            try
            {
                window.LoadAsset(source);
                string before = EditorJsonUtility.ToJson(source);
                var example = Examples(RuleTileTemplateKind.Floor)[0];
                var cells = Cells(example);
                select.Invoke(window, new object[] { 0, cells[0] });
                var slot = (RuleTileTemplateSlot)selected.GetValue(window);
                Undo.IncrementCurrentGroup();
                assign.Invoke(window, new object[] { sprites });
                foreach (var cell in cells)
                    Assert.That(Call("GetSprite", window.Draft, Slot(example, RuleTileTemplateKind.Floor, cell)), Is.SameAs(sprites[0]));
                var rule = RuleTileBuilderTemplates.FindRule(window.Draft, slot);
                Assert.That(rule.m_Output, Is.EqualTo(RuleTile.TilingRuleOutput.OutputSprite.Random));
                rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
                Undo.IncrementCurrentGroup();
                assign.Invoke(window, new object[] { new[] { sprites[1] } });
                Assert.That(RuleTileBuilderTemplates.FindRule(window.Draft, slot).m_Output, Is.EqualTo(RuleTile.TilingRuleOutput.OutputSprite.Animation));
                var nativePreview = (RuleTileBuilderPreview)type.GetField("_preview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                Assert.That(nativePreview.Tilemap.GetTile(Vector3Int.zero), Is.SameAs(window.Draft));
                Assert.That(nativePreview.Tilemap.GetSprite(Vector3Int.zero), Is.SameAs(sprites[1]), "Assignments refresh the existing native test grid immediately.");
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(RuleTileBuilderTemplates.FindRule(window.Draft, slot).m_Sprites, Is.EqualTo(sprites));
                Undo.PerformRedo();
                Assert.That(RuleTileBuilderTemplates.FindRule(window.Draft, (RuleTileTemplateSlot)selected.GetValue(window)).m_Sprites, Is.EqualTo(new[] { sprites[1] }));
                string guid = AssetDatabase.AssetPathToGUID(folder + "/Source.asset");
                window.SaveTo(null, true);
                window.LoadAsset(source);
                Assert.That(AssetDatabase.AssetPathToGUID(folder + "/Source.asset"), Is.EqualTo(guid));
                Assert.That(source.m_TilingRules[0].m_Sprites[0], Is.SameAs(sprites[1]));
                select.Invoke(window, new object[] { 0, cells[0] });
                assign.Invoke(window, new object[] { Array.Empty<Sprite>() });
                foreach (var cell in cells) Assert.That(Call("GetSprite", window.Draft, Slot(example, RuleTileTemplateKind.Floor, cell)), Is.SameAs(window.Draft.m_DefaultSprite));
                Assert.That(source.m_TilingRules.Count, Is.EqualTo(1));
                Assert.That(window.hasUnsavedChanges, Is.True);
            }
            finally
            {
                window.DiscardChanges();
                Object.DestroyImmediate(window);
                Undo.ClearAll();
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void PendingNavigation_ReachesEveryContourPieceWithoutAssigningAnything()
        {
            var next = typeof(RuleTileBuilderWindow).GetMethod("NextUnassignedPiece");
            Assert.That(next, Is.Not.Null);
            var window = ScriptableObject.CreateInstance<RuleTileBuilderWindow>();
            try
            {
                using (var serialized = new SerializedObject(window))
                {
                    serialized.FindProperty("_template").enumValueIndex = (int)RuleTileTemplateKind.FullContour;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var selected = typeof(RuleTileBuilderWindow).GetProperty("SelectedAssignmentSlot");
                var reached = new HashSet<string>();
                string before = EditorJsonUtility.ToJson(window.Draft);
                for (int i = 0; i < 47; i++)
                {
                    Assert.That(next.Invoke(window, null), Is.True);
                    reached.Add(((RuleTileTemplateSlot)selected.GetValue(window)).Key);
                }
                Assert.That(reached.Count, Is.EqualTo(47));
                Assert.That(EditorJsonUtility.ToJson(window.Draft), Is.EqualTo(before));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
            finally { window.DiscardChanges(); Object.DestroyImmediate(window); }
        }

        [TestCase(860, 660)]
        [TestCase(1220, 1000)]
        public void NativeWindow_CanvasClickAndDrop_SelectTheDisplayedPieceAndRefreshOutput(int width, int height)
        {
            var window = ScriptableObject.CreateInstance<RuleTileBuilderWindow>();
            var type = typeof(RuleTileBuilderWindow);
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png").OfType<Sprite>().Take(2).ToArray();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            var selection = Selection.activeObject;
            var before = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(sprites[0]));
            try
            {
                window.Draft.m_DefaultSprite = sprites[0];
                using (var serialized = new SerializedObject(window))
                {
                    serialized.FindProperty("_template").enumValueIndex = (int)RuleTileTemplateKind.FullContour;
                    serialized.FindProperty("_step").intValue = 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                window.ShowUtility();
                window.position = new Rect(100, 80, width, height);
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint, mousePosition = new Vector2(-100, -100) });
                var rects = (Dictionary<Vector3Int, Rect>)type.GetField("_assignmentRects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var cell = new Vector3Int(5, 7, 0); // Actual top edge of the room.
                var rect = rects[cell];
                var viewport = (Rect)type.GetField("_assignmentViewport", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                Assert.That(viewport.Contains(rect.center), Is.True, "Top edge remains accessible at minimum window size.");
                Assert.That(viewport.yMax, Is.LessThan(height - 25), "Canvas scrolls independently below the assignment controls.");
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = rect.center });
                Assert.That((RuleTileTemplateSlot)type.GetProperty("SelectedAssignmentSlot").GetValue(window), Is.Not.Null);
                Assert.That(Call("GetPieceName", type.GetProperty("SelectedAssignmentSlot").GetValue(window)), Is.EqualTo("Top edge"));
                window.SendEvent(new Event { type = EventType.Layout });
                DragAndDrop.objectReferences = sprites.Cast<Object>().ToArray();
                window.SendEvent(new Event { type = EventType.DragPerform, mousePosition = rect.center });
                var slot = (RuleTileTemplateSlot)type.GetProperty("SelectedAssignmentSlot").GetValue(window);
                Assert.That(RuleTileBuilderTemplates.FindRule(window.Draft, slot).m_Sprites, Is.EqualTo(sprites));
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint, mousePosition = rect.center });
                Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(sprites[0])), Is.EqualTo(before));
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(scene));
                Assert.That(scene.isDirty, Is.EqualTo(dirty));
                Assert.That(Selection.activeObject, Is.EqualTo(selection));
                var textures = (Dictionary<Texture2D, Texture2D>)type.GetField("_assignmentTextures", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                var owned = textures.Values.ToArray();
                Assert.That(owned.Length, Is.GreaterThan(0));
                Assert.That(owned.All(t => t.filterMode == FilterMode.Point), Is.True);
                window.DiscardChanges();
                window.Close();
                Assert.That(owned.All(t => !t), Is.True, "Owned point-filtered presentation copies must be released.");
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                DragAndDrop.objectReferences = Array.Empty<Object>();
                if (window) { window.DiscardChanges(); window.Close(); }
                Undo.ClearAll();
            }
        }
    }
}

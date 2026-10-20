#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectGrimhold.EditorTools.RuleTiles.Tests
{
    public sealed class RuleTileBuilderPreviewTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private object _preview;
        private Type _previewType;
        private RuleTile _draft;
        private Sprite _first;
        private Sprite _second;
        private Scene _activeScene;
        private bool _sceneDirty;
        private Object _selection;
        private int _previewSceneCount;

        [SetUp]
        public void SetUp()
        {
            _previewType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectGrimhold.EditorTools.RuleTiles.RuleTileBuilderPreview"))
                .FirstOrDefault(type => type != null);
            Assert.That(_previewType, Is.Not.Null, "The isolated native RuleTile preview must exist.");
            _draft = Own(ScriptableObject.CreateInstance<RuleTile>());
            var texture = Own(new Texture2D(4, 2));
            _first = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2));
            _second = Own(Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.one * .5f, 2));
            _draft.m_DefaultSprite = _first;
            _activeScene = SceneManager.GetActiveScene();
            _sceneDirty = _activeScene.isDirty;
            _selection = Selection.activeObject;
            _previewSceneCount = EditorSceneManager.previewSceneCount;
            _preview = Activator.CreateInstance(_previewType, new object[] { _draft });
        }

        [TearDown]
        public void TearDown()
        {
            (_preview as IDisposable)?.Dispose();
            _preview = null;
            for (var i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i]) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void PaintingUsesExactDraftAndForeignIsNotThis()
        {
            var rule = Rule(_second);
            rule.m_Neighbors[4] = RuleTile.TilingRuleOutput.Neighbor.This;
            _draft.m_TilingRules.Add(rule);
            Call("SetCell", Vector3Int.zero, 1);
            Call("SetCell", Vector3Int.right, 1);
            Assert.That(Map.GetTile(Vector3Int.zero), Is.SameAs(_draft));
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
            Call("SetCell", Vector3Int.right, 2);
            Assert.That(Map.GetTile(Vector3Int.right), Is.Not.Null.And.Not.SameAs(_draft));
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_first));
            rule.m_Neighbors[4] = RuleTile.TilingRuleOutput.Neighbor.NotThis;
            Call("Refresh");
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
            Call("SetCell", Vector3Int.right, 0);
            Assert.That(Map.GetTile(Vector3Int.right), Is.Null);
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
        }

        [Test]
        public void MaskCountsOnlyExactDraftInNativeNeighborOrder()
        {
            Call("SetCell", new Vector3Int(-1, 1, 0), 1);
            Call("SetCell", new Vector3Int(0, 1, 0), 2);
            Call("SetCell", new Vector3Int(1, -1, 0), 1);
            Assert.That(Call("GetMask", Vector3Int.zero), Is.EqualTo(129));
        }

        [Test]
        public void DraftReplacementPreservesPaintedLayoutAndForeignIdentity()
        {
            Call("SetCell", Vector3Int.zero, 1);
            Call("SetCell", Vector3Int.left, 2);
            var foreign = Map.GetTile(Vector3Int.left);
            var replacement = Own(ScriptableObject.CreateInstance<RuleTile>());
            replacement.m_DefaultSprite = _second;
            Call("SetDraft", replacement);
            Assert.That(Map.GetTile(Vector3Int.zero), Is.SameAs(replacement));
            Assert.That(Map.GetTile(Vector3Int.left), Is.SameAs(foreign));
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
        }

        [Test]
        public void RefreshRebuildsNativeNeighborCacheAfterRuleEdit()
        {
            var rule = Rule(_second);
            rule.m_Neighbors[1] = RuleTile.TilingRuleOutput.Neighbor.This;
            _draft.m_TilingRules.Add(rule);
            Call("SetCell", Vector3Int.zero, 1);
            Call("Refresh");
            Assert.That(_draft.neighborPositions, Does.Contain(Vector3Int.up));
            rule.m_Neighbors.Clear();
            rule.m_NeighborPositions.Clear();
            rule.m_Neighbors.Add(RuleTile.TilingRuleOutput.Neighbor.This);
            rule.m_NeighborPositions.Add(Vector3Int.right);
            Call("Refresh");
            Call("SetCell", Vector3Int.right, 1);
            Assert.That(_draft.neighborPositions, Does.Contain(Vector3Int.right));
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
        }

        [Test]
        public void PreviewUsesNativeRuleTransform()
        {
            var rule = Rule(_second);
            rule.m_Neighbors[1] = RuleTile.TilingRuleOutput.Neighbor.This;
            rule.m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Rotated;
            _draft.m_TilingRules.Add(rule);
            Call("SetCell", Vector3Int.zero, 1);
            Call("SetCell", Vector3Int.right, 1);
            Call("Refresh");
            Assert.That(Map.GetSprite(Vector3Int.zero), Is.SameAs(_second));
            Assert.That(Map.GetTransformMatrix(Vector3Int.zero), Is.Not.EqualTo(Matrix4x4.identity));
        }

        [Test]
        public void AnimationAdvancesUsingNativeAnimationSearchDespiteEarlierSingleRule()
        {
            _draft.m_TilingRules.Add(Rule(_first));
            var animation = Rule(_first);
            animation.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
            animation.m_Sprites = new[] { _first, _second };
            animation.m_MinAnimationSpeed = animation.m_MaxAnimationSpeed = 1;
            _draft.m_TilingRules.Add(animation);
            Call("SetCell", Vector3Int.zero, 1);
            Call("Refresh");
            Call("AdvanceAnimation", 0d);
            Assert.That(Map.GetAnimationFrameCount(Vector3Int.zero), Is.EqualTo(2));
            var frame = Map.GetAnimationFrame(Vector3Int.zero);
            Call("AdvanceAnimation", 1.1d);
            Assert.That(Map.GetAnimationFrame(Vector3Int.zero), Is.Not.EqualTo(frame));
        }

        [TestCase("Rectangle", 48)]
        [TestCase("L", 27)]
        [TestCase("Hole", 44)]
        [TestCase("Corridor", 8)]
        [TestCase("Isolated", 1)]
        [TestCase("Clear", 0)]
        public void PatternsHaveExpectedOccupancy(string pattern, int count)
        {
            Call("LoadPattern", pattern);
            Assert.That(Map.GetUsedTilesCount(), Is.EqualTo(count == 0 ? 0 : 1));
            Assert.That(Map.GetTilesBlock(Map.cellBounds).Count(tile => tile == _draft), Is.EqualTo(count));
        }

        [Test]
        public void PaintingCoordinatesMatchCenteredSquareGridAndRejectLetterbox()
        {
            var rectangle = new Rect(10, 20, 240, 120);
            var args = new object[] { rectangle, new Vector2(71, 21), Vector3Int.zero };
            Assert.That(Call("TryGetCell", args), Is.True);
            Assert.That(args[2], Is.EqualTo(new Vector3Int(-6, 5, 0)));
            args = new object[] { rectangle, new Vector2(189, 139), Vector3Int.zero };
            Assert.That(Call("TryGetCell", args), Is.True);
            Assert.That(args[2], Is.EqualTo(new Vector3Int(5, -6, 0)));
            args = new object[] { rectangle, new Vector2(11, 21), Vector3Int.zero };
            Assert.That(Call("TryGetCell", args), Is.False);
        }

        [Test]
        public void SandboxDoesNotDirtyActiveSceneOrChangeSelectionAndDisposesObjects()
        {
            var previewMap = Map;
            Assert.That(EditorSceneManager.IsPreviewScene(previewMap.gameObject.scene), Is.True);
            Assert.That(previewMap.gameObject.scene, Is.Not.EqualTo(_activeScene));
            Call("SetCell", Vector3Int.zero, 2);
            var foreignTile = previewMap.GetTile(Vector3Int.zero);
            var foreignSprite = previewMap.GetSprite(Vector3Int.zero);
            var foreignTexture = foreignSprite.texture;
            Call("LoadPattern", "Rectangle");
            Call("Dispose");
            Assert.That(previewMap == null, Is.True);
            Assert.That(foreignTile == null, Is.True);
            Assert.That(foreignSprite == null, Is.True);
            Assert.That(foreignTexture == null, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_activeScene));
            Assert.That(_activeScene.isDirty, Is.EqualTo(_sceneDirty));
            Assert.That(Selection.activeObject, Is.SameAs(_selection));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(_previewSceneCount));
        }

        [Test]
        public void PreviewNeverInstantiatesRuleGameObjects()
        {
            var source = EditorUtility.CreateGameObjectWithHideFlags("RuleTileBuilderPreviewTestSource", HideFlags.HideAndDontSave);
            _owned.Add(source);
            _draft.m_DefaultGameObject = source;
            var rule = Rule(_second);
            rule.m_GameObject = source;
            _draft.m_TilingRules.Add(rule);
            Call("SetCell", Vector3Int.zero, 1);
            Call("Refresh");
            Assert.That(Map.GetInstantiatedObject(Vector3Int.zero), Is.Null);
            Assert.That(Map.transform.childCount, Is.Zero);
        }

        private Tilemap Map => (Tilemap)_previewType.GetProperty("Tilemap").GetValue(_preview);
        private object Call(string method, params object[] args) => _previewType.GetMethod(method).Invoke(_preview, args);
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static RuleTile.TilingRule Rule(Sprite sprite)
        {
            return new RuleTile.TilingRule
            {
                m_Sprites = new[] { sprite },
                m_Neighbors = new List<int>(new int[8]),
                m_ColliderType = Tile.ColliderType.None
            };
        }
    }
}
#endif

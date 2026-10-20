using System;
using System.Linq;
using NUnit.Framework;
using ProjectGrimhold.EditorTools.RuleTiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Tests.EditMode.RuleTileBuilder
{
    public sealed class RuleTileBuilderAssetTests
    {
        private string _folder;
        private RuleTile _draft;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__RuleTileBuilderTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            _sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png")
                .OfType<Sprite>().First();
            _draft = ScriptableObject.CreateInstance<RuleTile>();
            _draft.m_DefaultSprite = _sprite;
            _draft.m_DefaultColliderType = Tile.ColliderType.None;
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (_draft) Object.DestroyImmediate(_draft);
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void CreateAndReload_PreservesNativeFieldsAndIdentity()
        {
            var path = _folder + "/New.asset";
            var tile = RuleTileBuilderAssets.Create(_draft, path, true);
            var guid = AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.GetType(), Is.EqualTo(typeof(RuleTile)));
            Assert.That(loaded.m_DefaultSprite, Is.EqualTo(_sprite));
            Assert.That(loaded.m_DefaultColliderType, Is.EqualTo(Tile.ColliderType.None));
            Assert.That(loaded.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(tile, Is.Not.EqualTo(_draft));
        }

        [Test]
        public void UpdateAndUndo_PreservesGuidAndReferencingTile()
        {
            var path = _folder + "/Existing.asset";
            var tile = RuleTileBuilderAssets.Create(_draft, path, true);
            var guid = AssetDatabase.AssetPathToGUID(path);
            using (var reference = new NativeTilemapFixture())
            {
                reference.Map.SetTile(Vector3Int.zero, tile);
                var fingerprint = RuleTileBuilderAssets.Fingerprint(tile);
                _draft.m_DefaultColliderType = Tile.ColliderType.Grid;
                RuleTileBuilderAssets.Update(tile, _draft, fingerprint, true);
                Assert.That(tile.m_DefaultColliderType, Is.EqualTo(Tile.ColliderType.Grid));
                Assert.That(reference.Map.GetTile(Vector3Int.zero), Is.SameAs(tile));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(tile.m_DefaultColliderType, Is.EqualTo(Tile.ColliderType.None));
                Undo.PerformRedo();
                Assert.That(tile.m_DefaultColliderType, Is.EqualTo(Tile.ColliderType.Grid));
                AssetDatabase.SaveAssetIfDirty(tile);
            }
        }

        [Test]
        public void CloneAndDiscard_DoesNotChangeSource()
        {
            var source = RuleTileBuilderAssets.Create(_draft, _folder + "/Source.asset", true);
            var before = RuleTileBuilderAssets.Fingerprint(source);
            var clone = RuleTileBuilderAssets.CloneDraft(source);
            clone.m_DefaultSprite = null;
            Object.DestroyImmediate(clone);
            Assert.That(RuleTileBuilderAssets.Fingerprint(source), Is.EqualTo(before));
        }

        [Test]
        public void OccupiedPath_IsRejectedWithoutMutatingExistingAsset()
        {
            var path = _folder + "/Occupied.asset";
            var source = RuleTileBuilderAssets.Create(_draft, path, true);
            var before = RuleTileBuilderAssets.Fingerprint(source);
            Assert.Throws<InvalidOperationException>(() => RuleTileBuilderAssets.Create(_draft, path, true));
            Assert.That(RuleTileBuilderAssets.Fingerprint(source), Is.EqualTo(before));
        }

        [TestCase("Assets/../Outside.asset")]
        [TestCase("Packages/Unsafe.asset")]
        [TestCase("Assets/Rule.png")]
        [TestCase("Assets/NoSuchFolder/Rule.asset")]
        public void InvalidDestination_IsRejected(string path)
        {
            Assert.That(RuleTileBuilderAssets.ValidateNewPath(path), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SourceChangedSinceOpening_IsRejectedWithoutOverwriting()
        {
            var source = RuleTileBuilderAssets.Create(_draft, _folder + "/Conflict.asset", true);
            var baseline = RuleTileBuilderAssets.Fingerprint(source);
            source.m_DefaultColliderType = Tile.ColliderType.Grid;
            var before = RuleTileBuilderAssets.Fingerprint(source);
            Assert.Throws<InvalidOperationException>(() => RuleTileBuilderAssets.Update(source, _draft, baseline, true));
            Assert.That(RuleTileBuilderAssets.Fingerprint(source), Is.EqualTo(before));
        }

        [Test]
        public void OverrideDependent_IsRejectedWithoutMutatingEitherAsset()
        {
            var source = RuleTileBuilderAssets.Create(_draft, _folder + "/Base.asset", true);
            var dependent = ScriptableObject.CreateInstance<RuleOverrideTile>();
            dependent.m_Tile = source;
            AssetDatabase.CreateAsset(dependent, _folder + "/Override.asset");
            AssetDatabase.SaveAssetIfDirty(dependent);
            var before = RuleTileBuilderAssets.Fingerprint(source);
            Assert.Throws<InvalidOperationException>(() => RuleTileBuilderAssets.Update(source, _draft, before, true));
            Assert.That(RuleTileBuilderAssets.Fingerprint(source), Is.EqualTo(before));
            Assert.That(dependent.m_Tile, Is.SameAs(source));
        }

        [Test]
        public void WarningsRequireExplicitConfirmation_AndErrorsCannotBeConfirmedAway()
        {
            Assert.Throws<InvalidOperationException>(() => RuleTileBuilderAssets.Create(_draft, _folder + "/Warn.asset"));
            _draft.m_DefaultSprite = null;
            Assert.Throws<InvalidOperationException>(() => RuleTileBuilderAssets.Create(_draft, _folder + "/Invalid.asset", true));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/Invalid.asset"), Is.Null);
        }

        private sealed class NativeTilemapFixture : IDisposable
        {
            private readonly UnityEngine.SceneManagement.Scene _scene;
            public Tilemap Map { get; }
            public NativeTilemapFixture()
            {
                _scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                var root = new GameObject("RuleTile test grid", typeof(Grid));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, _scene);
                var child = new GameObject("RuleTile test map", typeof(Tilemap));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, _scene);
                child.transform.SetParent(root.transform);
                Map = child.GetComponent<Tilemap>();
            }
            public void Dispose() => UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(_scene);
        }
    }
}

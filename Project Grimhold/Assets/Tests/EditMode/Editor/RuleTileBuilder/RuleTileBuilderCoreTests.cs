using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Editor.RuleTileBuilder
{
    public sealed class RuleTileBuilderCoreTests
    {
        private const string Prefix = "ProjectGrimhold.EditorTools.RuleTiles.";
        private string _folder;
        private RuleTile _tile;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__RuleTileBuilderTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            var texture = new Texture2D(8, 8);
            AssetDatabase.CreateAsset(texture, _folder + "/Source.asset");
            _sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8);
            _sprite.name = "Persistent source sprite";
            AssetDatabase.AddObjectToAsset(_sprite, texture);
            AssetDatabase.SaveAssets();
            _tile = ScriptableObject.CreateInstance<RuleTile>();
            _tile.m_DefaultSprite = _sprite;
        }

        [TearDown]
        public void TearDown()
        {
            if (_tile != null) Object.DestroyImmediate(_tile);
            if (!string.IsNullOrEmpty(_folder)) AssetDatabase.DeleteAsset(_folder);
        }

        [TestCase("Floor", 1)]
        [TestCase("Orthogonal", 16)]
        [TestCase("FullContour", 47)]
        public void Templates_ContainUniqueStableSlotsAndCoverEveryNeighborhood(string kind, int count)
        {
            var slots = Slots(kind);
            Assert.That(slots.Count, Is.EqualTo(count));
            var keys = new HashSet<string>();
            foreach (var slot in slots)
            {
                Assert.That(keys.Add((string)Field(slot, "Key")), Is.True);
                Assert.That((string)Field(slot, "Label"), Is.Not.Empty);
                Assert.That((string)Field(slot, "Group"), Is.Not.Empty);
                SetSprites(slot, _sprite);
            }
            var report = Analyze();
            Assert.That((IList)Field(report, "Errors"), Is.Empty);
            Assert.That((int[])Field(report, "FirstRuleByMask"), Has.All.GreaterThanOrEqualTo(0));
            object[] args = { _tile, Enum.Parse(TypeOf("RuleTileTemplateKind"), "Floor") };
            Assert.That(Call("RuleTileBuilderTemplates", "TryRecognize", args), Is.True);
            Assert.That(args[1].ToString(), Is.EqualTo(kind));
            var ids = new HashSet<int>();
            foreach (var rule in _tile.m_TilingRules)
            {
                Assert.That(rule.m_RuleTransform, Is.EqualTo(RuleTile.TilingRuleOutput.Transform.Fixed));
                Assert.That(ids.Add(rule.m_Id), Is.True, "Every generated native rule needs its own ID.");
            }
        }

        [Test]
        public void OptionalEmptySlots_DoNotCreateRules_AndExistingSettingsArePreserved()
        {
            var slot = Slots("Orthogonal")[0];
            SetSprites(slot);
            Assert.That(_tile.m_TilingRules, Is.Empty);
            SetSprites(slot, _sprite, _sprite);
            var rule = _tile.m_TilingRules[0];
            Assert.That(rule.m_Output, Is.EqualTo(RuleTile.TilingRuleOutput.OutputSprite.Random));
            rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
            rule.m_ColliderType = Tile.ColliderType.Grid;
            rule.m_MinAnimationSpeed = 3;
            SetSprites(slot, _sprite);
            Assert.That(_tile.m_TilingRules[0], Is.SameAs(rule));
            Assert.That(rule.m_Output, Is.EqualTo(RuleTile.TilingRuleOutput.OutputSprite.Animation));
            Assert.That(rule.m_ColliderType, Is.EqualTo(Tile.ColliderType.Grid));
            Assert.That(rule.m_MinAnimationSpeed, Is.EqualTo(3));
            SetSprites(slot);
            Assert.That(_tile.m_TilingRules, Is.Empty);
        }

        [Test]
        public void Validation_ReportsFallbackGapsShadowingAndIndependentAnimation()
        {
            SetSprites(Slots("Floor")[0], _sprite);
            var animated = _tile.m_TilingRules[0].Clone();
            animated.m_Id = 2;
            animated.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
            _tile.m_TilingRules.Add(animated);
            var report = Analyze();
            Assert.That((int[])Field(report, "FirstRuleByMask"), Has.All.EqualTo(0));
            Assert.That((int[])Field(report, "AnimationRuleByMask"), Has.All.EqualTo(1));
            Assert.That((IList)Field(report, "Warnings"), Is.Not.Empty);
            _tile.m_TilingRules.Clear();
            report = Analyze();
            Assert.That((int[])Field(report, "FirstRuleByMask"), Has.All.EqualTo(-1));
            Assert.That((IList)Field(report, "Warnings"), Is.Not.Empty);
            _tile.m_DefaultSprite = null;
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
        }

        [Test]
        public void Validation_RejectsInvalidOutputsParametersAndNonpersistentSpritesWithoutMutation()
        {
            SetSprites(Slots("Floor")[0], _sprite);
            var rule = _tile.m_TilingRules[0];
            rule.m_Sprites = new Sprite[] { null };
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
            rule.m_Sprites = new[] { _sprite };
            rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
            rule.m_MinAnimationSpeed = float.NaN;
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
            rule.m_MinAnimationSpeed = 1;
            rule.m_MaxAnimationSpeed = 0;
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
            rule.m_MaxAnimationSpeed = 1;
            rule.m_ColliderType = (Tile.ColliderType)999;
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
            rule.m_ColliderType = Tile.ColliderType.None;
            rule.m_RuleTransform = (RuleTile.TilingRuleOutput.Transform)999;
            Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
            rule.m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed;
            var temporary = Sprite.Create(_sprite.texture, _sprite.rect, Vector2.zero);
            try
            {
                _tile.m_DefaultSprite = temporary;
                Assert.That((IList)Field(Analyze(), "Errors"), Is.Not.Empty);
                string before = EditorJsonUtility.ToJson(_tile);
                Analyze();
                Assert.That(EditorJsonUtility.ToJson(_tile), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(temporary); }
        }

        [Test]
        public void Compatibility_RejectsOutOfRangeDuplicateOrMismatchedNeighbors()
        {
            var rule = new RuleTile.TilingRule();
            rule.m_Neighbors.Add(1);
            _tile.m_TilingRules.Add(rule);
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Not.Null);
            rule.m_NeighborPositions = new List<Vector3Int> { new Vector3Int(2, 0, 0) };
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Not.Null);
            rule.m_NeighborPositions[0] = Vector3Int.zero;
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Not.Null);
            rule.m_NeighborPositions[0] = Vector3Int.up;
            rule.m_Neighbors[0] = 3;
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Not.Null);
            rule.m_Neighbors[0] = 0;
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Null);
            rule.m_Neighbors.Add(2);
            rule.m_NeighborPositions.Add(Vector3Int.up);
            Assert.That(Call("RuleTileBuilderValidation", "CheckCompatibility", _tile), Is.Not.Null);
        }

        [TestCase(RuleTile.TilingRuleOutput.Transform.Fixed)]
        [TestCase(RuleTile.TilingRuleOutput.Transform.Rotated)]
        [TestCase(RuleTile.TilingRuleOutput.Transform.MirrorX)]
        [TestCase(RuleTile.TilingRuleOutput.Transform.MirrorY)]
        [TestCase(RuleTile.TilingRuleOutput.Transform.MirrorXY)]
        [TestCase(RuleTile.TilingRuleOutput.Transform.RotatedMirror)]
        public void Coverage_AgreesWithNativeTilemapForAll256Masks(RuleTile.TilingRuleOutput.Transform transform)
        {
            var rule = new RuleTile.TilingRule
            {
                m_NeighborPositions = new List<Vector3Int> { Vector3Int.up, Vector3Int.right, new Vector3Int(-1, -1, 0) },
                m_Neighbors = new List<int> { 1, 2, 1 },
                m_Sprites = new[] { _sprite }, m_RuleTransform = transform
            };
            _tile.m_TilingRules.Add(rule);
            _tile.m_DefaultSprite = null;
            var report = Analyze();
            int[] coverage = (int[])Field(report, "FirstRuleByMask");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("RuleTile native oracle", typeof(Grid));
            SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Tilemap", typeof(Tilemap));
            child.transform.SetParent(root.transform);
            var map = child.GetComponent<Tilemap>();
            Vector3Int[] positions = { new(-1, 1, 0), new(0, 1, 0), new(1, 1, 0), new(-1, 0, 0), new(1, 0, 0), new(-1, -1, 0), new(0, -1, 0), new(1, -1, 0) };
            try
            {
                for (int mask = 0; mask < 256; mask++)
                {
                    map.ClearAllTiles();
                    map.SetTile(Vector3Int.zero, _tile);
                    for (int i = 0; i < 8; i++)
                        if ((mask & (1 << i)) != 0) map.SetTile(positions[i], _tile);
                    _tile.UpdateNeighborPositions();
                    map.RefreshAllTiles();
                    Assert.That(map.GetSprite(Vector3Int.zero) == _sprite, Is.EqualTo(coverage[mask] == 0), "Mask " + mask);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Type TypeOf(string name)
        {
            var type = Type.GetType(Prefix + name + ", Assembly-CSharp-Editor");
            Assert.That(type, Is.Not.Null, "Missing Editor type: " + name);
            return type;
        }

        private static object Call(string type, string method, params object[] args)
        {
            return TypeOf(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }

        private static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);
        private static IList Slots(string kind) => (IList)Call("RuleTileBuilderTemplates", "GetSlots", Enum.Parse(TypeOf("RuleTileTemplateKind"), kind));
        private void SetSprites(object slot, params Sprite[] sprites) => Call("RuleTileBuilderTemplates", "SetSprites", _tile, slot, sprites);
        private object Analyze() => Call("RuleTileBuilderValidation", "Analyze", _tile);
    }
}

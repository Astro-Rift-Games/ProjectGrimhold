using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tests.EditMode.Movement
{
    [TestFixture]
    public sealed class PathfindingGridRefreshRegionTests
    {
        private const int ObstacleLayer = 6;

        private GameObject _gridRoot;
        private GameObject _blocker;
        private GameObject _wall;
        private PathfindingGrid _grid;
        private PathfindingGridConfig _config;
        private Tile _tile;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PathfindingGridConfig>();
            SetField(_config, "_nodeSize", 0.5f);
            SetField(_config, "_agentRadius", 0.1f);
            SetField(_config, "_obstacleLayer", (LayerMask)(1 << ObstacleLayer));

            _gridRoot = new GameObject("PathfindingGridRefreshRegionTests", typeof(Grid));
            var tilemapObject = new GameObject("Floor", typeof(Tilemap));
            tilemapObject.transform.SetParent(_gridRoot.transform, false);
            _tile = ScriptableObject.CreateInstance<Tile>();
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), _tile);
                }
            }

            _blocker = CreateBox("DoorBlocker", new Vector2(3f, 5f));
            _wall = CreateBox("Wall", new Vector2(7f, 5f));

            _grid = _gridRoot.AddComponent<PathfindingGrid>();
            SetField(_grid, "_config", _config);
            Physics2D.SyncTransforms();
            _grid.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_blocker);
            Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_gridRoot);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_tile);
        }

        [Test]
        public void RefreshRegion_AfterBlockerDisabled_MakesItsNodesWalkable()
        {
            Assert.That(IsWalkableAt(new Vector2(3f, 5f)), Is.False);
            Bounds bounds = _blocker.GetComponent<Collider2D>().bounds;

            _blocker.GetComponent<Collider2D>().enabled = false;
            _grid.RefreshRegion(bounds);

            Assert.That(IsWalkableAt(new Vector2(3f, 5f)), Is.True);
        }

        [Test]
        public void RefreshRegion_AfterBlockerReenabled_BlocksItsNodesAgain()
        {
            Collider2D blocker = _blocker.GetComponent<Collider2D>();
            blocker.enabled = false;
            _grid.RefreshRegion(blocker.bounds);

            blocker.enabled = true;
            Physics2D.SyncTransforms();
            _grid.RefreshRegion(blocker.bounds);

            Assert.That(IsWalkableAt(new Vector2(3f, 5f)), Is.False);
        }

        [Test]
        public void RefreshRegion_LeavesNodesOutsideTheRegionUntouched()
        {
            Collider2D blocker = _blocker.GetComponent<Collider2D>();
            Bounds bounds = blocker.bounds;
            _wall.GetComponent<Collider2D>().enabled = false;
            blocker.enabled = false;

            _grid.RefreshRegion(bounds);

            Assert.That(IsWalkableAt(new Vector2(7f, 5f)), Is.False, "Only the refreshed region may change.");
        }

        [Test]
        public void RefreshRegion_BeforeBuild_IsIgnored()
        {
            var unbuiltObject = new GameObject("UnbuiltGrid");
            try
            {
                PathfindingGrid unbuilt = unbuiltObject.AddComponent<PathfindingGrid>();
                SetField(unbuilt, "_config", _config);

                Assert.DoesNotThrow(() => unbuilt.RefreshRegion(new Bounds(Vector3.zero, Vector3.one)));
                Assert.That(unbuilt.IsBuilt, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(unbuiltObject);
            }
        }

        private bool IsWalkableAt(Vector2 worldPosition)
        {
            Assert.That(_grid.TryGetNodeFromWorldPoint(worldPosition, out int x, out int y), Is.True);
            return _grid.IsWalkable(x, y);
        }

        private static GameObject CreateBox(string name, Vector2 position)
        {
            var box = new GameObject(name, typeof(BoxCollider2D)) { layer = ObstacleLayer };
            box.transform.position = position;
            box.GetComponent<BoxCollider2D>().size = new Vector2(1f, 0.5f);
            return box;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}

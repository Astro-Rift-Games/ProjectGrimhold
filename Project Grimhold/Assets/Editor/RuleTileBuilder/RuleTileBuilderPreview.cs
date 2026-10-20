#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    /// <summary>A disposable native Tilemap sandbox, independent of the user's open scenes.</summary>
    public sealed class RuleTileBuilderPreview : IDisposable
    {
        public const int GridSize = 12;
        private const int MinimumCell = -GridSize / 2;
        private static readonly Vector3Int[] NeighborOffsets =
        {
            new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0)
        };

        private PreviewRenderUtility _utility;
        private RuleTile _draft;
        private Tile _foreignTile;
        private Sprite _foreignSprite;
        private Texture2D _foreignTexture;
        private bool _disposed;

        public Tilemap Tilemap { get; private set; }

        public RuleTileBuilderPreview(RuleTile draft)
        {
            if (!draft) throw new ArgumentNullException(nameof(draft));
            try
            {
                _utility = new PreviewRenderUtility(true);
                var camera = _utility.camera;
                camera.orthographic = true;
                camera.orthographicSize = GridSize * .5f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .12f, .14f, 1);
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 20;

                // Move hidden objects into the utility's preview scene before adding components.
                // This follows RuleTileEditor's sandbox path without changing the active scene.
                var gridObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Rule Tile Builder Preview Grid", HideFlags.HideAndDontSave);
                _utility.AddSingleGO(gridObject);
                gridObject.AddComponent<Grid>();
                var tilemapObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Rule Tile Builder Preview Tilemap", HideFlags.HideAndDontSave);
                _utility.AddSingleGO(tilemapObject);
                tilemapObject.transform.SetParent(gridObject.transform, false);
                Tilemap = tilemapObject.AddComponent<Tilemap>();
                Tilemap.animationFrameRate = 1;
                tilemapObject.AddComponent<TilemapRenderer>();

                CreateForeignTile();
                _draft = draft;
                Refresh();
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
                EditorApplication.quitting += Dispose;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetDraft(RuleTile draft)
        {
            ThrowIfDisposed();
            if (!draft) throw new ArgumentNullException(nameof(draft));
            if (ReferenceEquals(_draft, draft))
            {
                Refresh();
                return;
            }

            var previous = _draft;
            _draft = draft;
            foreach (var position in Tilemap.cellBounds.allPositionsWithin)
                if (Tilemap.GetTile(position) == previous)
                    Tilemap.SetTile(position, _draft);
            Refresh();
        }

        public void SetCell(Vector3Int position, int brush)
        {
            ThrowIfDisposed();
            if (brush < 0 || brush > 2) throw new ArgumentOutOfRangeException(nameof(brush));
            if (!ContainsCell(position)) return;
            Tilemap.SetTile(position, brush == 0 ? null : brush == 1 ? _draft : _foreignTile);
            Refresh();
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            // Also invalidates RuleTile's native tilemap-neighbor cache after matrix edits.
            _draft.UpdateNeighborPositions();
            Tilemap.RefreshAllTiles();
        }

        public int GetMask(Vector3Int position)
        {
            ThrowIfDisposed();
            var mask = 0;
            for (var i = 0; i < NeighborOffsets.Length; i++)
                if (Tilemap.GetTile(position + NeighborOffsets[i]) == _draft)
                    mask |= 1 << i;
            return mask;
        }

        public void LoadPattern(string pattern)
        {
            ThrowIfDisposed();
            if (pattern != "Rectangle" && pattern != "L" && pattern != "Hole" &&
                pattern != "Corridor" && pattern != "Isolated" && pattern != "Clear")
                throw new ArgumentException("Unknown preview pattern.", nameof(pattern));

            Tilemap.ClearAllTiles();
            for (var y = MinimumCell; y < MinimumCell + GridSize; y++)
            for (var x = MinimumCell; x < MinimumCell + GridSize; x++)
            {
                var rectangle = x >= -4 && x < 4 && y >= -3 && y < 3;
                var occupied = pattern == "Rectangle" && rectangle ||
                    pattern == "L" && x >= -3 && x < 4 && y >= -2 && y < 3 && (x < 0 || y < 1) ||
                    pattern == "Hole" && rectangle && !(x >= -1 && x < 1 && y >= -1 && y < 1) ||
                    pattern == "Corridor" && x >= -4 && x < 4 && y == 0 ||
                    pattern == "Isolated" && x == 0 && y == 0;
                if (occupied) Tilemap.SetTile(new Vector3Int(x, y, 0), _draft);
            }
            Refresh();
        }

        /// <summary>Render during IMGUI Repaint; the caller owns drawing, not the returned texture.</summary>
        public Texture Render(Rect rect)
        {
            ThrowIfDisposed();
            if (rect.width <= 0 || rect.height <= 0) return null;
            _utility.camera.orthographicSize = GridSize * .5f * Mathf.Max(1, rect.height / rect.width);
            _utility.BeginPreview(rect, GUIStyle.none);
            try
            {
                // Use the same native render path as the installed RuleTileEditor.
                _utility.camera.Render();
            }
            catch
            {
                _utility.EndPreview();
                throw;
            }
            return _utility.EndPreview();
        }

        public bool TryGetCell(Rect rect, Vector2 mouse, out Vector3Int cell)
        {
            cell = Vector3Int.zero;
            if (_disposed || rect.width <= 0 || rect.height <= 0) return false;
            var side = Mathf.Min(rect.width, rect.height);
            var square = new Rect(rect.center.x - side * .5f, rect.center.y - side * .5f, side, side);
            if (!square.Contains(mouse)) return false;
            cell = new Vector3Int(
                MinimumCell + Mathf.FloorToInt((mouse.x - square.x) / side * GridSize),
                MinimumCell + GridSize - 1 - Mathf.FloorToInt((mouse.y - square.y) / side * GridSize), 0);
            return ContainsCell(cell);
        }

        public void AdvanceAnimation(double editorTime)
        {
            ThrowIfDisposed();
            if (double.IsNaN(editorTime) || double.IsInfinity(editorTime)) return;
            foreach (var position in Tilemap.cellBounds.allPositionsWithin)
            {
                if (Tilemap.GetTile(position) != _draft || Tilemap.GetAnimationFrameCount(position) <= 0) continue;
                // The engine owns the animation-only rule search, selected sprites and speed.
                // Explicit time advances native animations even when EditMode has no player loop.
                Tilemap.SetAnimationTime(position, (float)Math.Max(0, editorTime));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
            _utility?.Cleanup();
            _utility = null;
            Tilemap = null;
            if (_foreignTile) Object.DestroyImmediate(_foreignTile);
            if (_foreignSprite) Object.DestroyImmediate(_foreignSprite);
            if (_foreignTexture) Object.DestroyImmediate(_foreignTexture);
            _foreignTile = null;
            _foreignSprite = null;
            _foreignTexture = null;
            // Drop the destroyed preview Tilemap from RuleTile's shared transient cache.
            if (_draft) _draft.UpdateNeighborPositions();
            _draft = null;
        }

        private void CreateForeignTile()
        {
            _foreignTexture = new Texture2D(2, 2)
            {
                name = "Rule Tile Builder Other Surface", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            var dark = new Color(.55f, .23f, .04f);
            var light = new Color(1f, .68f, .18f);
            _foreignTexture.SetPixels(new[] { dark, light, light, dark });
            _foreignTexture.Apply();
            _foreignSprite = Sprite.Create(_foreignTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            _foreignSprite.hideFlags = HideFlags.HideAndDontSave;
            _foreignTile = ScriptableObject.CreateInstance<Tile>();
            _foreignTile.name = "Other surface (preview only)";
            _foreignTile.hideFlags = HideFlags.HideAndDontSave;
            _foreignTile.sprite = _foreignSprite;
            _foreignTile.colliderType = Tile.ColliderType.None;
        }

        private static bool ContainsCell(Vector3Int cell) => cell.z == 0 &&
            cell.x >= MinimumCell && cell.x < MinimumCell + GridSize &&
            cell.y >= MinimumCell && cell.y < MinimumCell + GridSize;

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RuleTileBuilderPreview));
        }
    }
}
#endif

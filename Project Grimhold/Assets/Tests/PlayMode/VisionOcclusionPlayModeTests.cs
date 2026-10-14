#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Visibility
{
    /// <summary>
    /// Proves live occlusion of the local vision: obstacle colliders block the line of sight,
    /// a target's own collider never hides it, and colliders added at runtime block immediately.
    /// </summary>
    public sealed class VisionOcclusionPlayModeTests
    {
        private const string ObstacleLayerName = "WorldCollision";

        private readonly List<GameObject> _created = new List<GameObject>();
        private EntityVisibilitySystem _system;
        private FixedViewer _viewer;
        private int _obstacleLayer;

        [SetUp]
        public void SetUp()
        {
            _obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
            Assert.That(_obstacleLayer, Is.GreaterThanOrEqualTo(0), "WorldCollision layer must exist.");

            _system = Create("VisibilitySystem").AddComponent<EntityVisibilitySystem>();
            _viewer = new FixedViewer(Vector2.zero, Vector2.right, 1 << _obstacleLayer);
            _system.RegisterLocalViewer(_viewer);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.Destroy(_created[i]);
                }
            }

            _created.Clear();
        }

        [UnityTest]
        public IEnumerator TargetInCone_WithClearLine_IsShown()
        {
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));

            yield return EvaluateFrame();

            Assert.That(target.forceRenderingOff, Is.False);
        }

        [UnityTest]
        public IEnumerator ObstacleBetweenViewerAndTarget_HidesTarget()
        {
            CreateWall(new Vector2(2.5f, 0f));
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));

            yield return EvaluateFrame();

            Assert.That(target.forceRenderingOff, Is.True);
        }

        [UnityTest]
        public IEnumerator TargetOwnObstacleCollider_DoesNotHideIt()
        {
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));
            GameObject blocker = new GameObject("OwnBlocker") { layer = _obstacleLayer };
            blocker.transform.SetParent(target.transform, false);
            blocker.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);

            yield return EvaluateFrame();

            Assert.That(target.forceRenderingOff, Is.False);
        }

        [UnityTest]
        public IEnumerator ColliderAddedAtRuntime_BlocksWithoutRefresh()
        {
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));
            yield return EvaluateFrame();
            Assert.That(target.forceRenderingOff, Is.False);

            CreateWall(new Vector2(2.5f, 0f));
            yield return EvaluateFrame();

            Assert.That(target.forceRenderingOff, Is.True, "A door closing at runtime must block vision.");
        }

        [UnityTest]
        public IEnumerator TargetBehind_InsideNearRadius_IsShown_OutsideIsHidden()
        {
            SpriteRenderer near = CreateTarget(new Vector2(-1f, 0f));
            SpriteRenderer far = CreateTarget(new Vector2(-5f, 0f));

            yield return EvaluateFrame();

            Assert.That(near.forceRenderingOff, Is.False);
            Assert.That(far.forceRenderingOff, Is.True);
        }

        [UnityTest]
        public IEnumerator WithoutViewer_EveryTargetIsHidden()
        {
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));
            yield return EvaluateFrame();
            Assert.That(target.forceRenderingOff, Is.False);

            _system.UnregisterLocalViewer(_viewer);
            yield return EvaluateFrame();

            Assert.That(target.forceRenderingOff, Is.True);
        }

        [UnityTest]
        public IEnumerator TargetUnderViewerRoot_IsAlwaysShown()
        {
            GameObject viewerRoot = Create("ViewerRoot");
            _viewer.ViewerRoot = viewerRoot.transform;
            SpriteRenderer own = CreateTarget(new Vector2(-8f, 0f));
            own.transform.SetParent(viewerRoot.transform, true);

            yield return EvaluateFrame();

            Assert.That(own.forceRenderingOff, Is.False);
        }

        [UnityTest]
        public IEnumerator SpectatedViewer_TakesPrecedence_AndClearingFallsBackToLocalViewer()
        {
            SpriteRenderer right = CreateTarget(new Vector2(5f, 0f));
            SpriteRenderer left = CreateTarget(new Vector2(-5f, 0f));
            var spectated = new FixedViewer(Vector2.zero, Vector2.left, 1 << _obstacleLayer);

            _system.SetSpectatedViewer(spectated);
            yield return EvaluateFrame();
            Assert.That(right.forceRenderingOff, Is.True, "Spectating replaces the local facing.");
            Assert.That(left.forceRenderingOff, Is.False);

            _system.ClearSpectatedViewer(spectated);
            yield return EvaluateFrame();
            Assert.That(right.forceRenderingOff, Is.False);
            Assert.That(left.forceRenderingOff, Is.True);
        }

        [UnityTest]
        public IEnumerator TargetFarAway_InsideCone_IsShown()
        {
            SpriteRenderer far = CreateTarget(new Vector2(60f, 0f));

            yield return EvaluateFrame();

            Assert.That(far.forceRenderingOff, Is.False, "The cone has no range limit.");
        }

        [UnityTest]
        public IEnumerator OcclusionOnlyTarget_BehindViewer_IsShown_ButHiddenByWall()
        {
            SpriteRenderer behind = CreateTarget(new Vector2(-6f, 0f), occlusionOnly: true);
            SpriteRenderer walled = CreateTarget(new Vector2(0f, 6f), occlusionOnly: true);
            CreateWall(new Vector2(0f, 3f), new Vector2(4f, 0.5f));

            yield return EvaluateFrame();

            Assert.That(behind.forceRenderingOff, Is.False, "Static objects ignore facing.");
            Assert.That(walled.forceRenderingOff, Is.True, "Obstacles still hide static objects.");
        }

        [UnityTest]
        public IEnumerator LosingSight_KeepsTargetDuringGrace_ThenFadesItOut()
        {
            _viewer.FadeTimings = new VisibilityFadeTimings(graceSeconds: 0.3f, fadeInSeconds: 0f, fadeOutSeconds: 0.3f);
            SpriteRenderer target = CreateTarget(new Vector2(5f, 0f));
            target.color = new Color(1f, 0.5f, 0.5f, 1f);
            yield return EvaluateFrame();
            Assert.That(target.forceRenderingOff, Is.False);

            CreateWall(new Vector2(2.5f, 0f));
            yield return EvaluateFrame();
            Assert.That(target.forceRenderingOff, Is.False, "Grace keeps the target visible.");
            Assert.That(target.color.a, Is.EqualTo(1f));

            yield return WaitSeconds(0.45f);
            Assert.That(target.forceRenderingOff, Is.False);
            Assert.That(target.color.a, Is.GreaterThan(0f).And.LessThan(1f), "The target fades out after grace.");
            Assert.That(target.color.r, Is.EqualTo(1f).Within(0.001f), "Fading keeps the authored tint.");

            yield return WaitSeconds(0.4f);
            Assert.That(target.forceRenderingOff, Is.True);
            Assert.That(target.color, Is.EqualTo(new Color(1f, 0.5f, 0.5f, 1f)), "Hidden targets restore their color.");
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private IEnumerator EvaluateFrame()
        {
            Physics2D.SyncTransforms();
            yield return null;
            yield return null;
        }

        private SpriteRenderer CreateTarget(Vector2 position, bool occlusionOnly = false)
        {
            GameObject target = Create("Target");
            target.transform.position = position;
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            target.SetActive(false);
            EntityVisibilityPresenter presenter = target.AddComponent<EntityVisibilityPresenter>();
            typeof(EntityVisibilityPresenter)
                .GetField("_renderers", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(presenter, new Renderer[] { renderer });
            typeof(EntityVisibilityPresenter)
                .GetField("_occlusionOnly", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(presenter, occlusionOnly);
            target.SetActive(true);
            return renderer;
        }

        private void CreateWall(Vector2 position)
        {
            CreateWall(position, new Vector2(0.5f, 4f));
        }

        private void CreateWall(Vector2 position, Vector2 size)
        {
            GameObject wall = Create("Wall");
            wall.layer = _obstacleLayer;
            wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = size;
        }

        private GameObject Create(string name)
        {
            GameObject gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject;
        }

        private sealed class FixedViewer : IVisionViewer
        {
            private readonly Vector2 _origin;
            private readonly Vector2 _facing;
            private readonly LayerMask _obstacles;
            private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

            public FixedViewer(Vector2 origin, Vector2 facing, LayerMask obstacles)
            {
                _origin = origin;
                _facing = facing;
                _obstacles = obstacles;
            }

            public Transform ViewerRoot { get; set; }

            public VisibilityFadeTimings FadeTimings { get; set; } = VisibilityFadeTimings.Instant;

            public bool TryBuildQuery(out VisionQuery query)
            {
                query = new VisionQuery(
                    _origin,
                    _facing,
                    2f,
                    VisionConeRules.CosineOfHalfAngle(90f),
                    _obstacles,
                    _hits,
                    ViewerRoot,
                    FadeTimings);
                return true;
            }
        }
    }
}
#endif

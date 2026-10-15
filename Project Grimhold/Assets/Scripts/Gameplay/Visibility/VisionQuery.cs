using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// One frame of the local viewer's vision: shape parameters, live obstacle occlusion and the
    /// fade timings that soften visibility changes.
    /// Built by the local <see cref="IVisionViewer"/> and consumed by visibility targets. Presentation only.
    /// </summary>
    public readonly struct VisionQuery
    {
        private readonly Vector2 _origin;
        private readonly Vector2 _facing;
        private readonly float _nearRadius;
        private readonly float _coneHalfAngleCosine;
        private readonly ContactFilter2D _obstacleFilter;
        private readonly RaycastHit2D[] _hitBuffer;
        private readonly Transform _viewerRoot;
        private readonly VisibilityFadeTimings _fadeTimings;

        public VisionQuery(
            Vector2 origin,
            Vector2 facing,
            float nearRadius,
            float coneHalfAngleCosine,
            LayerMask obstacleLayer,
            RaycastHit2D[] hitBuffer,
            Transform viewerRoot,
            VisibilityFadeTimings fadeTimings)
        {
            _origin = origin;
            _facing = facing;
            _nearRadius = nearRadius;
            _coneHalfAngleCosine = coneHalfAngleCosine;
            _hitBuffer = hitBuffer;
            _viewerRoot = viewerRoot;
            _fadeTimings = fadeTimings;
            _obstacleFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = obstacleLayer,
                useTriggers = false
            };
        }

        public Vector2 Origin => _origin;

        public Vector2 Facing => _facing;

        public float NearRadius => _nearRadius;

        public float ConeHalfAngleCosine => _coneHalfAngleCosine;

        public Transform ViewerRoot => _viewerRoot;

        public VisibilityFadeTimings FadeTimings => _fadeTimings;

        /// <summary>
        /// Returns whether no obstacle collider outside <paramref name="targetRoot"/> or the viewer
        /// blocks the line to <paramref name="point"/>. When <paramref name="requiresCone"/> is true the
        /// point must also lie inside the near radius or the facing cone; static objects pass false so
        /// only obstacles hide them.
        /// </summary>
        public bool IsPointVisible(Vector2 point, Transform targetRoot, bool requiresCone)
        {
            if (_hitBuffer == null ||
                (requiresCone &&
                 !VisionConeRules.IsWithinVisionShape(_origin, _facing, point, _nearRadius, _coneHalfAngleCosine)))
            {
                return false;
            }

            int hitCount = Physics2D.Linecast(_origin, point, _obstacleFilter, _hitBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _hitBuffer[i].collider;
                if (hitCollider == null || BelongsTo(hitCollider.transform, targetRoot) ||
                    BelongsTo(hitCollider.transform, _viewerRoot))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool BelongsTo(Transform candidate, Transform root)
        {
            return root != null && candidate.IsChildOf(root);
        }
    }
}

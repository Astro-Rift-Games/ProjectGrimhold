using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Scene-level local presentation system. Each frame it asks the spectated viewer, or else the
    /// single registered local viewer, for a vision query and fades every registered target in or
    /// out according to whether that viewer sees it, using the viewer's grace and fade timings.
    /// Without a viewer every target is hidden immediately (fail-closed). It never touches
    /// gameplay, colliders or networked state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityVisibilitySystem : MonoBehaviour
    {
        private IVisionViewer _localViewer;
        private IVisionViewer _spectatedViewer;

        private readonly List<IVisibilityTarget> _targets = new List<IVisibilityTarget>();
        private readonly Dictionary<IVisibilityTarget, VisibilityFadeState> _fadeStates =
            new Dictionary<IVisibilityTarget, VisibilityFadeState>();

        /// <summary>
        /// Registers an entity fully hidden; it fades in once the viewer sees it.
        /// </summary>
        public void Register(IVisibilityTarget target)
        {
            if (target == null || _targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
            target.ApplyVisibility(0f);
            _fadeStates[target] = VisibilityFadeState.Hidden;
        }

        /// <summary>
        /// Removes an entity from evaluation. Its current visual state is left unchanged.
        /// </summary>
        public void Unregister(IVisibilityTarget target)
        {
            if (target != null)
            {
                _targets.Remove(target);
                _fadeStates.Remove(target);
            }
        }

        /// <summary>
        /// Registers the single vision producer belonging to this peer's current
        /// local raid avatar. Proxy avatars must never register a viewer.
        /// </summary>
        public void RegisterLocalViewer(IVisionViewer viewer)
        {
            if (viewer != null)
            {
                _localViewer = viewer;
            }
        }

        /// <summary>
        /// Clears the viewer only when the caller still owns it, so an old
        /// avatar cannot clear a replacement avatar's local vision.
        /// </summary>
        public void UnregisterLocalViewer(IVisionViewer viewer)
        {
            if (ReferenceEquals(_localViewer, viewer))
            {
                _localViewer = null;
            }
        }

        /// <summary>
        /// Shows the world through a spectated avatar's vision. While set, it takes
        /// precedence over the local viewer.
        /// </summary>
        public void SetSpectatedViewer(IVisionViewer viewer)
        {
            _spectatedViewer = viewer;
        }

        /// <summary>
        /// Clears the spectated viewer only when the caller still owns it.
        /// </summary>
        public void ClearSpectatedViewer(IVisionViewer viewer)
        {
            if (ReferenceEquals(_spectatedViewer, viewer))
            {
                _spectatedViewer = null;
            }
        }

        private void LateUpdate()
        {
            bool hasQuery = TryBuildQuery(out VisionQuery query);
            VisibilityFadeTimings timings = hasQuery ? query.FadeTimings : VisibilityFadeTimings.Instant;
            float deltaTime = Time.deltaTime;

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                IVisibilityTarget target = _targets[i];
                if (target == null || (target is Object unityObject && unityObject == null))
                {
                    _fadeStates.Remove(target);
                    _targets.RemoveAt(i);
                    continue;
                }

                bool seen = hasQuery && IsVisibleToViewer(target, in query);
                if (!_fadeStates.TryGetValue(target, out VisibilityFadeState previous))
                {
                    previous = VisibilityFadeState.Hidden;
                }

                VisibilityFadeState next = VisibilityFadeRules.Step(previous, seen, deltaTime, timings);
                _fadeStates[target] = next;
                if (previous.Alpha != next.Alpha)
                {
                    target.ApplyVisibility(next.Alpha);
                }
            }
        }

        private bool TryBuildQuery(out VisionQuery query)
        {
            query = default;
            if (IsAlive(ref _spectatedViewer))
            {
                return _spectatedViewer.TryBuildQuery(out query);
            }

            return IsAlive(ref _localViewer) && _localViewer.TryBuildQuery(out query);
        }

        private static bool IsAlive(ref IVisionViewer viewer)
        {
            if (viewer == null || (viewer is Object viewerObject && viewerObject == null))
            {
                viewer = null;
                return false;
            }

            return true;
        }

        private static bool IsVisibleToViewer(IVisibilityTarget target, in VisionQuery query)
        {
            Transform targetRoot = target.VisibilityRoot;
            if (query.ViewerRoot != null && targetRoot != null && targetRoot.IsChildOf(query.ViewerRoot))
            {
                return true;
            }

            return target.IsVisible(in query);
        }
    }
}

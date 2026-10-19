using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Companion component that makes any entity respect local vision without coupling
    /// gameplay logic. It owns its registration with the scene visibility system and
    /// fades its renderers with the opacity the system computes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityVisibilityPresenter : MonoBehaviour, IVisibilityTarget
    {
        [Tooltip("Renderers hidden or shown when this entity's visibility changes. Sprites fade through their color alpha; other renderers switch with forceRenderingOff.")]
        [SerializeField] private Renderer[] _renderers;

        [Tooltip("Static objects (chests, loot, sanctuaries, traps) are hidden only by obstacles, never by the viewer's facing.")]
        [SerializeField] private bool _occlusionOnly;

        [Tooltip("Offset from transform.position used to evaluate visibility. Useful for tall entities whose pivot is at the base.")]
        [SerializeField] private Vector2 _visibilityOffset;

        [Tooltip("Collider whose footprint is sampled for large entities such as Sanctuaries.")]
        [SerializeField] private Collider2D _visibilityCollider;

        [Tooltip("Show this entity when any sampled point inside the collider is seen by the local viewer.")]
        [SerializeField] private bool _useColliderBounds;

        private EntityVisibilitySystem _system;
        private SpriteRenderer[] _spriteRenderers;
        private Color[] _baseColors;
        private Color[] _writtenColors;
        private bool[] _hasWrittenColor;
        private float _appliedAlpha = 1f;

        public Vector2 VisibilityPoint => (Vector2)transform.position + _visibilityOffset;

        public Transform VisibilityRoot => transform;

        /// <inheritdoc />
        public bool IsVisible(in VisionQuery query)
        {
            bool requiresCone = !_occlusionOnly;
            if (!_useColliderBounds || _visibilityCollider == null)
            {
                return query.IsPointVisible(VisibilityPoint, transform, requiresCone);
            }

            Bounds bounds = _visibilityCollider.bounds;
            Vector2 center = bounds.center;
            if (query.IsPointVisible(center, transform, requiresCone))
            {
                return true;
            }

            // Sample inset corners so a meaningful portion of an authored
            // footprint can be seen without testing outside its collider.
            Vector2 extents = bounds.extents * 0.9f;
            return query.IsPointVisible(center + new Vector2(-extents.x, -extents.y), transform, requiresCone) ||
                   query.IsPointVisible(center + new Vector2(-extents.x, extents.y), transform, requiresCone) ||
                   query.IsPointVisible(center + new Vector2(extents.x, -extents.y), transform, requiresCone) ||
                   query.IsPointVisible(center + new Vector2(extents.x, extents.y), transform, requiresCone);
        }

        /// <inheritdoc />
        public void ApplyVisibility(float alpha)
        {
            if (_renderers == null)
            {
                return;
            }

            CacheSpriteRenderers();
            _appliedAlpha = Mathf.Clamp01(alpha);
            bool isRendered = _appliedAlpha > 0f;
            bool isPartial = isRendered && _appliedAlpha < 1f;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }

                _renderers[i].forceRenderingOff = !isRendered;
                ApplySpriteAlpha(i, isPartial);
            }
        }

        /// <summary>
        /// Multiplies the sprite's own alpha while partially visible. Colors written by other
        /// presenters (damage flash, sanctuary states, armor tints) are adopted as the new base,
        /// and the base color is restored exactly once fully visible or hidden.
        /// </summary>
        private void ApplySpriteAlpha(int index, bool isPartial)
        {
            SpriteRenderer sprite = _spriteRenderers[index];
            if (sprite == null)
            {
                return;
            }

            Color current = sprite.color;
            if (!_hasWrittenColor[index] || current != _writtenColors[index])
            {
                _baseColors[index] = current;
            }

            if (!isPartial)
            {
                if (_hasWrittenColor[index])
                {
                    sprite.color = _baseColors[index];
                    _hasWrittenColor[index] = false;
                }

                return;
            }

            Color faded = _baseColors[index];
            faded.a *= _appliedAlpha;
            sprite.color = faded;
            _writtenColors[index] = faded;
            _hasWrittenColor[index] = true;
        }

        private void CacheSpriteRenderers()
        {
            if (_spriteRenderers != null && _spriteRenderers.Length == _renderers.Length)
            {
                return;
            }

            int count = _renderers.Length;
            _spriteRenderers = new SpriteRenderer[count];
            _baseColors = new Color[count];
            _writtenColors = new Color[count];
            _hasWrittenColor = new bool[count];
            for (int i = 0; i < count; i++)
            {
                _spriteRenderers[i] = _renderers[i] as SpriteRenderer;
            }
        }

        private void OnEnable()
        {
            if (_system == null)
            {
                // Resolve the scene system once; it must be active to evaluate.
                _system = FindAnyObjectByType<EntityVisibilitySystem>(FindObjectsInactive.Exclude);
            }

            if (_system != null)
            {
                _system.Register(this);
            }
        }

        private void LateUpdate()
        {
            // Networked scene objects can enable before the scene-level visibility
            // system is created. Retry registration until that dependency exists.
            if (_system == null)
            {
                _system = FindAnyObjectByType<EntityVisibilitySystem>(FindObjectsInactive.Exclude);
                if (_system != null)
                {
                    _system.Register(this);
                }
            }
        }

        private void OnDisable()
        {
            if (_system != null)
            {
                _system.Unregister(this);
            }
        }

#if UNITY_EDITOR
        private const float StateGizmoRadius = 0.25f;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _system == null)
            {
                return;
            }

            // Green: fully visible (including grace). Yellow: fading. Red: hidden.
            Gizmos.color = _appliedAlpha >= 1f ? Color.green : _appliedAlpha > 0f ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(VisibilityPoint, StateGizmoRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(VisibilityPoint, 0.15f);
        }
#endif
    }
}

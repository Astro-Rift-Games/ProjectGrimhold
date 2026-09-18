using UnityEngine;

namespace Grimhold.Combat.Presentation
{
    [CreateAssetMenu(fileName = "CombatVfxDefinition", menuName = "Grimhold/Combat/Combat VFX Definition")]
    public sealed class CombatVfxDefinition : ScriptableObject
    {
        [SerializeField] private CombatVfxInstance _prefab;
        [SerializeField] private CombatVfxAnchor _anchorMode = CombatVfxAnchor.AttackOrigin;
        [SerializeField] private Vector2 _localOffset;
        [SerializeField] private float _rotationOffset;
        [SerializeField] private Color _defaultTint = Color.white;
        [SerializeField] private int _sortingOrder = 20;
        [SerializeField, Min(1)] private int _poolCapacity = 5;

        public CombatVfxInstance Prefab => _prefab;
        public CombatVfxAnchor AnchorMode => _anchorMode;
        public Vector2 LocalOffset => _localOffset;
        public float RotationOffset => _rotationOffset;
        public Color DefaultTint => _defaultTint;
        public int SortingOrder => _sortingOrder;
        public int PoolCapacity => _poolCapacity;
        
        public bool TryValidate(out string error)
        {
            if (_prefab == null)
            {
                error = "VFX definition requires a prefab.";
                return false;
            }
            if (_poolCapacity < 1)
            {
                error = "Pool capacity must be at least 1.";
                return false;
            }
            error = null;
            return true;
        }
    }
}

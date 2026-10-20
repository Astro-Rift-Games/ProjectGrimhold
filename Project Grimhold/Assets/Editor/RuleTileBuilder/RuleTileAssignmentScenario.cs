using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    /// <summary>A read-only illustrative footprint; coordinates use Unity's upward-positive Y.</summary>
    public sealed class RuleTileAssignmentScenario
    {
        private readonly HashSet<Vector3Int> _occupied;
        public string Name { get; }
        public string Description { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<Vector3Int> Cells { get; }
        public IReadOnlyList<Vector3Int> FeaturedCells { get; }

        internal RuleTileAssignmentScenario(string name, string description, int width, int height,
            IEnumerable<Vector3Int> cells, IEnumerable<Vector3Int> featured = null)
        {
            Name = name;
            Description = description;
            Width = width;
            Height = height;
            _occupied = new HashSet<Vector3Int>(cells);
            Cells = _occupied.OrderByDescending(c => c.y).ThenBy(c => c.x).ToList().AsReadOnly();
            FeaturedCells = (featured ?? new Vector3Int[0]).ToList().AsReadOnly();
        }

        public RuleTileTemplateSlot GetSlot(RuleTileTemplateKind kind, Vector3Int cell)
        {
            if (!_occupied.Contains(cell)) return null;
            int mask = 0;
            for (int bit = 0; bit < 8; bit++)
                if (_occupied.Contains(cell + RuleTileBuilderTemplates.GetPosition(bit))) mask |= 1 << bit;
            foreach (var slot in RuleTileBuilderTemplates.GetSlots(kind))
                if ((mask & slot.ConditionMask) == slot.Mask) return slot;
            return null;
        }
    }
}

using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Contract for any entity that participates in local visibility.
    /// Defines how the system queries it and toggles its visual representation.
    /// </summary>
    public interface IVisibilityTarget
    {
        /// <summary>
        /// World point (e.g. center or base of the character) evaluated against the vision query.
        /// </summary>
        Vector2 VisibilityPoint { get; }

        /// <summary>
        /// Root transform owning this target. Colliders under it never occlude the target itself,
        /// and the local viewer's own avatar is always shown.
        /// </summary>
        Transform VisibilityRoot { get; }

        /// <summary>
        /// Determines whether a visible part of this target is seen by the local viewer.
        /// </summary>
        bool IsVisible(in VisionQuery query);

        /// <summary>
        /// Applies the entity's presentation opacity in [0, 1]; zero means not rendered at all.
        /// Implementations must only affect presentation (never gameplay, colliders or network state).
        /// </summary>
        void ApplyVisibility(float alpha);
    }
}

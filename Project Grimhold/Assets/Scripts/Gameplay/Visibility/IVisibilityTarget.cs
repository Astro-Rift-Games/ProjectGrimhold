using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Contrato para cualquier entidad que participe en el sistema de visibilidad.
    /// Define cómo el sistema puede consultarla y manipular su representación visual.
    /// </summary>
    public interface IVisibilityTarget
    {
        /// <summary>
        /// El punto en el mundo (ej. centro o base del personaje) que se usará para 
        /// evaluar la pertenencia al polígono de visión.
        /// </summary>
        Vector2 VisibilityPoint { get; }

        /// <summary>
        /// Determines whether a visible part of this target belongs to the current
        /// local line-of-sight polygon.
        /// </summary>
        bool IsVisible(LosPolygonHandle losHandle);

        /// <summary>
        /// Define la visibilidad visual de la entidad. 
        /// Las implementaciones sólo deben afectar renderers (no gameplay, colliders o network).
        /// </summary>
        void SetVisible(bool isVisible);
    }
}

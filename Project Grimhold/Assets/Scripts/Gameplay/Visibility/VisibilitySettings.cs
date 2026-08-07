using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Configuraciones compartidas para el sistema de visibilidad.
    /// </summary>
    [CreateAssetMenu(menuName = "Grimhold/Visibility/Settings", fileName = "VisibilitySettings")]
    public sealed class VisibilitySettings : ScriptableObject
    {
        [Tooltip("Radio máximo de visión en unidades métricas.")]
        public float ViewRadius = 10f;
        
        [Tooltip("Layers que bloquean la visión (paredes, obstáculos).")]
        public LayerMask ObstacleLayer;
        
        [Tooltip("Cantidad de rayos que se lanzan hacia el perímetro máximo para redondear el límite visual.")]
        [Range(8, 64)]
        public int BorderRays = 32;
    }
}

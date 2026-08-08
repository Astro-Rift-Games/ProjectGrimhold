using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Calcula geométricamente el polígono de visión lanzando rayos orientados hacia 
    /// los vértices de los obstáculos estáticos relevantes.
    /// </summary>
    public sealed class VisibilityCalculator : MonoBehaviour
    {
        [SerializeField] private VisibilitySettings _settings;
        
        [Tooltip("Si es null, se buscará automáticamente en la escena durante Awake.")]
        [SerializeField] private VisibilityObstacleCache _obstacleCache;

        /// <summary>
        /// Los vértices calculados del polígono de visión ordenados secuencialmente.
        /// El índice 0 siempre es el origen de la visión.
        /// </summary>
        public List<Vector3> PolygonVertices { get; } = new List<Vector3>();

        private void Awake()
        {
            if (_obstacleCache == null)
            {
                _obstacleCache = FindAnyObjectByType<VisibilityObstacleCache>();
            }
        }

        private readonly List<Vector2> _targetVertices = new List<Vector2>();
        private readonly List<RaycastHitInfo> _hits = new List<RaycastHitInfo>();
        private readonly List<float> _angles = new List<float>();

        private struct RaycastHitInfo
        {
            public float Angle;
            public Vector2 Point;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle <= -Mathf.PI) angle += Mathf.PI * 2f;
            while (angle > Mathf.PI) angle -= Mathf.PI * 2f;
            return angle;
        }

        /// <summary>
        /// Ejecuta el algoritmo de raycasting orientado a vértices y genera el polígono resultante.
        /// </summary>
        /// <param name="origin">Punto de emisión de la visión (ej. centro del jugador).</param>
        public void Calculate(Vector2 origin)
        {
            if (_settings == null || _obstacleCache == null)
            {
                return;
            }

            _targetVertices.Clear();
            _hits.Clear();
            _angles.Clear();

            float radius = _settings.ViewRadius;
            LayerMask mask = _settings.ObstacleLayer;

            // 1. Obtener vértices cercanos desde el caché
            _obstacleCache.GetVerticesInRange(origin, radius, _targetVertices);

            // 2. Generar ángulos críticos hacia los vértices de obstáculos
            foreach (var v in _targetVertices)
            {
                Vector2 dir = v - origin;
                float angle = Mathf.Atan2(dir.y, dir.x);
                
                // Disparamos rayos exactamente al vértice y ligeramente desviados
                // para detectar paredes traseras en las esquinas.
                _angles.Add(NormalizeAngle(angle));
                _angles.Add(NormalizeAngle(angle - 0.0001f));
                _angles.Add(NormalizeAngle(angle + 0.0001f));
            }

            // 3. Generar ángulos distribuidos uniformemente para el borde del radio máximo
            int borderRays = _settings.BorderRays;
            float angleStep = (Mathf.PI * 2f) / borderRays;
            for (int i = 0; i < borderRays; i++)
            {
                _angles.Add(NormalizeAngle(i * angleStep));
            }

            // 4. Lanzar los raycasts
            foreach (var angle in _angles)
            {
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                
                // Physics2D.Raycast es muy eficiente, y estamos limitando la cantidad
                // de rayos estrictamente a los vértices relevantes + borde.
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, radius, mask);
                
                if (hit.collider != null)
                {
                    _hits.Add(new RaycastHitInfo { Angle = angle, Point = hit.point });
                }
                else
                {
                    _hits.Add(new RaycastHitInfo { Angle = angle, Point = origin + dir * radius });
                }
            }

            // 5. Ordenar los impactos por ángulo
            // Dado que Atan2 devuelve valores de -PI a PI, este ordenamiento
            // garantiza un polígono estrellado sin cruces (convexo al origen).
            _hits.Sort((a, b) => a.Angle.CompareTo(b.Angle));

            // 6. Construir lista final de vértices
            PolygonVertices.Clear();
            PolygonVertices.Add(origin); // El centro siempre es el índice 0
            
            foreach (var hitInfo in _hits)
            {
                PolygonVertices.Add(hitInfo.Point);
            }
        }
    }
}

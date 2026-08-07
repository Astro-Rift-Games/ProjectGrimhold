using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Indexa y mantiene en cache los vértices de los colisionadores estáticos que actúan
    /// como obstáculos de visión, evitando recalcularlos en cada frame.
    /// </summary>
    public sealed class VisibilityObstacleCache : MonoBehaviour
    {
        [Tooltip("La configuración que indica qué layer(s) bloquean la visión.")]
        [SerializeField] private VisibilitySettings _settings;

        // Caché global de vértices para obstáculos estáticos
        private readonly List<Vector2> _allVertices = new List<Vector2>();

        private void Awake()
        {
            RefreshCache();
        }

        /// <summary>
        /// Recolecta todos los colisionadores válidos del mundo y extrae sus vértices.
        /// (En el MVP solo soporta obstáculos estáticos presentes al inicio de la escena).
        /// </summary>
        public void RefreshCache()
        {
            _allVertices.Clear();
            
            if (_settings == null)
            {
                Debug.LogWarning("[VisibilityObstacleCache] No se asignó VisibilitySettings.");
                return;
            }

            // Buscar todos los colisionadores activos en la escena
            var colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            
            foreach (var col in colliders)
            {
                // Ignorar colisionadores que no pertenezcan al layer de obstáculos
                if (((1 << col.gameObject.layer) & _settings.ObstacleLayer) == 0)
                    continue;

                ExtractVerticesFromCollider(col);
            }
        }

        private void ExtractVerticesFromCollider(Collider2D col)
        {
            if (col is PolygonCollider2D poly)
            {
                for (int i = 0; i < poly.pathCount; i++)
                {
                    var path = poly.GetPath(i);
                    foreach (var p in path)
                    {
                        _allVertices.Add(poly.transform.TransformPoint(p));
                    }
                }
            }
            else if (col is CompositeCollider2D comp)
            {
                var pathPoints = new Vector2[comp.pointCount]; 
                for (int i = 0; i < comp.pathCount; i++)
                {
                    int count = comp.GetPath(i, pathPoints);
                    for (int j = 0; j < count; j++)
                    {
                        _allVertices.Add(comp.transform.TransformPoint(pathPoints[j]));
                    }
                }
            }
            else if (col is BoxCollider2D box)
            {
                Vector2 ext = box.size * 0.5f;
                Vector2 offset = box.offset;
                _allVertices.Add(box.transform.TransformPoint(offset + new Vector2(-ext.x, -ext.y)));
                _allVertices.Add(box.transform.TransformPoint(offset + new Vector2(ext.x, -ext.y)));
                _allVertices.Add(box.transform.TransformPoint(offset + new Vector2(ext.x, ext.y)));
                _allVertices.Add(box.transform.TransformPoint(offset + new Vector2(-ext.x, ext.y)));
            }
            // EdgeCollider2D u otros pueden agregarse en el futuro si es necesario.
        }

        /// <summary>
        /// Llena la lista 'results' con los vértices cacheados que se encuentran
        /// dentro del radio especificado desde el origen.
        /// </summary>
        public void GetVerticesInRange(Vector2 origin, float radius, List<Vector2> results)
        {
            float sqrRadius = radius * radius;
            int count = _allVertices.Count;
            
            for (int i = 0; i < count; i++)
            {
                Vector2 vertex = _allVertices[i];
                // Chequeo de distancia al cuadrado por rendimiento
                if ((vertex - origin).sqrMagnitude <= sqrRadius)
                {
                    results.Add(vertex);
                }
            }
        }
    }
}

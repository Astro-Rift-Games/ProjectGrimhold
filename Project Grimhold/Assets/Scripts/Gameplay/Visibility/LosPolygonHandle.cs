using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Provee consultas geométricas puras sobre el polígono de visión calculado en el frame actual.
    /// Esta clase es instanciada y actualizada por VisibilityMeshBuilder.
    /// </summary>
    public sealed class LosPolygonHandle
    {
        private readonly List<Vector2> _perimeter = new List<Vector2>();

        /// <summary>
        /// Evalúa si un punto en espacio de mundo se encuentra dentro del polígono de visión actual.
        /// Utiliza el algoritmo de ray casting (odd-even rule).
        /// </summary>
        public bool IsInsideLos(Vector2 point)
        {
            if (_perimeter.Count < 3)
                return false;

            bool isInside = false;
            int j = _perimeter.Count - 1;

            for (int i = 0; i < _perimeter.Count; i++)
            {
                Vector2 pi = _perimeter[i];
                Vector2 pj = _perimeter[j];

                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
                {
                    isInside = !isInside;
                }
                j = i;
            }

            return isInside;
        }

        /// <summary>
        /// Actualiza el polígono perimetral con los vértices calculados por VisibilityCalculator.
        /// Excluye el vértice de origen para mantener un polígono simple estrellado.
        /// </summary>
        internal void UpdatePolygon(IReadOnlyList<Vector3> vertices, int startIndex, int count)
        {
            _perimeter.Clear();
            if (vertices == null || vertices.Count < startIndex + count)
            {
                return;
            }

            // Capacidad para evitar expansiones
            if (_perimeter.Capacity < count)
            {
                _perimeter.Capacity = count;
            }

            for (int i = 0; i < count; i++)
            {
                _perimeter.Add(vertices[startIndex + i]);
            }
        }
    }
}

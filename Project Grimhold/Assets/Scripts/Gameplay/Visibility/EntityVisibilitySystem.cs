using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Sistema de nivel de escena que evalúa iterativamente qué entidades caen 
    /// dentro del polígono de visión calculado.
    /// Ejecuta de forma local al cliente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityVisibilitySystem : MonoBehaviour
    {
        private VisibilityMeshBuilder _localMeshBuilder;

        private readonly List<IVisibilityTarget> _targets = new List<IVisibilityTarget>();
        private readonly Dictionary<IVisibilityTarget, bool> _lastState = new Dictionary<IVisibilityTarget, bool>();

        /// <summary>
        /// Registra una nueva entidad para comenzar a evaluar su visibilidad.
        /// </summary>
        public void Register(IVisibilityTarget target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        /// <summary>
        /// Quita una entidad del sistema. No restablece su estado visual actual.
        /// </summary>
        public void Unregister(IVisibilityTarget target)
        {
            if (target != null)
            {
                _targets.Remove(target);
                _lastState.Remove(target);
            }
        }

        /// <summary>
        /// Registers the single LOS producer belonging to this peer's current
        /// local raid avatar. Proxy avatars must never register a builder.
        /// </summary>
        public void RegisterLocalMeshBuilder(VisibilityMeshBuilder meshBuilder)
        {
            if (meshBuilder != null)
            {
                _localMeshBuilder = meshBuilder;
            }
        }

        /// <summary>
        /// Clears the LOS producer only when the caller still owns it, so an old
        /// avatar cannot clear a replacement avatar's local presentation.
        /// </summary>
        public void UnregisterLocalMeshBuilder(VisibilityMeshBuilder meshBuilder)
        {
            if (_localMeshBuilder == meshBuilder)
            {
                _localMeshBuilder = null;
            }
        }

        private void LateUpdate()
        {
            if (_localMeshBuilder == null || !_localMeshBuilder.isActiveAndEnabled ||
                _localMeshBuilder.LosHandle == null)
            {
                return;
            }

            LosPolygonHandle handle = _localMeshBuilder.LosHandle;

            // Iteramos hacia atrás para remover referencias nulas de forma segura
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                IVisibilityTarget target = _targets[i];
                
                // Si la entidad fue destruida (Unity null), la eliminamos
                if (target == null || (target is Object unityObj && unityObj == null))
                {
                    _lastState.Remove(target);
                    _targets.RemoveAt(i);
                    continue;
                }

                // Evaluación geométrica pura usando Point-in-Polygon
                bool inside = target.IsVisible(handle);

                // Sólo aplicamos el cambio si el estado es diferente al registrado previamente
                if (!_lastState.TryGetValue(target, out bool wasInside) || wasInside != inside)
                {
                    target.SetVisible(inside);
                    _lastState[target] = inside;
                }
            }
        }
    }
}

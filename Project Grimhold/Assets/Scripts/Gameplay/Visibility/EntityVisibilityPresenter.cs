using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Componente compañero que otorga visibilidad LOS a cualquier entidad del juego sin acoplar
    /// la lógica de gameplay. Maneja su propio registro con el sistema de visibilidad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityVisibilityPresenter : MonoBehaviour, IVisibilityTarget
    {
        [Tooltip("Renderers que serán ocultados/mostrados cuando cambie la visibilidad de esta entidad.")]
        [SerializeField] private Renderer[] _renderers;
        
        [Tooltip("Offset relativo al transform.position para evaluar la visibilidad. Útil para entidades altas donde el pivot está en la base.")]
        [SerializeField] private Vector2 _visibilityOffset;

        private EntityVisibilitySystem _system;

        public Vector2 VisibilityPoint => (Vector2)transform.position + _visibilityOffset;

        private void OnEnable()
        {
            if (_system == null)
            {
                // Obtenemos el sistema solo una vez en inicialización. 
                // Se usa FindObjectsInactive.Exclude porque el sistema debe estar activo para evaluar.
                _system = FindAnyObjectByType<EntityVisibilitySystem>(FindObjectsInactive.Exclude);
            }

            if (_system != null)
            {
                _system.Register(this);
            }
        }

        private void OnDisable()
        {
            if (_system != null)
            {
                _system.Unregister(this);
            }
        }

        public void SetVisible(bool isVisible)
        {
            if (_renderers == null) return;
            
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = isVisible;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(VisibilityPoint, 0.15f);
        }
#endif
    }
}

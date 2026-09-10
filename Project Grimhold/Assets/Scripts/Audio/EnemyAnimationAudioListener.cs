using UnityEngine;

/// <summary>
/// Puente entre el Animator Controller (en un GameObject hijo) y el EnemyAudioPresenter (en el GameObject raíz).
/// Debe colocarse en el mismo GameObject que contiene el componente Animator para que los AnimationEvents
/// puedan encontrar sus métodos públicos.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAnimationAudioListener : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private EnemyAudioPresenter _presenter;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
    }

    private void CacheDependencies()
    {
        if (_presenter == null)
        {
            _presenter = GetComponentInParent<EnemyAudioPresenter>();
        }
    }

    /// <summary>
    /// Llamado desde un AnimationEvent pasando la clave del sonido configurado en el EnemyAudioConfig (ej: "Step", "Attack").
    /// </summary>
    public void PlayAudioEvent(string soundKey)
    {
        if (_presenter == null)
        {
            CacheDependencies();
        }

        if (_presenter != null)
        {
            _presenter.PlayAudio(soundKey);
        }
    }

    /// <summary>
    /// Método de conveniencia sin parámetros para AnimationEvents de pasos en animaciones de enemigos.
    /// Invoca la clave "Step".
    /// </summary>
    public void PlayFootstep()
    {
        PlayAudioEvent("Step");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

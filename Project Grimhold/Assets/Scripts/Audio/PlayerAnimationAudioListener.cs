using UnityEngine;

/// <summary>
/// Puente entre el Animator Controller (en un GameObject hijo) y el PlayerAudioPresenter (en el GameObject raíz).
/// Debe colocarse en el mismo GameObject que contiene el componente Animator para que los AnimationEvents
/// puedan encontrar sus métodos públicos.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationAudioListener : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private PlayerAudioPresenter _presenter;

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
            _presenter = GetComponentInParent<PlayerAudioPresenter>();
        }
    }

    /// <summary>
    /// Llamado desde un AnimationEvent pasando la clave del sonido configurado en el PlayerAudioConfig (ej: "Movement", "Step").
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
    /// Método de conveniencia sin parámetros para AnimationEvents de pasos en animaciones de caminata/carrera.
    /// Invoca la clave "Movement".
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

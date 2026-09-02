using UnityEngine;

/// <summary>
/// Immutable base configuration for shared attack execution behavior.
/// </summary>
public abstract class AttackConfig : ScriptableObject
{
    [SerializeField]
    private AttackInputMode _inputMode = AttackInputMode.Press;
    public AttackInputMode InputMode => _inputMode;

    /// <summary>
    /// Intenta validar si la configuración actual es válida.
    /// </summary>
    /// <param name="error">Mensaje descriptivo del primer error encontrado.</param>
    /// <returns>True si la configuración es totalmente válida, de lo contrario False.</returns>
    public abstract bool TryValidate(out string error);

    /// <summary>
    /// Realiza validaciones comunes para todos los tipos de ataque.
    /// </summary>
    protected bool TryValidateCommon(out string error)
    {
        if (!System.Enum.IsDefined(typeof(AttackInputMode), _inputMode))
        {
            error = $"{nameof(InputMode)} has an unsupported value (current: {(int)_inputMode}).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected virtual void OnValidate() { }
}

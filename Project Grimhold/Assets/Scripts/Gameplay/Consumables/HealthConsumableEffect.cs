using UnityEngine;

/// <summary>
/// Efecto concreto para pociones de salud u objetos que recuperan vida.
/// </summary>
[CreateAssetMenu(fileName = "HealthConsumableEffect", menuName = "Grimhold/Consumables/Effects/Health")]
public sealed class HealthConsumableEffect : ConsumableEffectBase
{
    [SerializeField, Min(1f)]
    [Tooltip("Cantidad de salud que recupera el consumible.")]
    private float _healAmount = 30f;

    /// <summary>
    /// Cantidad de salud configurada para recuperar.
    /// </summary>
    public float HealAmount => _healAmount;

    public override bool TryApplyEffect(ICharacter target, out string failureReason)
    {
        if (target == null)
        {
            failureReason = "Objetivo no válido.";
            return false;
        }

        if (target is IHealable healable)
        {
            var result = healable.ApplyHealing(new HealRequest(_healAmount));
            if (result.Success)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = result.FailureReason switch
            {
                HealFailureReason.TargetDead => "El objetivo está muerto.",
                HealFailureReason.HealthFull => "La salud ya está al máximo.",
                HealFailureReason.MissingAuthority => "No hay autoridad para curar.",
                HealFailureReason.TargetUnavailable => "El objetivo no puede recibir curación.",
                _ => "No se pudo curar al objetivo."
            };
            return false;
        }

        failureReason = "El objetivo no puede ser curado.";
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _healAmount = Mathf.Max(1f, _healAmount);
    }
#endif
}

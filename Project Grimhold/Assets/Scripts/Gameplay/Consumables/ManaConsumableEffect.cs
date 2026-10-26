using UnityEngine;

/// <summary>
/// Efecto concreto para pociones o fragmentos que recuperan maná.
/// </summary>
[CreateAssetMenu(fileName = "ManaConsumableEffect", menuName = "Grimhold/Consumables/Effects/Mana")]
public sealed class ManaConsumableEffect : ConsumableEffectBase
{
    [SerializeField, Min(1f)]
    [Tooltip("Cantidad de maná que recupera el consumible.")]
    private float _manaAmount = 40f;

    /// <summary>
    /// Cantidad de maná configurada para recuperar.
    /// </summary>
    public float ManaAmount => _manaAmount;

    public override bool CanApply(ICharacter target, out string failureReason)
    {
        if (target == null)
        {
            failureReason = "Objetivo no válido.";
            return false;
        }

        if (target is IManaReceiver manaReceiver)
        {
            if (!target.IsAlive)
            {
                failureReason = "El objetivo está muerto.";
                return false;
            }

            if (target is PlayerCharacter player && player.IsDowned)
            {
                failureReason = "El objetivo está derribado.";
                return false;
            }

            if (manaReceiver.CurrentMana >= manaReceiver.MaximumMana)
            {
                failureReason = "El maná ya está al máximo.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        failureReason = "El objetivo no puede recibir maná.";
        return false;
    }

    public override bool TryApplyEffect(ICharacter target, out string failureReason)
    {
        if (!CanApply(target, out failureReason))
        {
            return false;
        }

        if (target is IManaReceiver manaReceiver)
        {
            return manaReceiver.TryRestoreMana(_manaAmount, out failureReason);
        }

        failureReason = "El objetivo no puede recibir maná.";
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _manaAmount = Mathf.Max(1f, _manaAmount);
    }
#endif
}

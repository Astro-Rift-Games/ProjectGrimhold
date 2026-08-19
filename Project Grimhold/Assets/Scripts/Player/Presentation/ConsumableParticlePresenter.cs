using UnityEngine;

/// <summary>
/// Observa el evento local de consumo exitoso y emite el sistema de partículas
/// configurado en la definición del consumible.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConsumableParticlePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private PlayerConsumableNetworkController _consumableController;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    private void Awake()
    {
        if (_consumableController == null)
        {
            _consumableController = GetComponentInParent<PlayerConsumableNetworkController>();
        }
    }

    private void OnEnable()
    {
        if (_consumableController != null)
        {
            _consumableController.ConsumeConfirmed += OnConsumeConfirmed;
        }
    }

    private void OnDisable()
    {
        if (_consumableController != null)
        {
            _consumableController.ConsumeConfirmed -= OnConsumeConfirmed;
        }
    }

    private void OnConsumeConfirmed(LootId lootId)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGet(lootId.Value, out LootDefinition lootDef))
        {
            return;
        }

        ConsumableDefinition consumableDef = lootDef.ConsumableDefinition;
        if (consumableDef != null && consumableDef.ConsumeParticles != null)
        {
            ParticleEffectPlayer.InstantiateAndPlay(consumableDef.ConsumeParticles, transform.position);
        }
    }
}

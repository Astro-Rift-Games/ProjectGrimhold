using Fusion;
using UnityEngine;

/// <summary>
/// Presents the player's equipped armor visually by layering copies of the base modular sprites.
/// Driven by the authoritative EquipmentRevision from PlayerWeaponEquipmentNetworkController.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerArmorPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private PlayerWeaponEquipmentNetworkController _equipmentSource;

    [Header("Base Renderers (Source)")]
    [SerializeField] private SpriteRenderer _headBase;
    [SerializeField] private SpriteRenderer _bodyBase;
    [SerializeField] private SpriteRenderer _leftHandBase;
    [SerializeField] private SpriteRenderer _rightHandBase;
    [SerializeField] private SpriteRenderer _legsBase;

    [Header("Armor Renderers (Target)")]
    [SerializeField] private SpriteRenderer _helmetVisual;
    [SerializeField] private SpriteRenderer _armorVisual;
    [SerializeField] private SpriteRenderer _leftGloveVisual;
    [SerializeField] private SpriteRenderer _rightGloveVisual;
    [SerializeField] private SpriteRenderer _bootsVisual;

    private int _lastEquipmentRevision = -1;

    // Cache the resolved definitions so we don't fetch them every frame
    private EquipmentVisualDefinition _helmetConfig;
    private EquipmentVisualDefinition _armorConfig;
    private EquipmentVisualDefinition _glovesConfig;
    private EquipmentVisualDefinition _bootsConfig;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        _lastEquipmentRevision = -1;
        CacheDependencies();
        ResolveEquipmentVisualConfigs();
        UpdateVisualsToMatchBase();
    }

    private void LateUpdate()
    {
        if (_equipmentSource == null || _equipmentSource.Object == null || !_equipmentSource.Object.IsValid)
        {
            return;
        }

        int currentRevision = _equipmentSource.ObservedEquipmentRevision;
        if (_lastEquipmentRevision != currentRevision)
        {
            ResolveEquipmentVisualConfigs();
            _lastEquipmentRevision = currentRevision;
        }

        UpdateVisualsToMatchBase();
    }

    private void ResolveEquipmentVisualConfigs()
    {
        _helmetConfig = GetVisualConfig(EquipmentSlot.Helmet);
        _armorConfig = GetVisualConfig(EquipmentSlot.Armor);
        _glovesConfig = GetVisualConfig(EquipmentSlot.Gloves);
        _bootsConfig = GetVisualConfig(EquipmentSlot.Boots);

        // Update presence and static properties (tints) based on config
        ApplyConfigToTarget(_helmetConfig, _helmetVisual);
        ApplyConfigToTarget(_armorConfig, _armorVisual);
        
        ApplyConfigToTarget(_glovesConfig, _leftGloveVisual);
        ApplyConfigToTarget(_glovesConfig, _rightGloveVisual);
        ApplyGloveBaseVisibility();
        
        ApplyConfigToTarget(_bootsConfig, _bootsVisual);
    }

    private void ApplyGloveBaseVisibility()
    {
        bool showBaseHands = _glovesConfig == null;

        if (_leftHandBase != null)
        {
            _leftHandBase.enabled = showBaseHands;
        }

        if (_rightHandBase != null)
        {
            _rightHandBase.enabled = showBaseHands;
        }
    }

    private EquipmentVisualDefinition GetVisualConfig(EquipmentSlot slot)
    {
        if (_equipmentSource.TryGetSlotDefinition(slot, out LootDefinition definition))
        {
            return definition.EquipmentVisualDefinition;
        }
        return null;
    }

    private void ApplyConfigToTarget(EquipmentVisualDefinition config, SpriteRenderer target)
    {
        if (target == null) return;
        
        if (config == null)
        {
            target.enabled = false;
            return;
        }
        
        target.enabled = true;
        target.color = config.UsesBaseSpritesAsPlaceholder ? config.Tint : Color.white;
    }

    private void UpdateVisualsToMatchBase()
    {
        SyncSprite(_headBase, _helmetVisual, _helmetConfig);
        SyncSprite(_bodyBase, _armorVisual, _armorConfig);
        SyncSprite(_leftHandBase, _leftGloveVisual, _glovesConfig);
        SyncSprite(_rightHandBase, _rightGloveVisual, _glovesConfig);
        SyncSprite(_legsBase, _bootsVisual, _bootsConfig);
    }

    private void SyncSprite(SpriteRenderer source, SpriteRenderer target, EquipmentVisualDefinition config)
    {
        if (source != null && target != null && target.enabled && config != null)
        {
            target.sprite = config.ResolveSprite(source.sprite);
            
            // Follow animated renderer state while keeping the equipment deterministically above its base part.
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.sortingOrder = EquipmentLayerSortingRule.ResolveEquipmentSortingOrder(source.sortingOrder);
            target.sortingLayerID = source.sortingLayerID;
        }
    }

    private void CacheDependencies()
    {
        if (_equipmentSource == null)
        {
            _equipmentSource = GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

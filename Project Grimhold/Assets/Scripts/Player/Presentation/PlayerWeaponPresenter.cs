using Fusion;
using UnityEngine;

/// <summary>
/// Presents the active Weapon Set without owning attack motion.
/// The Animator moves the hands; held visuals inherit those transforms through their grips.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    private const int SortingOrderFront = 20;
    private const int SortingOrderBack = -10;

    [Header("References")]
    [SerializeField] private MonoBehaviour _movementStateSource;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private Transform _mainHandGrip;
    [SerializeField] private Transform _mainHandWeaponVisual;
    [SerializeField] private SpriteRenderer _mainHandRenderer;
    [SerializeField] private Transform _offHandGrip;
    [SerializeField] private Transform _offHandVisual;
    [SerializeField] private SpriteRenderer _offHandRenderer;

    private IMovementState _movementState;
    private NetworkBehaviour _movementNetworkBehaviour;
    private LootDefinition _mainHandDefinition;
    private LootDefinition _offHandDefinition;
    private Vector2 _safeFacing = Vector2.down;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        if (!ValidateDependencies())
        {
            enabled = false;
            return;
        }

        RefreshEquipment(force: true);
        RefreshSorting();
    }

    private void OnDisable()
    {
        _safeFacing = Vector2.down;
        _mainHandDefinition = null;
        _offHandDefinition = null;
        SetRendererSprite(_mainHandRenderer, null);
        SetRendererSprite(_offHandRenderer, null);
    }

    private void LateUpdate()
    {
        RefreshEquipment(force: false);
        RefreshSorting();
    }

    private void RefreshEquipment(bool force)
    {
        LootDefinition mainHand = null;
        LootDefinition offHand = null;

        if (CanReadEquipmentState())
        {
            _equipmentSource.TryGetEquippedDefinition(out mainHand);

            WeaponSetSlot activeSet = _equipmentSource.ActiveWeaponSetSlot;
            EquipmentSlot offHandSlot = EquipmentSlotRules.GetOffHandSlot(activeSet);
            if (offHandSlot != EquipmentSlot.None)
            {
                _equipmentSource.TryGetSlotDefinition(offHandSlot, out offHand);
            }
        }

        if (force || !ReferenceEquals(_mainHandDefinition, mainHand))
        {
            _mainHandDefinition = mainHand;
            ApplyMainHandDefinition(mainHand);
        }

        if (force || !ReferenceEquals(_offHandDefinition, offHand))
        {
            _offHandDefinition = offHand;
            ApplyOffHandDefinition(offHand);
        }
    }

    private void ApplyMainHandDefinition(LootDefinition definition)
    {
        WeaponDefinition weapon = definition != null ? definition.WeaponDefinition : null;
        SetRendererSprite(
            _mainHandRenderer,
            weapon != null ? definition.WorldSprite ?? definition.Icon : null);

        if (weapon == null)
        {
            _mainHandWeaponVisual.localPosition = Vector3.zero;
            _mainHandWeaponVisual.localRotation = Quaternion.identity;
            return;
        }

        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        Vector2 gripAlignedPosition =
            PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                presentation.GripPoint,
                Vector2.one,
                presentation.AngleCorrection) + presentation.StanceOffset;

        _mainHandWeaponVisual.localPosition = new Vector3(
            gripAlignedPosition.x,
            gripAlignedPosition.y,
            _mainHandWeaponVisual.localPosition.z);
        _mainHandWeaponVisual.localRotation = Quaternion.Euler(
            0f,
            0f,
            presentation.AngleCorrection);
    }

    private void ApplyOffHandDefinition(LootDefinition definition)
    {
        bool isShield = definition != null && definition.Category == LootCategory.Shield;
        SetRendererSprite(
            _offHandRenderer,
            isShield ? definition.WorldSprite ?? definition.Icon : null);

        _offHandVisual.localPosition = Vector3.zero;
        _offHandVisual.localRotation = Quaternion.identity;
    }

    private void RefreshSorting()
    {
        Vector2 facing = CanReadMovementState()
            ? _movementState.FacingDirection
            : _safeFacing;
        _safeFacing = CharacterVisualDirectionResolver.SanitizeFacing(facing, _safeFacing);
        CharacterVisualDirection direction = CharacterVisualDirectionResolver.Resolve(_safeFacing);
        int order = CharacterVisualDirectionResolver.CalculateSortingOrder(
            direction,
            SortingOrderFront,
            SortingOrderBack);

        _mainHandRenderer.sortingOrder = order;
        _offHandRenderer.sortingOrder = order;
    }

    private void CacheDependencies()
    {
        _equipmentSource ??= GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
        if (_movementStateSource == null)
        {
            _movementStateSource = GetComponentInParent<PlayerMovementNetworkController>();
        }

        _movementState = _movementStateSource as IMovementState;
        _movementNetworkBehaviour = _movementStateSource as NetworkBehaviour;
    }

    private bool CanReadMovementState()
    {
        return _movementState != null &&
            (_movementNetworkBehaviour == null ||
             _movementNetworkBehaviour.Object != null && _movementNetworkBehaviour.Object.IsValid);
    }

    private bool CanReadEquipmentState()
    {
        return _equipmentSource != null &&
            _equipmentSource.Object != null &&
            _equipmentSource.Object.IsValid;
    }

    private bool ValidateDependencies()
    {
        if (_movementState != null &&
            _equipmentSource != null &&
            _mainHandGrip != null &&
            _mainHandWeaponVisual != null &&
            _mainHandRenderer != null &&
            _offHandGrip != null &&
            _offHandVisual != null &&
            _offHandRenderer != null &&
            _mainHandWeaponVisual.IsChildOf(_mainHandGrip) &&
            _offHandVisual.IsChildOf(_offHandGrip))
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerWeaponPresenter)} on '{name}' requires movement, Equipment, " +
            "and held visuals parented beneath their matching hand grips.",
            this);
        return false;
    }

    private static void SetRendererSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        renderer.enabled = sprite != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

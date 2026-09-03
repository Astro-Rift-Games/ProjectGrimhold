using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Captures local device input and exposes it as gameplay intentions.
///
/// This component does not execute movement, attacks or any other gameplay
/// action. Fusion consumes the accumulated state through
/// <see cref="ConsumeNetworkInput"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInputReader : MonoBehaviour
{
    [Header("Aim")]
    [SerializeField]
    private Camera _worldCamera;

    [SerializeField]
    private float _aimPlaneZ;

    private PlayerInputActions _inputActions;
    private InputAction _weaponSlot1Action;
    private InputAction _weaponSlot2Action;
    private InputAction _sprintAction;
    private InputAction _toggleAttributesAction;

    private Vector2 _moveDirection;
    private Vector2 _aimWorldPosition;

    private NetworkButtons _buttons;
    private NetworkButtons _pendingButtons;
    private bool _resetAccumulatedButtons;
    private int _gameplaySuppressionCount;
    private bool _primaryAttackRequiresRelease;
    private bool _interactRequiresRelease;

    /// <summary>
    /// Raised for the local-only action that opens or closes the raid inventory.
    /// This intention is never included in <see cref="PlayerNetworkInput"/>.
    /// </summary>
    public event Action InventoryToggleRequested;

    /// <summary>
    /// Raised for the local-only action that requests closing the raid inventory.
    /// Returns true if an open inventory screen consumed the close request.
    /// It never opens a closed inventory and is not included in network input.
    /// </summary>
    public event Func<bool> InventoryCloseRequested;

    /// <summary>
    /// Raised for the local-only action that opens or closes the Town attribute panel.
    /// This intention is never included in <see cref="PlayerNetworkInput"/>.
    /// </summary>
    public event Action AttributesToggleRequested;

    /// <summary>
    /// Raised for the local-only action that requests toggling the local menu.
    /// </summary>
    public event Action MenuToggleRequested;

    /// <summary>
    /// Raised for the local-only interaction press edge.
    /// Presentation elements observe this event (e.g., to close the looting screen)
    /// without sending network RPCs or advancing gameplay simulation.
    /// </summary>
    public event Action InteractPressedLocally;

    private void Awake()
    {
        CacheDependencies();

        _inputActions = new PlayerInputActions();
        _weaponSlot1Action = _inputActions.asset.FindAction("Gameplay/SelectWeaponSlot1", true);
        _weaponSlot2Action = _inputActions.asset.FindAction("Gameplay/SelectWeaponSlot2", true);
        _sprintAction = _inputActions.asset.FindAction("Gameplay/Sprint", true);
        _toggleAttributesAction = _inputActions.asset.FindAction("LocalUI/ToggleAttributes", true);
    }

    private void OnEnable()
    {
        _inputActions.Gameplay.Interact.performed += OnInteractPerformed;
        _inputActions.Gameplay.Interact.canceled += OnInteractCanceled;
        _inputActions.Gameplay.PrimaryAttack.canceled += OnPrimaryAttackCanceled;
        _inputActions.LocalUI.ToggleInventory.performed += OnToggleInventoryPerformed;
        _inputActions.LocalUI.CloseInventory.performed += OnCloseInventoryPerformed;
        _toggleAttributesAction.performed += OnToggleAttributesPerformed;
        _inputActions.Gameplay.Enable();
        _inputActions.LocalUI.Enable();
    }

    private void Update()
    {
        ResetAccumulatedButtonsIfRequired();

        UpdateDiscreteButtonRearm();
        ReadPrimaryAttack();
        ReadSprint();
        ReadWeaponSelection();
    }

    private void OnDisable()
    {
        _inputActions.Gameplay.Interact.performed -= OnInteractPerformed;
        _inputActions.Gameplay.Interact.canceled -= OnInteractCanceled;
        _inputActions.Gameplay.PrimaryAttack.canceled -= OnPrimaryAttackCanceled;
        _inputActions.LocalUI.ToggleInventory.performed -= OnToggleInventoryPerformed;
        _inputActions.LocalUI.CloseInventory.performed -= OnCloseInventoryPerformed;
        _toggleAttributesAction.performed -= OnToggleAttributesPerformed;
        _inputActions.Gameplay.Disable();
        _inputActions.LocalUI.Disable();
        ResetInputState();
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_interactRequiresRelease)
        {
            return;
        }

        bool wasSuppressed = IsGameplayInputSuppressed;

        InteractPressedLocally?.Invoke();

        if (wasSuppressed || IsGameplayInputSuppressed)
        {
            return;
        }

        _pendingButtons.Set(PlayerInputButton.Interact, true);
    }

    private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
    {
        InventoryToggleRequested?.Invoke();
    }

    private void OnToggleAttributesPerformed(InputAction.CallbackContext context)
    {
        AttributesToggleRequested?.Invoke();
    }

    private void OnCloseInventoryPerformed(InputAction.CallbackContext context)
    {
        bool consumedByInventory = false;
        if (InventoryCloseRequested != null)
        {
            foreach (Func<bool> handler in InventoryCloseRequested.GetInvocationList())
            {
                if (handler.Invoke())
                {
                    consumedByInventory = true;
                }
            }
        }

        if (!consumedByInventory)
        {
            MenuToggleRequested?.Invoke();
        }
    }

    private void OnInteractCanceled(InputAction.CallbackContext context)
    {
        _interactRequiresRelease = false;
    }

    private void OnPrimaryAttackCanceled(InputAction.CallbackContext context)
    {
        _primaryAttackRequiresRelease = false;
    }

    /// <summary>
    /// Acquires ownership of a local gameplay-input suppression.
    /// Continuous and discrete gameplay intentions produce a default network payload
    /// until every acquisition has been released.
    /// </summary>
    public IDisposable AcquireGameplayInputSuppression()
    {
        if (_gameplaySuppressionCount == 0)
        {
            ResetGameplayIntent();
        }

        _gameplaySuppressionCount++;
        return new GameplayInputSuppression(this);
    }

    public bool IsGameplayInputSuppressed => _gameplaySuppressionCount > 0;

    /// <summary>
    /// Returns the latest local intentions accumulated since Fusion's
    /// previous input collection.
    /// </summary>
    public PlayerNetworkInput ConsumeNetworkInput()
    {
        if (IsGameplayInputSuppressed)
        {
            ResetGameplayIntent();
            return default;
        }

        ReadMovement();
        ReadAimWorldPosition();

        // The reset is deferred until the next Update. This preserves a
        // latched press when Fusion requests input more than once in the
        // same rendered frame.
        _resetAccumulatedButtons = true;

        NetworkButtons combinedButtons = _buttons;

        if (_pendingButtons.IsSet(PlayerInputButton.Interact))
        {
            combinedButtons.Set(PlayerInputButton.Interact, true);
        }

        if (_pendingButtons.IsSet(PlayerInputButton.WeaponSlot1))
        {
            combinedButtons.Set(PlayerInputButton.WeaponSlot1, true);
        }

        if (_pendingButtons.IsSet(PlayerInputButton.WeaponSlot2))
        {
            combinedButtons.Set(PlayerInputButton.WeaponSlot2, true);
        }

        PlayerNetworkInput input = new PlayerNetworkInput
        {
            MoveDirection = _moveDirection,
            AimWorldPosition = _aimWorldPosition,
            Buttons = combinedButtons
        };

        _pendingButtons.Set(PlayerInputButton.Interact, false);
        _pendingButtons.Set(PlayerInputButton.WeaponSlot1, false);
        _pendingButtons.Set(PlayerInputButton.WeaponSlot2, false);

        return input;
    }

    private void ReadMovement()
    {
        _moveDirection =
            _inputActions.Gameplay.Move.ReadValue<Vector2>();
    }

    private void ReadAimWorldPosition()
    {
        if (_worldCamera == null)
        {
            _aimWorldPosition = Vector2.zero;
            return;
        }

        Vector2 screenPosition =
            _inputActions.Gameplay.AimPosition.ReadValue<Vector2>();

        Transform cameraTransform = _worldCamera.transform;

        Vector3 aimPlanePoint =
            new(0f, 0f, _aimPlaneZ);

        float distanceToAimPlane = Vector3.Dot(
            aimPlanePoint - cameraTransform.position,
            cameraTransform.forward);

        if (distanceToAimPlane < 0f)
        {
            _aimWorldPosition = Vector2.zero;
            return;
        }

        Vector3 screenPoint = new(
            screenPosition.x,
            screenPosition.y,
            distanceToAimPlane);

        Vector3 worldPosition =
            _worldCamera.ScreenToWorldPoint(screenPoint);

        _aimWorldPosition = new Vector2(
            worldPosition.x,
            worldPosition.y);
    }

    private void ReadPrimaryAttack()
    {
        var primaryAttackAction =
            _inputActions.Gameplay.PrimaryAttack;

        if (IsGameplayInputSuppressed)
        {
            _buttons.Set(PlayerInputButton.PrimaryAttack, false);
            return;
        }

        if (_primaryAttackRequiresRelease)
        {
            if (!primaryAttackAction.IsPressed())
            {
                _primaryAttackRequiresRelease = false;
            }

            _buttons.Set(PlayerInputButton.PrimaryAttack, false);
            return;
        }

        AccumulateButton(
            PlayerInputButton.PrimaryAttack,
            primaryAttackAction.IsPressed(),
            primaryAttackAction.WasPressedThisFrame());
    }

    private void ReadWeaponSelection()
    {
        if (IsGameplayInputSuppressed)
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSlot1, false);
            _pendingButtons.Set(PlayerInputButton.WeaponSlot2, false);
            return;
        }

        if (_weaponSlot1Action.WasPressedThisFrame())
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSlot1, true);
        }

        if (_weaponSlot2Action.WasPressedThisFrame())
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSlot2, true);
        }
    }

    private void ReadSprint()
    {
        _buttons.Set(
            PlayerInputButton.Sprint,
            !IsGameplayInputSuppressed && _sprintAction.IsPressed());
    }

    private void AccumulateButton(
        PlayerInputButton button,
        bool isPressed,
        bool wasPressedThisFrame)
    {
        // IsPressed transports the held state for automatic attacks.
        // WasPressedThisFrame preserves a very short tap until Fusion
        // consumes the input.
        if (isPressed || wasPressedThisFrame)
        {
            _buttons.Set(button, true);
        }
    }

    private void ResetAccumulatedButtonsIfRequired()
    {
        if (!_resetAccumulatedButtons)
        {
            return;
        }

        _buttons = default;
        _resetAccumulatedButtons = false;
    }

    private void ResetInputState()
    {
        _moveDirection = Vector2.zero;
        _aimWorldPosition = Vector2.zero;
        ResetDiscreteInputState();
    }

    private void ResetGameplayIntent()
    {
        _moveDirection = Vector2.zero;
        _aimWorldPosition = Vector2.zero;
        ResetDiscreteInputState();
    }

    private void ResetDiscreteInputState()
    {
        _buttons = default;
        _pendingButtons = default;
        _resetAccumulatedButtons = false;
    }

    private void UpdateDiscreteButtonRearm()
    {
        if (_interactRequiresRelease && !_inputActions.Gameplay.Interact.IsPressed())
        {
            _interactRequiresRelease = false;
        }

        if (IsGameplayInputSuppressed)
        {
            ResetDiscreteInputState();
        }
    }

    private void ReleaseGameplayInputSuppression()
    {
        if (_gameplaySuppressionCount <= 0)
        {
            return;
        }

        _gameplaySuppressionCount--;
        if (_gameplaySuppressionCount > 0)
        {
            return;
        }

        ResetGameplayIntent();
        _primaryAttackRequiresRelease = _inputActions.Gameplay.PrimaryAttack.IsPressed();
        _interactRequiresRelease = _inputActions.Gameplay.Interact.IsPressed();
    }

    private sealed class GameplayInputSuppression : IDisposable
    {
        private PlayerInputReader _owner;

        public GameplayInputSuppression(PlayerInputReader owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_owner == null)
            {
                return;
            }

            PlayerInputReader owner = _owner;
            _owner = null;
            owner.ReleaseGameplayInputSuppression();
        }
    }

    private void CacheDependencies()
    {
        if (_worldCamera == null)
        {
            _worldCamera = Camera.main;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

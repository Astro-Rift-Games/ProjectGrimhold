using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
    private InputAction _weaponSetAAction;
    private InputAction _weaponSetBAction;
    private InputAction _sprintAction;
    private InputAction _secondaryAction;
    private InputAction _toggleAttributesAction;
    private InputAction _abilitySlot1Action;
    private InputAction _abilitySlot2Action;

    private Vector2 _moveDirection;
    private Vector2 _aimWorldPosition;

    private NetworkButtons _buttons;
    private NetworkButtons _pendingButtons;
    private bool _resetAccumulatedButtons;
    private int _gameplaySuppressionCount;
    private bool _primaryAttackRequiresRelease;
    private bool _secondaryActionRequiresRelease;
    private bool _interactRequiresRelease;
    private bool _abilitySlot1RequiresRelease;
    private bool _abilitySlot2RequiresRelease;

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
        _weaponSetAAction = _inputActions.asset.FindAction("Gameplay/SelectWeaponSetA", true);
        _weaponSetBAction = _inputActions.asset.FindAction("Gameplay/SelectWeaponSetB", true);
        _sprintAction = _inputActions.asset.FindAction("Gameplay/Sprint", true);
        _secondaryAction = _inputActions.asset.FindAction("Gameplay/SecondaryAction", true);
        _toggleAttributesAction = _inputActions.asset.FindAction("LocalUI/ToggleAttributes", true);
        _abilitySlot1Action = _inputActions.asset.FindAction("Gameplay/AbilitySlot1", true);
        _abilitySlot2Action = _inputActions.asset.FindAction("Gameplay/AbilitySlot2", true);
    }

    private void OnEnable()
    {
        _inputActions.Gameplay.Interact.performed += OnInteractPerformed;
        _inputActions.Gameplay.Interact.canceled += OnInteractCanceled;
        _inputActions.Gameplay.PrimaryAttack.canceled += OnPrimaryAttackCanceled;
        _secondaryAction.canceled += OnSecondaryActionCanceled;
        _inputActions.LocalUI.ToggleInventory.performed += OnToggleInventoryPerformed;
        _inputActions.LocalUI.CloseInventory.performed += OnCloseInventoryPerformed;
        _toggleAttributesAction.performed += OnToggleAttributesPerformed;
        _abilitySlot1Action.performed += OnAbilityPerformed;
        _abilitySlot2Action.performed += OnAbilityPerformed;
        _abilitySlot1Action.canceled += OnAbilityCanceled;
        _abilitySlot2Action.canceled += OnAbilityCanceled;
        _inputActions.Gameplay.Enable();
        _inputActions.LocalUI.Enable();
        // Action state is initially unchecked after enabling. Inspect the controls
        // as well so a held key cannot become a new request on the next input update.
        _abilitySlot1RequiresRelease = IsAbilityControlHeld(_abilitySlot1Action);
        _abilitySlot2RequiresRelease = IsAbilityControlHeld(_abilitySlot2Action);
    }

    private void Update()
    {
        ResetAccumulatedButtonsIfRequired();

        UpdateDiscreteButtonRearm();
        ReadPrimaryAttack();
        ReadSecondaryAction();
        ReadAbility(_abilitySlot1Action, PlayerInputButton.AbilitySlot1, ref _abilitySlot1RequiresRelease);
        ReadAbility(_abilitySlot2Action, PlayerInputButton.AbilitySlot2, ref _abilitySlot2RequiresRelease);
        ReadSprint();
        ReadWeaponSelection();
    }

    private void OnDisable()
    {
        _inputActions.Gameplay.Interact.performed -= OnInteractPerformed;
        _inputActions.Gameplay.Interact.canceled -= OnInteractCanceled;
        _inputActions.Gameplay.PrimaryAttack.canceled -= OnPrimaryAttackCanceled;
        _secondaryAction.canceled -= OnSecondaryActionCanceled;
        _inputActions.LocalUI.ToggleInventory.performed -= OnToggleInventoryPerformed;
        _inputActions.LocalUI.CloseInventory.performed -= OnCloseInventoryPerformed;
        _toggleAttributesAction.performed -= OnToggleAttributesPerformed;
        _abilitySlot1Action.performed -= OnAbilityPerformed;
        _abilitySlot2Action.performed -= OnAbilityPerformed;
        _abilitySlot1Action.canceled -= OnAbilityCanceled;
        _abilitySlot2Action.canceled -= OnAbilityCanceled;
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

    private void OnSecondaryActionCanceled(InputAction.CallbackContext context)
    {
        _secondaryActionRequiresRelease = false;
    }

    private void OnAbilityPerformed(InputAction.CallbackContext context)
    {
        if (IsGameplayInputSuppressed) return;
        bool isSlot1 = context.action == _abilitySlot1Action;
        if (isSlot1 ? _abilitySlot1RequiresRelease : _abilitySlot2RequiresRelease) return;
        // Latch the press even if a tap ends before the next reader Update.
        _pendingButtons.Set(isSlot1 ? PlayerInputButton.AbilitySlot1 : PlayerInputButton.AbilitySlot2, true);
    }

    private void ReadAbility(InputAction action, PlayerInputButton button, ref bool requiresRelease)
    {
        if (IsGameplayInputSuppressed || requiresRelease)
        {
            if (requiresRelease && !IsAbilityControlHeld(action)) requiresRelease = false;
            _buttons.Set(button, false);
            _pendingButtons.Set(button, false);
            return;
        }
        AccumulateButton(button, action.IsPressed(),
            action.WasPressedThisFrame() || _pendingButtons.IsSet(button));
    }

    private void OnAbilityCanceled(InputAction.CallbackContext context)
    {
        if (context.action == _abilitySlot1Action) _abilitySlot1RequiresRelease = false;
        else _abilitySlot2RequiresRelease = false;
    }

    private static bool IsAbilityControlHeld(InputAction action)
    {
        if (action.IsPressed()) return true;
        foreach (var control in action.controls)
        {
            if (control is ButtonControl button && button.isPressed) return true;
        }
        return false;
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

        // A new ability tap can arrive after the previous collection requested
        // a deferred reset. Keep it in pending state until it is collected.
        _buttons.Set(PlayerInputButton.AbilitySlot1,
            _buttons.IsSet(PlayerInputButton.AbilitySlot1) || _pendingButtons.IsSet(PlayerInputButton.AbilitySlot1));
        _buttons.Set(PlayerInputButton.AbilitySlot2,
            _buttons.IsSet(PlayerInputButton.AbilitySlot2) || _pendingButtons.IsSet(PlayerInputButton.AbilitySlot2));

        NetworkButtons combinedButtons = _buttons;

        if (_pendingButtons.IsSet(PlayerInputButton.Interact))
        {
            combinedButtons.Set(PlayerInputButton.Interact, true);
        }

        if (_pendingButtons.IsSet(PlayerInputButton.WeaponSetA))
        {
            combinedButtons.Set(PlayerInputButton.WeaponSetA, true);
        }

        if (_pendingButtons.IsSet(PlayerInputButton.WeaponSetB))
        {
            combinedButtons.Set(PlayerInputButton.WeaponSetB, true);
        }

        PlayerNetworkInput input = new PlayerNetworkInput
        {
            MoveDirection = _moveDirection,
            AimWorldPosition = _aimWorldPosition,
            Buttons = combinedButtons
        };

        _pendingButtons.Set(PlayerInputButton.Interact, false);
        _pendingButtons.Set(PlayerInputButton.WeaponSetA, false);
        _pendingButtons.Set(PlayerInputButton.WeaponSetB, false);
        _pendingButtons.Set(PlayerInputButton.AbilitySlot1, false);
        _pendingButtons.Set(PlayerInputButton.AbilitySlot2, false);

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

    private void ReadSecondaryAction()
    {
        if (IsGameplayInputSuppressed)
        {
            _buttons.Set(PlayerInputButton.SecondaryAction, false);
            return;
        }

        if (_secondaryActionRequiresRelease)
        {
            if (!_secondaryAction.IsPressed())
            {
                _secondaryActionRequiresRelease = false;
            }

            _buttons.Set(PlayerInputButton.SecondaryAction, false);
            return;
        }

        AccumulateButton(
            PlayerInputButton.SecondaryAction,
            _secondaryAction.IsPressed(),
            _secondaryAction.WasPressedThisFrame());
    }

    private void ReadWeaponSelection()
    {
        if (IsGameplayInputSuppressed)
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSetA, false);
            _pendingButtons.Set(PlayerInputButton.WeaponSetB, false);
            return;
        }

        if (_weaponSetAAction.WasPressedThisFrame())
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSetA, true);
        }

        if (_weaponSetBAction.WasPressedThisFrame())
        {
            _pendingButtons.Set(PlayerInputButton.WeaponSetB, true);
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
        _secondaryActionRequiresRelease = _secondaryAction.IsPressed();
        _interactRequiresRelease = _inputActions.Gameplay.Interact.IsPressed();
        _abilitySlot1RequiresRelease = IsAbilityControlHeld(_abilitySlot1Action);
        _abilitySlot2RequiresRelease = IsAbilityControlHeld(_abilitySlot2Action);
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

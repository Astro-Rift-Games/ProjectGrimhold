using UnityEngine;

/// <summary>
/// Projects the Quick Slot bindings of the local Raid player into the HUD and turns a shortcut
/// press into the same authoritative consume request the Inventory uses. It holds no gameplay
/// state: amounts come from the observed inventory and the consume itself is validated by the
/// State Authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidQuickSlotHudPresenter : MonoBehaviour
{
    [SerializeField]
    private RaidQuickSlotHudView _view;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    private QuickSlotController _quickSlots;
    private PlayerConsumableNetworkController _consumable;
    private PlayerInputReader _inputReader;
    private PlayerCharacter _character;
    private bool _isBound;
    private bool _observedDowned;

    /// <summary>Binds the HUD. A null controller leaves the HUD unbound and empty.</summary>
    public void Bind(
        QuickSlotController quickSlots,
        PlayerConsumableNetworkController consumable,
        PlayerInputReader inputReader,
        PlayerCharacter character)
    {
        Unbind();

        if (quickSlots == null || consumable == null || inputReader == null || character == null ||
            _view == null || !_view.HasAllSlots || _lootCatalog == null)
        {
            Debug.LogError($"{nameof(RaidQuickSlotHudPresenter)} has missing binding or serialized dependencies.", this);
            return;
        }

        _quickSlots = quickSlots;
        _consumable = consumable;
        _inputReader = inputReader;
        _character = character;
        _isBound = true;
        _observedDowned = character.IsDowned;
        _quickSlots.Changed += Refresh;
        _inputReader.QuickSlotActivated += OnQuickSlotActivated;
        _consumable.ConsumeRejected += OnConsumeRejected;
        Refresh();
    }

    public void Unbind()
    {
        if (_quickSlots != null)
        {
            _quickSlots.Changed -= Refresh;
        }

        if (_inputReader != null)
        {
            _inputReader.QuickSlotActivated -= OnQuickSlotActivated;
        }

        if (_consumable != null)
        {
            _consumable.ConsumeRejected -= OnConsumeRejected;
        }

        _quickSlots = null;
        _consumable = null;
        _inputReader = null;
        _character = null;
        _isBound = false;

        if (_view != null && _view.HasAllSlots)
        {
            for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
            {
                _view.GetSlot(i).PresentEmpty();
            }

            _view.HideMessage();
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        // Downed state is not an inventory or profile change, so it is observed here.
        if (_isBound && _character != null && _character.IsDowned != _observedDowned)
        {
            _observedDowned = _character.IsDowned;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!_isBound)
        {
            return;
        }

        QuickSlotBindings bindings = _quickSlots.Bindings;
        for (int slot = 0; slot < QuickSlotBindings.SlotCount; slot++)
        {
            RaidQuickSlotHudSlotView view = _view.GetSlot(slot);
            LootId lootId = bindings.Get(slot);
            if (!lootId.IsValid)
            {
                view.PresentEmpty();
                continue;
            }

            Sprite icon = null;
            if (_lootCatalog.TryGet(lootId.Value, out LootDefinition definition))
            {
                icon = definition.Icon;
            }

            int amount = _quickSlots.GetAvailableAmount(lootId);
            view.Present(icon, amount, amount <= 0 || _observedDowned);
        }
    }

    private void OnQuickSlotActivated(int slotIndex)
    {
        if (!_isBound || !QuickSlotBindings.IsValidSlotIndex(slotIndex))
        {
            return;
        }

        LootId lootId = _quickSlots.Bindings.Get(slotIndex);
        if (!lootId.IsValid)
        {
            return;
        }

        if (_character.IsDowned)
        {
            _view.ShowMessage("Estás abatido.");
            return;
        }

        if (_quickSlots.GetAvailableAmount(lootId) <= 0)
        {
            _view.ShowMessage("No tienes unidades disponibles.");
            return;
        }

        // A request already in flight is not an error: the shortcut is simply ignored.
        if (_consumable.HasRequestInFlight)
        {
            return;
        }

        if (!_consumable.TryRequestConsume(lootId))
        {
            _view.ShowMessage("No puedes usar este objeto ahora.");
        }
    }

    private void OnConsumeRejected(ConsumableFailureReason reason)
    {
        // The Inventory shows its own feedback while open; shortcuts cannot fire then.
        string message = reason switch
        {
            ConsumableFailureReason.TargetDead => "Estás muerto.",
            ConsumableFailureReason.HealthFull => "Tu salud ya está al máximo.",
            ConsumableFailureReason.InsufficientAmount => "No tienes unidades disponibles.",
            ConsumableFailureReason.TargetUnavailable => "No puedes usar objetos ahora.",
            _ => "No se pudo usar el objeto."
        };
        _view.ShowMessage(message);
    }
}

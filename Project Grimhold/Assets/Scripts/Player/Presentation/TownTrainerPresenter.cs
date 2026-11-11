using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the Town Trainer weapon selection screen upon interaction or dialogue completion
/// with a <see cref="TownTrainerNpcInteractable"/>.
/// Only Input Authority opens the UI and commits requests to the local profile.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownTrainerPresenter : MonoBehaviour
{
    private const float MaxInteractionDistance = 4f;

    [SerializeField] private TownTrainerView _view;
    [SerializeField] private PlayerInteractionNetworkController _interactionController;
    [SerializeField] private DialoguePresenter _dialoguePresenter;
    [SerializeField] private LootDefinitionCatalog _lootCatalog;

    private ITownEquipmentMutationEndpoint _equipmentEndpoint;
    private bool _requestInFlight;
    private NetworkObject _openNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private NetworkRunner _runner;
    private bool _isBound;

    public TownTrainerView View => _view;
    public bool IsOpen => _view != null && _view.IsOpen;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
        if (_view != null && _view.gameObject != null)
        {
            Destroy(_view.gameObject);
            _view = null;
        }
    }

    private void Update()
    {
        if (_view == null || !_view.IsOpen)
        {
            return;
        }

        if (_openNpc == null || !_openNpc.IsValid ||
            Vector3.Distance(transform.position, _openNpc.transform.position) > MaxInteractionDistance)
        {
            ClosePanel();
        }
    }

    public void BindEndpoint(ITownEquipmentMutationEndpoint endpoint, LocalProfileStore store, LootDefinitionCatalog lootCatalog = null)
    {
        _equipmentEndpoint = endpoint;
        if (lootCatalog == null && store != null) lootCatalog = store.LootCatalog;
        if (lootCatalog != null)
        {
            _lootCatalog = lootCatalog;
        }
    }

    private void CacheDependencies()
    {
        if (_interactionController == null)
        {
            _interactionController = GetComponent<PlayerInteractionNetworkController>();
        }

        if (_dialoguePresenter == null)
        {
            _dialoguePresenter = GetComponent<DialoguePresenter>();
        }

        if (_runner == null)
        {
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                _runner = netObj.Runner;
            }
        }
    }

    private void Bind()
    {
        if (_isBound) return;
        CacheDependencies();

        if (_interactionController != null)
        {
            _interactionController.InteractionResolved += OnInteractionResolved;
        }

        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally += OnDialogueCompletedNormally;
        }

        _isBound = true;
    }

    private void Unbind()
    {
        ClosePanel();

        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally -= OnDialogueCompletedNormally;
        }

        _isBound = false;
    }

    private void EnsureView()
    {
        if (_view == null)
        {
            _view = GetComponentInChildren<TownTrainerView>(true);
        }

        if (_view == null)
        {
            _view = TownTrainerView.Create(transform);
        }

        _view.WeaponSelected -= OnWeaponSelected;
        _view.WeaponSelected += OnWeaponSelected;
        _view.CloseRequested -= ClosePanel;
        _view.CloseRequested += ClosePanel;
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0)
        {
            return;
        }

        NetworkObject target = ResolveNetworkObject(interactionEvent.TargetId);
        if (target == null || !target.TryGetBehaviour(out TownTrainerNpcInteractable _))
        {
            return;
        }

        // If the NPC has a dialogue trigger, allow the dialogue sequence to play first.
        if (target.GetComponentInChildren<IDialogueTrigger>() != null)
        {
            return;
        }

        OpenForTarget(target);
    }

    private void OnDialogueCompletedNormally(EntityId targetId)
    {
        NetworkObject target = ResolveNetworkObject(targetId);
        if (target == null || !target.TryGetBehaviour(out TownTrainerNpcInteractable _))
        {
            return;
        }

        if (_view != null && _view.IsOpen)
        {
            return;
        }

        OpenForTarget(target);
    }

    private NetworkObject ResolveNetworkObject(EntityId entityId)
    {
        if (_runner == null)
        {
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                _runner = netObj.Runner;
            }
        }

        if (_runner != null && _runner.IsRunning)
        {
            var networkId = new NetworkId { Raw = unchecked((uint)entityId.Value) };
            if (_runner.TryFindObject(networkId, out NetworkObject target))
            {
                return target;
            }
        }

        return null;
    }

    public void OpenForTarget(NetworkObject target)
    {
        _openNpc = target;
        EnsureView();

        var options = ResolveTrainingWeaponOptions();
        _view.Present(options);

        AcquireInputSuppression();
        if (_equipmentEndpoint == null)
        {
            Debug.LogError("[TownTrainerPresenter] Falta el endpoint de armas de entrenamiento.", this);
            _view.SetFeedback("El entrenador no está disponible. Intentá nuevamente más tarde.", Color.red);
        }
        else if (!_equipmentEndpoint.CanMutate)
            _view.SetFeedback("No podés cambiar de arma mientras estás Listo o hay una solicitud en curso.", Color.red);
        else if (options.Count > 0)
            _view.SetFeedback("Selecciona un arma. Sustituirá automáticamente cualquier arma de entrenamiento previa.", new Color(0.7f, 0.75f, 0.8f, 1f));
        _view.SetCanSelect(_equipmentEndpoint != null && _equipmentEndpoint.CanMutate && !_requestInFlight);
        _view.Open();
    }

    public void ClosePanel()
    {
        if (_view != null && _view.IsOpen)
        {
            _view.Close();
        }

        ReleaseInputSuppression();

        if (_openNpc != null)
        {
            _openNpc.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
            _openNpc = null;
        }
    }

    private IReadOnlyList<TrainingWeaponOption> ResolveTrainingWeaponOptions()
    {
        var options = new List<TrainingWeaponOption>();

        if (_lootCatalog != null)
        {
            for (int i = 0; i < _lootCatalog.DefinitionCount; i++)
            {
                if (_lootCatalog.TryGetByIndex(i, out LootDefinition def) && def != null && def.IsTrainingWeapon)
                {
                    options.Add(TrainingWeaponOption.FromDefinition(def));
                }
            }
        }

        if (options.Count == 0)
        {
            Debug.LogError("[TownTrainerPresenter] El catálogo no contiene armas de entrenamiento.", this);
        }

        return options;
    }

    private async void OnWeaponSelected(LootId weaponId)
    {
        if (_requestInFlight) return;
        if (_equipmentEndpoint == null)
        {
            Debug.LogError("[TownTrainerPresenter] Falta el endpoint de armas de entrenamiento.", this);
            _view.SetFeedback("El entrenador no está disponible. Intentá nuevamente más tarde.", Color.red);
            return;
        }
        if (!_equipmentEndpoint.CanMutate)
        {
            _view.SetFeedback("No podés cambiar de arma mientras estás Listo o hay una solicitud en curso.", Color.red);
            return;
        }
        _requestInFlight = true;
        _view.SetCanSelect(false);
        _view.SetFeedback("Solicitando arma…", Color.white);
        try
        {
            var result = await _equipmentEndpoint.RequestTrainingWeaponAsync(weaponId, EquipmentSlot.WeaponSetAMainHand);
            if (this == null || _view == null) return;
            if (result == StashOperationResult.Success)
                _view.SetFeedback($"¡Has recibido tu {ResolveWeaponDisplayName(weaponId)}! Se ha retirado cualquier arma de entrenamiento previa.", new Color(0.35f, 1f, 0.45f, 1f));
            else
                _view.SetFeedback(result switch
                {
                    StashOperationResult.ReservationPending => "No podés cambiar de arma mientras hay una expedición pendiente.",
                    StashOperationResult.InventoryFull => "No hay espacio para guardar el arma reemplazada.",
                    StashOperationResult.InvalidInventory => "Tu inventario cambió o necesita actualizarse. Volvé a intentarlo.",
                    _ => "No se pudo confirmar el arma. Revisá la conexión e intentá nuevamente."
                }, Color.red);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[TownTrainerPresenter] Error al solicitar el arma: {exception.Message}", this);
            if (this != null && _view != null) _view.SetFeedback("No se pudo confirmar el arma. Intentá nuevamente.", Color.red);
        }
        finally
        {
            _requestInFlight = false;
            if (this != null && _view != null) _view.SetCanSelect(_equipmentEndpoint != null && _equipmentEndpoint.CanMutate);
        }
    }

    private string ResolveWeaponDisplayName(LootId weaponId)
    {
        if (_lootCatalog != null && _lootCatalog.TryGet(weaponId.Value, out LootDefinition def) && !string.IsNullOrWhiteSpace(def.DisplayName))
        {
            return def.DisplayName;
        }

        return weaponId.Value switch
        {
            "training_short_sword" => "Espada Corta de Entrenamiento",
            "training_dagger" => "Daga de Entrenamiento",
            "training_wand" => "Varita de Entrenamiento",
            _ => weaponId.Value
        };
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null) return;

        if (_runner != null)
        {
            LocalInputContext context = _runner.GetComponent<LocalInputContext>();
            if (context != null && context.Reader != null)
            {
                _inputReader = context.Reader;
                _inputReader.InventoryCloseRequested += TryClosePanelFromInput;
                _inputReader.InteractPressedLocally += ClosePanelFromInteraction;
                _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
                return;
            }
        }

        // Fallback input reader query from player
        PlayerInputReader localReader = GetComponent<PlayerInputReader>() ?? FindAnyObjectByType<PlayerInputReader>();
        if (localReader != null)
        {
            _inputReader = localReader;
            _inputReader.InventoryCloseRequested += TryClosePanelFromInput;
            _inputReader.InteractPressedLocally += ClosePanelFromInteraction;
            _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
        }
    }

    private void ReleaseInputSuppression()
    {
        if (_inputReader != null)
        {
            _inputReader.InventoryCloseRequested -= TryClosePanelFromInput;
            _inputReader.InteractPressedLocally -= ClosePanelFromInteraction;
            _inputReader = null;
        }

        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private bool TryClosePanelFromInput()
    {
        if (_view == null || !_view.IsOpen) return false;
        ClosePanel();
        return true;
    }

    private void ClosePanelFromInteraction()
    {
        if (_view != null && _view.IsOpen)
        {
            ClosePanel();
        }
    }
}

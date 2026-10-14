using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the local Ability Vendor panel after a confirmed interaction with a Town Ability Vendor NPC.
/// Only Input Authority creates the Canvas and manages input. There is no shared stock, so the purchase
/// is not networked: the requesting client's <see cref="LocalProfileStore"/> is the only writer, and the
/// price always comes from <see cref="AbilityVendorCatalog"/>, never from the view. The confirmed store is
/// supplied by the Town profile composition through <see cref="BindProfile"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownAbilityVendorPresenter : NetworkBehaviour
{
    [SerializeField]
    private TownAbilityVendorView _viewPrefab;

    [SerializeField]
    private AbilityVendorCatalog _vendorCatalog;

    [SerializeField]
    private AbilityDefinitionCatalog _abilityCatalog;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;
    [SerializeField] private DialoguePresenter _dialoguePresenter;

    private TownAbilityVendorView _view;
    private NetworkObject _openNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private LocalProfileStore _store;
    private bool _isListening;

    /// <summary>Supplies the confirmed local profile store. Rebinding closes any open panel.</summary>
    public void BindProfile(LocalProfileStore store)
    {
        ClosePanel();
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void UnbindProfile()
    {
        ClosePanel();
        _store = null;
    }

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        Bind();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            Bind();
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority || _view == null || !_view.IsOpen)
        {
            return;
        }

        if (Runner == null || !Runner.IsRunning || _openNpc == null || !_openNpc.IsValid)
        {
            ClosePanel();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unbind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        if (!HasInputAuthority || _interactionController == null || _view != null)
        {
            return;
        }

        if (!TryValidateConfiguration())
        {
            return;
        }

        _view = TownAbilityVendorView.Create(transform, _viewPrefab);
        if (_view == null)
        {
            return;
        }

        _view.BuyRequested += OnBuyRequested;
        _view.CloseRequested += ClosePanel;
        _interactionController.InteractionResolved += OnInteractionResolved;
        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally += OnDialogueCompletedNormally;
        }
    }

    private bool TryValidateConfiguration()
    {
        if (_viewPrefab == null)
        {
            Debug.LogError($"{nameof(TownAbilityVendorPresenter)} failed to bind because the view prefab is not assigned in the Inspector.", this);
            return false;
        }

        if (_vendorCatalog == null)
        {
            Debug.LogError($"{nameof(TownAbilityVendorPresenter)} failed to bind because the vendor catalog is not assigned in the Inspector.", this);
            return false;
        }

        if (_abilityCatalog == null)
        {
            Debug.LogError($"{nameof(TownAbilityVendorPresenter)} failed to bind because the ability catalog is not assigned in the Inspector.", this);
            return false;
        }

        if (!_vendorCatalog.TryValidate(_abilityCatalog, out string error))
        {
            Debug.LogError($"{nameof(TownAbilityVendorPresenter)} failed to bind because the vendor catalog is invalid: {error}", this);
            return false;
        }

        return true;
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null || _view == null || _view.IsOpen)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null)
        {
            // Other interactions (like looting) destroy their target, so it is legitimately missing.
            return;
        }

        if (!target.TryGetBehaviour(out TownAbilityVendorNpcInteractable _))
        {
            return;
        }

        if (target.GetComponentInChildren<IDialogueTrigger>() != null)
        {
            return;
        }

        OpenForTarget(target);
    }

    private void OnDialogueCompletedNormally(EntityId targetId)
    {
        var networkId = new NetworkId { Raw = unchecked((uint)targetId.Value) };
        if (Runner == null || !Runner.IsRunning ||
            !Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out TownAbilityVendorNpcInteractable _) ||
            target.GetComponentInChildren<IDialogueTrigger>() == null)
        {
            return;
        }

        if (_view != null && _view.IsOpen)
        {
            return;
        }

        if (_view != null)
        {
            OpenForTarget(target);
            if (_view.IsOpen)
            {
                return;
            }
        }

        target.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
    }

    private void OpenForTarget(NetworkObject target)
    {
        if (_store == null || !_store.IsAvailable)
        {
            Debug.LogWarning("Ability Vendor is unavailable because the local profile is not bound.", this);
            return;
        }

        _openNpc = target;
        _view.ShowFeedback(string.Empty);
        Refresh();
        _view.Open();
        if (_view.IsOpen)
        {
            target.GetComponentInChildren<TownNpcDirectionalView>()?.FaceTarget(transform.position);
        }

        StartListening();
        AcquireInputSuppression();
    }

    private void OnBuyRequested(AbilityId abilityId)
    {
        if (_store == null || _view == null || !_view.IsOpen)
        {
            return;
        }

        if (!TryFindEntry(abilityId, out AbilityVendorEntry entry))
        {
            Refresh();
            return;
        }

        // The panel may be stale (another source changed the profile); the confirmed state decides.
        if (!entry.CanBuy)
        {
            _view.ShowFeedback(AbilityVendorFeedback.Describe(
                entry.State == AbilityVendorOfferState.AlreadyUnlocked
                    ? AbilityPurchaseResult.AlreadyUnlocked
                    : AbilityPurchaseResult.InsufficientCurrency));
            Refresh();
            return;
        }

        if (!_vendorCatalog.TryGetOffer(abilityId, out AbilityVendorOffer offer))
        {
            Debug.LogError($"Ability Vendor has no authored offer for '{abilityId}'.", this);
            Refresh();
            return;
        }

        AbilityPurchaseResult result = _store.TryPurchaseAbility(abilityId, offer.Price);
        if (result == AbilityPurchaseResult.PersistenceFailed || result == AbilityPurchaseResult.UnknownAbility)
        {
            Debug.LogWarning($"Ability Vendor purchase of '{abilityId}' failed: {result}.", this);
        }

        _view.ShowFeedback(AbilityVendorFeedback.Describe(result));
        Refresh();
    }

    private bool TryFindEntry(AbilityId abilityId, out AbilityVendorEntry entry)
    {
        entry = default;
        if (!TryBuildEntries(out IReadOnlyList<AbilityVendorEntry> entries, out _))
        {
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].AbilityId == abilityId)
            {
                entry = entries[index];
                return true;
            }
        }

        return false;
    }

    private bool TryBuildEntries(
        out IReadOnlyList<AbilityVendorEntry> entries,
        out long currency)
    {
        entries = Array.Empty<AbilityVendorEntry>();
        currency = 0L;
        if (_store == null || !_store.IsAvailable || !_store.TryGetCharacterAttributeState(out CharacterAttributeState attributes))
        {
            return false;
        }

        currency = _store.GetCurrency();
        entries = AbilityVendorBuilder.Build(_vendorCatalog.Offers, _store.GetUnlockedAbilities(), attributes, currency);
        return true;
    }

    private void Refresh()
    {
        if (_view == null)
        {
            return;
        }

        if (!TryBuildEntries(out IReadOnlyList<AbilityVendorEntry> entries, out long currency))
        {
            _view.Present(entries, currency);
            _view.ShowFeedback(AbilityVendorFeedback.Describe(AbilityPurchaseResult.ProfileUnavailable));
            return;
        }

        _view.Present(entries, currency);
    }

    private void StartListening()
    {
        if (_isListening || _store == null)
        {
            return;
        }

        _store.ProfileCommitted += OnProfileCommitted;
        _isListening = true;
    }

    private void StopListening()
    {
        if (!_isListening)
        {
            return;
        }

        if (_store != null)
        {
            _store.ProfileCommitted -= OnProfileCommitted;
        }

        _isListening = false;
    }

    private void OnProfileCommitted(ProfileId profileId) => Refresh();

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null)
        {
            return;
        }

        LocalInputContext context = Runner.GetComponent<LocalInputContext>();
        if (context == null || context.Reader == null)
        {
            return;
        }

        _inputReader = context.Reader;
        _inputReader.InventoryCloseRequested += TryClosePanelFromInput;
        _inputReader.InteractPressedLocally += ClosePanelFromInteraction;
        _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
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
        if (_view == null || !_view.IsOpen)
        {
            return false;
        }

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

    private void ClosePanel()
    {
        StopListening();
        _view?.Close();
        if (_openNpc != null)
        {
            _openNpc.GetComponentInChildren<TownNpcDirectionalView>()?.RestoreInitialFacing();
        }

        _openNpc = null;
        ReleaseInputSuppression();
    }

    private void Unbind()
    {
        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally -= OnDialogueCompletedNormally;
        }

        ClosePanel();

        if (_view != null)
        {
            _view.BuyRequested -= OnBuyRequested;
            _view.CloseRequested -= ClosePanel;
            Destroy(_view.gameObject);
            _view = null;
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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

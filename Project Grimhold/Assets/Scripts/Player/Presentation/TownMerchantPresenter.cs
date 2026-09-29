using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Presents the local merchant shop after a confirmed interaction with a Town Merchant NPC.
/// Only Input Authority creates the Canvas and manages input. Each open session owns one
/// <see cref="TownMerchantTradeSession"/> (draft, preview, confirmation and outcome) and forwards
/// its view model to <see cref="MerchantShopUI"/>. Confirmed-profile dependencies are supplied by
/// the Town profile composition through <see cref="BindProfile"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownMerchantPresenter : NetworkBehaviour
{
    // Unlocks the draft only; accepted reservations are released by outcome reports, never by time.
    private const double SubmissionTimeoutSeconds = 35d;

    [SerializeField]
    private GameObject _merchantShopPrefab;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;
    [SerializeField] private DialoguePresenter _dialoguePresenter;

    private TownMerchantView _view;
    private NetworkObject _openNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private TownMerchantProfileSource _profile;
    private TownMerchantTradeSession _session;

    /// <summary>Supplies the confirmed local profile. Rebinding closes any open session.</summary>
    public void BindProfile(TownMerchantProfileSource profile)
    {
        ClosePanel();
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public void UnbindProfile()
    {
        ClosePanel();
        _profile = null;
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
            return;
        }

        _session?.Tick();
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

        if (_merchantShopPrefab == null)
        {
            Debug.LogError("TownMerchantPresenter failed to bind because MerchantShopPrefab is not assigned in the Inspector.", this);
            return;
        }

        _view = TownMerchantView.Create(transform, _merchantShopPrefab);
        if (_view == null)
        {
            return;
        }

        _interactionController.InteractionResolved += OnInteractionResolved;
        if (_dialoguePresenter != null)
        {
            _dialoguePresenter.DialogueCompletedNormally += OnDialogueCompletedNormally;
        }
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

        if (!target.TryGetBehaviour(out TownMerchantNpcInteractable _))
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
            !target.TryGetBehaviour(out TownMerchantNpcInteractable _) ||
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
        if (!target.TryGetBehaviour(out TownMerchantNetworkController merchant))
        {
            Debug.LogError("Merchant NPC is missing TownMerchantNetworkController.", target);
            return;
        }

        if (_profile == null || _view.ShopUI == null)
        {
            Debug.LogWarning("Merchant is unavailable because the local profile is not bound.", this);
            return;
        }

        OpenSession(merchant, target);
    }

    private void OpenSession(TownMerchantNetworkController merchant, NetworkObject npc)
    {
        TownMerchantProfileSource profile = _profile;
        MerchantShopUI shopUI = _view.ShopUI;

        _session = new TownMerchantTradeSession(
            profile.ProfileId,
            merchant,
            profile.ShopTransactionService,
            profile.Inventory,
            () => profile.Currency.GetCurrency(profile.ProfileId),
            shopUI.Present,
            () => Time.unscaledTimeAsDouble,
            SubmissionTimeoutSeconds);
        _session.TradeCompleted += shopUI.PresentResult;
        shopUI.Bind(_session);
        shopUI.OnCloseRequested.AddListener(ClosePanelFromUI);

        _openNpc = npc;
        _view.Open();
        if (_view.IsOpen)
        {
            npc.GetComponentInChildren<TownNpcDirectionalView>()?.FaceTarget(transform.position);
        }
        AcquireInputSuppression();
    }

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

    private void ClosePanelFromUI()
    {
        ClosePanel();
    }

    /// <summary>Closing discards the draft; the confirmed profile is unchanged by construction.</summary>
    private void ClosePanel()
    {
        _session?.Dispose();
        _session = null;

        if (_view != null && _view.ShopUI != null)
        {
            _view.ShopUI.OnCloseRequested.RemoveListener(ClosePanelFromUI);
            _view.ShopUI.Unbind();
        }

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

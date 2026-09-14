using System;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SocialPlayerIdentity))]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class TownPartyInvitationPresenter : NetworkBehaviour
{
    [SerializeField]
    private SocialPlayerIdentity _identity;

    [SerializeField]
    private LocalInteractionCandidateSource _candidateSource;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    [SerializeField]
    private InteractionHudPresenter _interactionHud;

    [SerializeField]
    private TownPartyInvitationView _view;

    private TownRaidPreparationDirectory _directory;
    private IDisposable _inputSuppression;
    private int _visibleInvitationId;
    private bool _bound;

    private void Awake() => CacheDependencies();

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
        if (!HasInputAuthority || _view == null || _identity == null)
        {
            return;
        }

        EnsureDirectory();
        if (_directory == null ||
            !_directory.TryGetPendingInvitation(new ProfileId(_identity.ProfileId.ToString()), out TownPartyInvitationSnapshot invitation))
        {
            _visibleInvitationId = 0;
            _view.HidePending();
            ReleaseInputSuppression();
            return;
        }

        _visibleInvitationId = invitation.InvitationId;
        float remaining = invitation.ExpiresAt.RemainingTime(Runner) ?? 0f;
        ProfileId localProfile = new(_identity.ProfileId.ToString());
        if (invitation.RecipientProfileId == localProfile)
        {
            _directory.TryGetDisplayName(invitation.InviterProfileId, out string inviterName);
            _view.PresentIncoming(GetDisplayName(inviterName, invitation.InviterProfileId), remaining);
            AcquireInputSuppression();
        }
        else
        {
            _directory.TryGetDisplayName(invitation.RecipientProfileId, out string recipientName);
            _view.PresentOutgoing(GetDisplayName(recipientName, invitation.RecipientProfileId), remaining);
            ReleaseInputSuppression();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Unbind();
    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    private void Bind()
    {
        if (_bound || !HasInputAuthority || _interactionController == null || _candidateSource == null ||
            _interactionHud == null || _view == null)
        {
            return;
        }

        _interactionController.InteractionResolved += OnInteractionResolved;
        _view.Accepted += Accept;
        _view.Rejected += Reject;
        _interactionHud.Bind(_candidateSource, _interactionController, Runner);
        _bound = true;
        EnsureDirectory();
    }

    private void EnsureDirectory()
    {
        TownRaidPreparationDirectory resolved = Runner != null
            ? Runner.GetComponent<TownRaidPreparationDirectoryContext>()?.Directory
            : null;
        if (_directory == resolved)
        {
            return;
        }

        if (_directory != null)
        {
            _directory.InvitationResultReceived -= OnInvitationResult;
        }

        _directory = resolved;
        if (_directory != null)
        {
            _directory.InvitationResultReceived += OnInvitationResult;
        }
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null ||
            !target.TryGetBehaviour(out SocialPlayerInteractable interactable))
        {
            return;
        }

        EnsureDirectory();
        if (_directory == null || !_directory.RequestInvite(interactable.ProfileId))
        {
            _interactionHud.ShowStatusFeedback("Invitación no disponible");
        }
    }

    private void Accept()
    {
        if (_visibleInvitationId > 0)
        {
            _directory?.RequestRespondToInvitation(_visibleInvitationId, true);
        }
    }

    private void Reject()
    {
        if (_visibleInvitationId > 0)
        {
            _directory?.RequestRespondToInvitation(_visibleInvitationId, false);
        }
    }

    private void OnInvitationResult(TownPartyInvitationResultEvent resultEvent)
    {
        _interactionHud?.ShowStatusFeedback(GetResultMessage(resultEvent.Result));
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null)
        {
            return;
        }

        PlayerInputReader reader = Runner.GetComponent<LocalInputContext>()?.Reader;
        if (reader != null)
        {
            _inputSuppression = reader.AcquireGameplayInputSuppression();
        }
    }

    private void ReleaseInputSuppression()
    {
        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private void Unbind()
    {
        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        if (_directory != null)
        {
            _directory.InvitationResultReceived -= OnInvitationResult;
        }

        if (_view != null)
        {
            _view.Accepted -= Accept;
            _view.Rejected -= Reject;
            _view.HidePending();
        }

        _interactionHud?.Unbind();
        ReleaseInputSuppression();
        _directory = null;
        _visibleInvitationId = 0;
        _bound = false;
    }

    private void CacheDependencies()
    {
        if (_identity == null) _identity = GetComponent<SocialPlayerIdentity>();
        if (_candidateSource == null) _candidateSource = GetComponent<LocalInteractionCandidateSource>();
        if (_interactionController == null) _interactionController = GetComponent<PlayerInteractionNetworkController>();
    }

    private static string GetDisplayName(string displayName, ProfileId fallback) =>
        string.IsNullOrWhiteSpace(displayName) ? fallback.Value : displayName;

    private static string GetResultMessage(TownPartyInvitationResult result) => result switch
    {
        TownPartyInvitationResult.Accepted => "Invitación aceptada",
        TownPartyInvitationResult.Rejected => "Invitación rechazada",
        TownPartyInvitationResult.Expired => "La invitación expiró",
        TownPartyInvitationResult.AlreadyGrouped => "El jugador ya está en un grupo",
        TownPartyInvitationResult.PartyFull => "El grupo está completo",
        TownPartyInvitationResult.Busy => "El grupo está ocupado",
        TownPartyInvitationResult.Cooldown => "Esperá antes de volver a invitar",
        _ => "Invitación no disponible"
    };

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}

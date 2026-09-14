using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SocialPlayerIdentity))]
public sealed class TownPartyHudPresenter : NetworkBehaviour
{
    [SerializeField] private SocialPlayerIdentity _identity;
    [SerializeField] private TownPartyHudView _view;

    private TownPartyDirectory _directory;
    private int _presentedPartyId;
    private int _presentedRevision = -1;
    private string _presentedLocalName;
    private string _presentedCompanionName;
    private bool _bound;

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        _view?.SetOwned(HasInputAuthority);
        Bind();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid) Bind();
    }

    public override void Render()
    {
        if (!HasInputAuthority || _identity == null || _view == null) return;
        EnsureDirectory();
        ProfileId local = new(_identity.ProfileId.ToString());
        if (_directory == null || !_directory.TryGetParty(local, out TownPartySnapshot party)) return;
        _directory.TryGetDisplayName(local, out string localName);
        string companionName = null;
        if (party.Members.Count == TownPartyRules.MaxMembers)
        {
            ProfileId companion = party.Members[0] == local ? party.Members[1] : party.Members[0];
            _directory.TryGetDisplayName(companion, out companionName);
        }
        if (_presentedPartyId == party.PartyId && _presentedRevision == party.Revision &&
            _presentedLocalName == localName && _presentedCompanionName == companionName) return;
        if (!TownPartyPresentation.TryCreate(party, local, localName, companionName, out TownPartyPresentation presentation)) return;
        _view.Present(presentation);
        _presentedPartyId = party.PartyId;
        _presentedRevision = party.Revision;
        _presentedLocalName = localName;
        _presentedCompanionName = companionName;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Unbind();
    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    private void Bind()
    {
        if (_bound || !HasInputAuthority || _view == null) return;
        _view.LeaveRequested += LeaveParty;
        _view.SetOwned(true);
        _bound = true;
        EnsureDirectory();
    }

    private void Unbind()
    {
        if (_view != null)
        {
            _view.LeaveRequested -= LeaveParty;
            _view.SetOwned(false);
        }
        _directory = null;
        _bound = false;
        _presentedPartyId = 0;
        _presentedRevision = -1;
        _presentedLocalName = null;
        _presentedCompanionName = null;
    }

    private void EnsureDirectory()
    {
        _directory = Runner != null ? Runner.GetComponent<TownPartyDirectoryContext>()?.Directory : null;
    }

    private void LeaveParty() => _directory?.RequestLeaveParty();

    private void CacheDependencies()
    {
        if (_identity == null) _identity = GetComponent<SocialPlayerIdentity>();
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}

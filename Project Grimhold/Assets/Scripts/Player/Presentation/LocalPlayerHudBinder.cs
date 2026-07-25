using Fusion;
using UnityEngine;

/// <summary>
/// Binds the provisional gameplay HUD exclusively to this peer's Input Authority player.
/// All dependencies are serialized within the network player prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerHudBinder : NetworkBehaviour
{
    [SerializeField]
    private GameObject _hudRoot;

    [SerializeField]
    private InteractionHudPresenter _interactionPresenter;

    [SerializeField]
    private LootHudPresenter _lootPresenter;

    [SerializeField]
    private RaidInventoryPresenter _inventoryPresenter;

    [SerializeField]
    private RaidHudPresenter _raidHudPresenter;

    [SerializeField]
    private LocalInteractionCandidateSource _candidateSource;

    [SerializeField]
    private PlayerInteractionNetworkController _interactionController;

    [SerializeField]
    private PlayerLootReceiver _lootReceiver;

    [SerializeField]
    private PlayerCharacter _playerCharacter;

    [SerializeField]
    private PlayerCombatNetworkController _combatController;

    [SerializeField]
    private PlayerLootTransferNetworkController _lootTransferController;

    private bool _isBound;
    private bool _isPlayerClassResolved;
    private bool _missingJoinContextReported;
    private LocalInputContext _inputContext;
    private LocalPlayerJoinContext _joinContext;
    private NetworkRunner _boundRunner;

    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            SetHudActive(false);
            return;
        }

        BindLocalHud();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid && HasInputAuthority)
        {
            BindLocalHud();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnbindLocalHud();
    }

    public override void Render()
    {
        if (_isBound && !_isPlayerClassResolved && _joinContext != null)
        {
            TryResolvePlayerClass();
        }
    }

    private void OnDisable()
    {
        UnbindLocalHud();
    }

    private void OnDestroy()
    {
        UnbindLocalHud();
    }

    private void BindLocalHud()
    {
        if (_isBound)
        {
            return;
        }

        if (_hudRoot == null || _interactionPresenter == null || _lootPresenter == null ||
            _inventoryPresenter == null || _raidHudPresenter == null ||
            _candidateSource == null || _interactionController == null || _lootReceiver == null ||
            _lootTransferController == null || _playerCharacter == null || _combatController == null)
        {
            Debug.LogError($"{nameof(LocalPlayerHudBinder)} has missing HUD dependencies.", this);
            SetHudActive(false);
            return;
        }

        _boundRunner = Runner;
        if (_boundRunner == null)
        {
            Debug.LogError($"{nameof(LocalPlayerHudBinder)} has no active runner.", this);
            ClearRunnerReferences();
            SetHudActive(false);
            return;
        }

        _inputContext = _boundRunner.GetComponent<LocalInputContext>();
        if (_inputContext == null)
        {
            Debug.LogError($"{nameof(LocalPlayerHudBinder)} could not resolve {nameof(LocalInputContext)}.", this);
            ClearRunnerReferences();
            SetHudActive(false);
            return;
        }

        TryCacheJoinContext();
        SetHudActive(true);
        _interactionPresenter.Bind(_candidateSource, _interactionController);
        _lootPresenter.Bind(_lootReceiver);
        _raidHudPresenter.Bind(_playerCharacter, _combatController, _lootReceiver);
        _inputContext.ReaderChanged += OnInputReaderChanged;
        _isBound = true;

        TryResolvePlayerClass();
        OnInputReaderChanged(_inputContext.Reader);
    }

    private void UnbindLocalHud()
    {
        if (_interactionPresenter != null)
        {
            _interactionPresenter.Unbind();
        }

        if (_lootPresenter != null)
        {
            _lootPresenter.Unbind();
        }

        if (_raidHudPresenter != null)
        {
            _raidHudPresenter.Unbind();
        }

        if (_inputContext != null)
        {
            _inputContext.ReaderChanged -= OnInputReaderChanged;
            _inputContext = null;
        }

        if (_inventoryPresenter != null)
        {
            _inventoryPresenter.Unbind();
        }

        _isBound = false;
        _isPlayerClassResolved = false;
        _missingJoinContextReported = false;
        ClearRunnerReferences();
        SetHudActive(false);
    }

    private void OnInputReaderChanged(PlayerInputReader inputReader)
    {
        if (!_isBound || _inventoryPresenter == null)
        {
            return;
        }

        if (!_isPlayerClassResolved && _joinContext == null)
        {
            TryCacheJoinContext();
            TryResolvePlayerClass();
        }

        _inventoryPresenter.Unbind();
        if (inputReader != null)
        {
            _inventoryPresenter.Bind(
                _lootReceiver,
                inputReader,
                _interactionController,
                _lootTransferController,
                Runner,
                transform);
        }
    }

    private void TryCacheJoinContext()
    {
        if (_joinContext != null || _boundRunner == null)
        {
            return;
        }

        _joinContext = _boundRunner.GetComponent<LocalPlayerJoinContext>();
        if (_joinContext == null && !_missingJoinContextReported)
        {
            Debug.LogError(
                $"{nameof(LocalPlayerHudBinder)} could not resolve {nameof(LocalPlayerJoinContext)}. Class presentation will remain unavailable.",
                this);
            _missingJoinContextReported = true;
        }
    }

    private void TryResolvePlayerClass()
    {
        if (_isPlayerClassResolved || _joinContext == null || _raidHudPresenter == null)
        {
            return;
        }

        PlayerClassId playerClass = _joinContext.JoinData.ClassId;
        if (!PlayerJoinDataCodec.IsSupported(playerClass))
        {
            return;
        }

        _raidHudPresenter.SetPlayerClass(playerClass);
        _isPlayerClassResolved = true;
    }

    private void ClearRunnerReferences()
    {
        _joinContext = null;
        _boundRunner = null;
    }

    private void SetHudActive(bool active)
    {
        if (_hudRoot != null && _hudRoot.activeSelf != active)
        {
            _hudRoot.SetActive(active);
        }
    }
}

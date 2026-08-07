using Fusion;
using UnityEngine;

/// <summary>
/// Binds the local gameplay HUD exclusively to this peer's Input Authority player.
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
    private RaidMinimapPresenter _raidMinimapPresenter;

    [SerializeField]
    private CombatFeedbackPresenter _combatFeedbackPresenter;

    [SerializeField]
    private RaidMenuPresenter _menuPresenter;

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
    private PlayerExtractionController _extractionController;

    [SerializeField]
    private PlayerExtractionProgressController _extractionProgressController;

    [SerializeField]
    private PlayerLootTransferNetworkController _lootTransferController;

    [SerializeField]
    private PlayerLootDropNetworkController _lootDropController;

    [SerializeField]
    private PlayerConsumableNetworkController _consumableController;

    private bool _isBound;
    private bool _isPlayerClassResolved;
    private bool _missingJoinContextReported;
    private LocalInputContext _inputContext;
    private LocalPlayerJoinContext _joinContext;
    private NetworkRunner _boundRunner;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private EntityRegistry _entityRegistry;

    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            SetHudActive(false);
            return;
        }

        BindLocalHud();
    }

    public void TryBindAsLocalPlayer()
    {
        if (!HasInputAuthority)
        {
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
            _inventoryPresenter == null || _raidHudPresenter == null || _combatFeedbackPresenter == null ||
            _candidateSource == null || _interactionController == null || _lootReceiver == null ||
            _lootTransferController == null || _lootDropController == null || _consumableController == null ||
            _playerCharacter == null || _combatController == null || _extractionController == null)
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

        _assignmentService = _boundRunner.GetComponent<ExtractionSanctuaryAssignmentService>();
        _entityRegistry = _boundRunner.GetComponent<EntityRegistry>();

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
        _interactionPresenter.Bind(_candidateSource, _interactionController, Runner);
        _lootPresenter.Bind(_lootReceiver);
        _raidHudPresenter.Bind(
            _playerCharacter,
            _combatController,
            _lootReceiver,
            _extractionController,
            _extractionProgressController,
            _assignmentService,
            _entityRegistry);
        if (_raidMinimapPresenter != null)
        {
            _raidMinimapPresenter.Bind(
                _boundRunner,
                Object,
                transform,
                _extractionController,
                _assignmentService,
                _entityRegistry);
        }
        _combatFeedbackPresenter.Bind(_combatController, _playerCharacter);
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

        if (_raidMinimapPresenter != null)
        {
            _raidMinimapPresenter.Unbind();
        }

        if (_combatFeedbackPresenter != null)
        {
            _combatFeedbackPresenter.Unbind();
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

        if (_menuPresenter != null)
        {
            _menuPresenter.Unbind();
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
        if (_menuPresenter != null)
        {
            _menuPresenter.Unbind();
        }

        if (inputReader != null)
        {
            _inventoryPresenter.Bind(
                _lootReceiver,
                inputReader,
                _interactionController,
                _lootTransferController,
                _lootDropController,
                _consumableController,
                Runner,
                transform);

            if (_menuPresenter != null)
            {
                _menuPresenter.Bind(
                    _playerCharacter,
                    inputReader,
                    _inventoryPresenter,
                    Runner);
            }
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
        _assignmentService = null;
        _entityRegistry = null;
    }

    private void SetHudActive(bool active)
    {
        if (_hudRoot != null && _hudRoot.activeSelf != active)
        {
            _hudRoot.SetActive(active);
        }
    }
}

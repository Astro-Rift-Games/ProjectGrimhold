using Fusion;
using UnityEngine;

/// <summary>
/// Owns the authoritative raised-shield state and evaluates directional shield mitigation.
/// Equipment remains the source of truth for which shield is available.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-8)]
public sealed class PlayerShieldDefenseNetworkController : NetworkBehaviour
{
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentController;
    [SerializeField] private MonoBehaviour _characterSource;
    [SerializeField] private MonoBehaviour _movementStateSource;

    private ICharacter _character;
    private IMovementState _movementState;
    private NetworkMatchController _matchController;
    private bool _dependenciesValid;

    [Networked]
    public NetworkBool IsDefending { get; private set; }

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        _matchController = Runner.GetComponent<NetworkMatchController>();

        if (HasStateAuthority &&
            (!HostMigrationRestoreUtility.IsRestoreSpawn(this) || !CanSustainDefense()))
        {
            IsDefending = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_dependenciesValid)
        {
            return;
        }

        IsDefending = GetInput(out PlayerNetworkInput input) && CanDefend(input.Buttons);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _matchController = null;
        _dependenciesValid = false;
    }

    /// <summary>Checks the current secondary-action intent against authoritative gameplay state.</summary>
    public bool CanDefend(NetworkButtons buttons) =>
        buttons.IsSet(PlayerInputButton.SecondaryAction) && CanSustainDefense();

    /// <summary>Applies the active shield's directional mitigation without changing the damage contract.</summary>
    public bool TryMitigateDamage(
        in DamageRequest request,
        float incomingDamage,
        out float mitigatedDamage)
    {
        mitigatedDamage = incomingDamage;
        if (Object == null || !Object.IsValid || !IsDefending ||
            request.DamageType == DamageType.TrueDamage ||
            !_equipmentController.TryGetActiveShieldDefinition(out ShieldDefinition shield))
        {
            return false;
        }

        return ShieldDefenseMath.TryMitigate(
            incomingDamage,
            shield.DamageReduction,
            shield.DefensiveConeDegrees,
            _movementState.FacingDirection,
            request.Direction,
            out mitigatedDamage);
    }

    private bool CanSustainDefense()
    {
        bool gameplayPhaseActive = _matchController == null ||
            _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        return _dependenciesValid && gameplayPhaseActive && _character.IsAlive &&
            _equipmentController.TryGetActiveShieldDefinition(out _);
    }

    private void CacheDependencies()
    {
        _equipmentController ??= GetComponent<PlayerWeaponEquipmentNetworkController>();
        if (_characterSource == null)
        {
            _characterSource = GetComponent<PlayerCharacter>();
        }

        if (_movementStateSource == null)
        {
            _movementStateSource = GetComponent<PlayerMovementNetworkController>();
        }

        _character = _characterSource as ICharacter;
        _movementState = _movementStateSource as IMovementState;
    }

    private bool ValidateDependencies()
    {
        if (_equipmentController != null && _character != null && _movementState != null)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerShieldDefenseNetworkController)} requires Equipment, character and movement state dependencies.",
            this);
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}

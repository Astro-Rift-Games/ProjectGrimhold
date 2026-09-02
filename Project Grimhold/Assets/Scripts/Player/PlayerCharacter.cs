using UnityEngine;
using Fusion;

/// <summary>
/// Concrete network character implementation used by player-controlled entities.
/// Character identity, health and damage behavior are inherited from CharacterBase.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCharacter : CharacterBase
{
    [SerializeField]
    private PlayerCorpseGenerationController _corpseGenerationController;

    [SerializeField]
    private PlayerExtractionController _extractionController;

    [SerializeField]
    private RaidAvatarParticipantLink _participantLink;
    private bool _reportedMissingExtractionController;
    private bool _hasCachedMaximumHealth;
    private float _cachedMaximumHealth;
    private bool _reportedInvalidDerivedStatistics;

    [Networked]
    public NetworkString<_32> ProfileIdString { get; set; }

    /// <summary>
    /// Indicates whether the player character can receive damage.
    /// Returns false if the player is alive and in Extracted process state.
    /// </summary>
    public override bool CanReceiveDamage
    {
        get
        {
            if (Object == null || !Object.IsValid)
            {
                return base.CanReceiveDamage;
            }

            if (_extractionController == null)
            {
                if (!_reportedMissingExtractionController)
                {
                    Debug.LogWarning(
                        $"{nameof(PlayerCharacter)}: {nameof(PlayerExtractionController)} composition missing on object {name}.",
                        this);
                    _reportedMissingExtractionController = true;
                }

                return base.CanReceiveDamage;
            }

            if (_extractionController.State == ExtractionState.Extracted)
            {
                return false;
            }

            return base.CanReceiveDamage;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        CacheDependencies();
    }

    /// <summary>
    /// Derives the participant's effective maximum Health from the frozen Raid attributes.
    /// A temporarily unresolved participant link keeps the prefab fallback available without
    /// caching it, so Host Migration remapping can resolve the authoritative snapshot later.
    /// </summary>
    protected override float ResolveMaximumHealth()
    {
        if (_hasCachedMaximumHealth)
        {
            return _cachedMaximumHealth;
        }

        if (_participantLink == null ||
            !_participantLink.TryGetCharacterAttributeState(out CharacterAttributeState attributes))
        {
            return base.ResolveMaximumHealth();
        }

        if (!CharacterDerivedStatisticsCalculator.TryCalculate(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                out CharacterDerivedStatistics statistics,
                out CharacterDerivedStatisticsCalculationFailure failure))
        {
            if (!_reportedInvalidDerivedStatistics)
            {
                Debug.LogError(
                    $"{nameof(PlayerCharacter)} could not derive maximum Health from the admitted " +
                    $"character attributes. Failure={failure}.",
                    this);
                _reportedInvalidDerivedStatistics = true;
            }

            return base.ResolveMaximumHealth();
        }

        _cachedMaximumHealth = statistics.MaximumHealth;
        _hasCachedMaximumHealth = true;
        return _cachedMaximumHealth;
    }

    /// <summary>
    /// Starts the authoritative player-corpse transaction after this character's
    /// health has transitioned to zero through the shared damage pipeline.
    /// </summary>
    protected override void HandleDeath()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_corpseGenerationController == null)
        {
            Debug.LogError(
                $"{nameof(PlayerCharacter)} requires {nameof(PlayerCorpseGenerationController)} on the player object.",
                this);
            return;
        }

        bool corpseReady = _corpseGenerationController.TryConvertInventoryToCorpseLoot(Runner.Tick);
        if (corpseReady)
        {
            _participantLink?.NotifyCorpseConversionCompleted();
        }

        var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        spawnManager?.NotifyPendingReconnectCharacterDefeated(Object);
    }

    private void CacheDependencies()
    {
        if (_corpseGenerationController == null)
        {
            _corpseGenerationController = GetComponent<PlayerCorpseGenerationController>();
        }

        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }

        if (_participantLink == null)
        {
            _participantLink = GetComponent<RaidAvatarParticipantLink>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

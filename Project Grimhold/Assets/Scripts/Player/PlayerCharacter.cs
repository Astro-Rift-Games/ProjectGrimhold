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
    private bool _reportedMissingExtractionController;

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

        _corpseGenerationController.TryConvertInventoryToCorpseLoot(Runner.Tick);
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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

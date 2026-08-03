using UnityEngine;

/// <summary>
/// Immutable, data-driven configuration storing shared parameters for extraction zones and simulation.
///
/// This component belongs purely to the static configuration layer. It does not maintain runtime state,
/// does not inherit from NetworkBehaviour, has no Fusion dependencies, and does not execute simulation logic.
///
/// Time conversion rule:
/// <see cref="CountdownDurationSeconds"/> stores stable configuration duration in seconds.
/// Converting this value into network simulation time or a Fusion <c>TickTimer</c> is explicitly delegated
/// to the tick-driven simulation in TASK-28.
///
/// Spatial rule:
/// Zone components own concrete <c>Collider2D</c> geometry. The participant controller applies
/// <see cref="BoundaryTolerance"/> only while revalidating an active process.
/// </summary>
/// <remarks>
/// See <c>Docs/Architecture/ExtractionArchitecture.md</c> for details regarding sources of truth,
/// State Authority ownership, and subsystem boundaries.
/// </remarks>
[CreateAssetMenu(fileName = "ExtractionConfig", menuName = "Grimhold/Config/ExtractionConfig")]
public sealed class ExtractionConfig : ScriptableObject
{
    [SerializeField, Min(0.001f)]
    private float _countdownDurationSeconds = 5f;

    [SerializeField]
    private bool _cancelWhenLeavingArea = true;

    [SerializeField, Min(0f)]
    private float _boundaryTolerance = 0.5f;

    [SerializeField]
    private bool _requireAliveToStart = true;

    [SerializeField]
    private bool _cancelWhenNotAlive = true;

    [Header("Individual Progress")]
    [SerializeField, Min(1)]
    private int _progressQuota = 100;

    /// <summary>
    /// Countdown duration in seconds required for a player to complete extraction.
    /// Must be finite and strictly greater than zero.
    /// Conversion to network simulation time/ticks is handled by simulation in TASK-28.
    /// </summary>
    public float CountdownDurationSeconds => _countdownDurationSeconds;

    /// <summary>
    /// Whether the extraction countdown cancels automatically when the player leaves the zone boundary.
    /// </summary>
    public bool CancelWhenLeavingArea => _cancelWhenLeavingArea;

    /// <summary>
    /// Non-negative tolerance applied outward from the concrete zone collider during continuation checks.
    /// Must be finite and greater than or equal to zero.
    /// </summary>
    public float BoundaryTolerance => _boundaryTolerance;

    /// <summary>
    /// Whether the player must be alive (according to <c>ICharacter.IsAlive</c>) to initiate extraction.
    /// </summary>
    public bool RequireAliveToStart => _requireAliveToStart;

    /// <summary>
    /// Whether the extraction process cancels automatically if the player ceases to be alive during countdown.
    /// </summary>
    public bool CancelWhenNotAlive => _cancelWhenNotAlive;

    /// <summary>
    /// Positive individual progress required before sanctuary assignment can be requested.
    /// This static value is shared configuration and is not replicated.
    /// </summary>
    public int ProgressQuota => _progressQuota;

    /// <summary>
    /// Validates that configuration properties contain valid, finite, and non-negative values.
    /// </summary>
    /// <param name="error">Outputs a descriptive error message when validation fails; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when configuration is valid; otherwise, <see langword="false"/>.</returns>
    public bool TryValidate(out string error)
    {
        error = null;

        if (float.IsNaN(_countdownDurationSeconds) || float.IsInfinity(_countdownDurationSeconds) || _countdownDurationSeconds <= 0f)
        {
            error = $"{nameof(ExtractionConfig)}: {nameof(CountdownDurationSeconds)} must be a finite number strictly greater than zero.";
            return false;
        }

        if (float.IsNaN(_boundaryTolerance) || float.IsInfinity(_boundaryTolerance) || _boundaryTolerance < 0f)
        {
            error = $"{nameof(ExtractionConfig)}: {nameof(BoundaryTolerance)} must be a finite non-negative number.";
            return false;
        }

        if (_progressQuota <= 0)
        {
            error = $"{nameof(ExtractionConfig)}: {nameof(ProgressQuota)} must be strictly greater than zero.";
            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        if (float.IsNaN(_countdownDurationSeconds) || float.IsInfinity(_countdownDurationSeconds) || _countdownDurationSeconds <= 0f)
        {
            _countdownDurationSeconds = Mathf.Max(0.001f, float.IsNaN(_countdownDurationSeconds) || float.IsInfinity(_countdownDurationSeconds) ? 5f : _countdownDurationSeconds);
        }

        if (float.IsNaN(_boundaryTolerance) || float.IsInfinity(_boundaryTolerance) || _boundaryTolerance < 0f)
        {
            _boundaryTolerance = Mathf.Max(0f, float.IsNaN(_boundaryTolerance) || float.IsInfinity(_boundaryTolerance) ? 0f : _boundaryTolerance);
        }

        _progressQuota = Mathf.Max(1, _progressQuota);

        if (!TryValidate(out string validationError))
        {
            Debug.LogWarning(validationError, this);
        }
    }
}

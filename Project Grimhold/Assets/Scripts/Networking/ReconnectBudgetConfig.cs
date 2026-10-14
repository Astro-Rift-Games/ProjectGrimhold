using UnityEngine;

/// <summary>
/// Immutable, data-driven configuration storing shared parameters for reconnection budget.
/// </summary>
[CreateAssetMenu(fileName = "ReconnectBudgetConfig", menuName = "Grimhold/Config/ReconnectBudgetConfig")]
public sealed class ReconnectBudgetConfig : ScriptableObject
{
    [SerializeField, Min(0f)]
    private float _durationSeconds = -1f;

    /// <summary>
    /// Reconnection budget duration in seconds.
    /// Conversion to network simulation ticks is handled by ReconnectBudgetRules.
    /// </summary>
    public float DurationSeconds => _durationSeconds;

    /// <summary>
    /// Validates that configuration properties contain valid values.
    /// </summary>
    /// <param name="error">Outputs a descriptive error message when validation fails; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when configuration is valid; otherwise, <see langword="false"/>.</returns>
    public bool TryValidate(out string error)
    {
        error = null;
        if (float.IsNaN(_durationSeconds) || float.IsInfinity(_durationSeconds) || _durationSeconds <= 0f)
        {
            error = $"{nameof(ReconnectBudgetConfig)}: {nameof(DurationSeconds)} must be a finite number strictly greater than zero.";
            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        if (float.IsNaN(_durationSeconds) || float.IsInfinity(_durationSeconds) || _durationSeconds < 0f)
        {
            _durationSeconds = -1f;
        }

        if (!TryValidate(out string validationError))
        {
            Debug.LogWarning(validationError, this);
        }
    }
}

using System;

/// <summary>Pure lifecycle binding from one confirmed local profile to Town attribute presentation.</summary>
public sealed class TownAttributeAssignmentBinding : IDisposable
{
    public delegate bool TryReadState(out CharacterAttributeState state);

    /// <summary>Builds the character sheet for the attributes the binding just read.</summary>
    public delegate bool TryBuildStatistics(
        in CharacterAttributeState state,
        out TownCharacterStatisticsPresentation statistics);

    private readonly ProfileId _observedProfileId;
    private readonly int _maximumAttributeValue;
    private readonly TryReadState _readState;
    private readonly Action<Action<ProfileId>> _unsubscribe;
    private readonly Action<TownAttributeAssignmentPresentation> _present;
    private readonly Action _presentUnavailable;
    private readonly TryBuildStatistics _buildStatistics;
    private readonly Action<TownCharacterStatisticsPresentation> _presentStatistics;
    private readonly Action _presentStatisticsUnavailable;
    private readonly Action<ProfileId> _changeHandler;
    private bool _disposed;

    /// <param name="buildStatistics">
    /// Optional. When given, <paramref name="presentStatistics"/> and
    /// <paramref name="presentStatisticsUnavailable"/> are required, and the sheet is rebuilt on every
    /// refresh together with the attributes.
    /// </param>
    public TownAttributeAssignmentBinding(
        ProfileId observedProfileId,
        int maximumAttributeValue,
        TryReadState readState,
        Action<Action<ProfileId>> subscribe,
        Action<Action<ProfileId>> unsubscribe,
        Action<TownAttributeAssignmentPresentation> present,
        Action presentUnavailable,
        TryBuildStatistics buildStatistics = null,
        Action<TownCharacterStatisticsPresentation> presentStatistics = null,
        Action presentStatisticsUnavailable = null)
    {
        if (buildStatistics != null && (presentStatistics == null || presentStatisticsUnavailable == null))
        {
            throw new ArgumentException(
                "Statistics presentation callbacks are required with a statistics builder.",
                nameof(buildStatistics));
        }

        _buildStatistics = buildStatistics;
        _presentStatistics = presentStatistics;
        _presentStatisticsUnavailable = presentStatisticsUnavailable;

        if (!observedProfileId.IsValid)
        {
            throw new ArgumentException("The observed profile must be valid.", nameof(observedProfileId));
        }

        _observedProfileId = observedProfileId;
        _maximumAttributeValue = maximumAttributeValue;
        _readState = readState ?? throw new ArgumentNullException(nameof(readState));
        _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _presentUnavailable = presentUnavailable ?? throw new ArgumentNullException(nameof(presentUnavailable));
        _changeHandler = OnProfileCommitted;

        if (subscribe == null)
        {
            throw new ArgumentNullException(nameof(subscribe));
        }

        subscribe(_changeHandler);
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _unsubscribe(_changeHandler);
    }

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (_disposed || profileId != _observedProfileId)
        {
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        if (!_readState(out CharacterAttributeState state) ||
            !TownAttributeAssignmentPresentation.TryCreate(
                state,
                _maximumAttributeValue,
                out TownAttributeAssignmentPresentation presentation))
        {
            _presentUnavailable();
            _presentStatisticsUnavailable?.Invoke();
            return;
        }

        _present(presentation);
        if (_buildStatistics == null)
        {
            return;
        }

        if (_buildStatistics(state, out TownCharacterStatisticsPresentation statistics))
        {
            _presentStatistics(statistics);
        }
        else
        {
            _presentStatisticsUnavailable();
        }
    }
}

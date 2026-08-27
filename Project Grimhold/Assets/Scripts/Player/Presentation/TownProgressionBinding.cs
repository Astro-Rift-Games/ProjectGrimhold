using System;

/// <summary>
/// Pure C# lifecycle binding between one local profile notification source and Town presentation.
/// </summary>
public sealed class TownProgressionBinding : IDisposable
{
    private readonly ProfileId _observedProfileId;
    private readonly ExperienceCurve _curve;
    private readonly Func<(int Level, long Experience)> _readState;
    private readonly Action<Action<ProfileId>> _unsubscribe;
    private readonly Action<TownProgressionPresentation> _present;
    private readonly Action _presentUnavailable;
    private readonly Action<ProfileId> _changeHandler;
    private bool _disposed;

    public TownProgressionBinding(
        ProfileId observedProfileId,
        ExperienceCurve curve,
        Func<(int Level, long Experience)> readState,
        Action<Action<ProfileId>> subscribe,
        Action<Action<ProfileId>> unsubscribe,
        Action<TownProgressionPresentation> present,
        Action presentUnavailable)
    {
        if (!observedProfileId.IsValid)
        {
            throw new ArgumentException("The observed profile must be valid.", nameof(observedProfileId));
        }

        _observedProfileId = observedProfileId;
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
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

        (int level, long experience) = _readState();
        if (!TownProgressionPresentation.TryCreate(
                _curve,
                level,
                experience,
                out TownProgressionPresentation presentation))
        {
            _presentUnavailable();
            return;
        }

        _present(presentation);
    }
}

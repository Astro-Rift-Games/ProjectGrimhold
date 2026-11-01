/// <summary>
/// Adapts the replicated Downed state and recovery session of one player to
/// <see cref="IDownedPostProcessSource"/>. Reads only; never writes networked state.
/// </summary>
public sealed class PlayerDownedPostProcessSource : IDownedPostProcessSource
{
    private readonly PlayerCharacter _character;
    private readonly PlayerDownedStateNetworkController _downedState;
    private readonly PlayerDownedRecoveryNetworkController _recovery;

    public PlayerDownedPostProcessSource(
        PlayerCharacter character,
        PlayerDownedStateNetworkController downedState,
        PlayerDownedRecoveryNetworkController recovery)
    {
        _character = character;
        _downedState = downedState;
        _recovery = recovery;
    }

    public bool IsAvailable =>
        _character != null && _character.Object != null && _character.Object.IsValid &&
        _downedState != null && _recovery != null;

    public bool IsDowned => IsAvailable && _character.IsDowned;

    public float ReserveFraction => IsAvailable
        ? DownedRecoveryFeedbackRules.ReserveFraction(_downedState.DownedHealth, _downedState.InitialDownedHealth)
        : 0f;

    public bool TryGetReviveProgress(out float progress)
    {
        progress = 0f;
        return IsAvailable && _recovery.TryGetProgress(out progress);
    }
}

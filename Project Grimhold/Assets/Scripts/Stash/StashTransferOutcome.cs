/// <summary>The result of one Stash action: how it ended, the local rule that decided it, and any remote error.</summary>
public readonly struct StashTransferOutcome
{
    public StashTransferStatus Status { get; }

    /// <summary>The local rule result behind a <see cref="StashTransferStatus.Rejected"/> or <see cref="StashTransferStatus.Diverged"/> outcome.</summary>
    public StashOperationResult Rule { get; }

    /// <summary>The backend's message for a <see cref="StashTransferStatus.RemoteFailed"/> outcome.</summary>
    public string RemoteError { get; }

    public bool IsSuccess => Status == StashTransferStatus.Success;

    private StashTransferOutcome(StashTransferStatus status, StashOperationResult rule, string remoteError)
    {
        Status = status;
        Rule = rule;
        RemoteError = remoteError;
    }

    public static StashTransferOutcome Success() =>
        new(StashTransferStatus.Success, StashOperationResult.Success, null);

    public static StashTransferOutcome Rejected(StashOperationResult rule) =>
        new(StashTransferStatus.Rejected, rule, null);

    public static StashTransferOutcome Blocked() =>
        new(StashTransferStatus.Blocked, StashOperationResult.Success, null);

    public static StashTransferOutcome RemoteFailed(string remoteError) =>
        new(StashTransferStatus.RemoteFailed, StashOperationResult.Success, remoteError);

    public static StashTransferOutcome Diverged(StashOperationResult rule) =>
        new(StashTransferStatus.Diverged, rule, null);

    public static StashTransferOutcome Unavailable() =>
        new(StashTransferStatus.Unavailable, StashOperationResult.Success, null);

    public override string ToString() => Status switch
    {
        StashTransferStatus.Rejected or StashTransferStatus.Diverged => $"{Status} ({Rule})",
        StashTransferStatus.RemoteFailed => $"{Status} ({RemoteError})",
        _ => Status.ToString()
    };
}

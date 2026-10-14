/// <summary>The result of one Stash action: how it ended, the local rule that decided it, and any remote error.</summary>
public readonly struct StashTransferOutcome
{
    public StashTransferStatus Status { get; }

    /// <summary>The local rule behind a rejection, divergence or recovery failure; bulk summaries retain the first failure's rule.</summary>
    public StashOperationResult Rule { get; }

    /// <summary>The original backend message; bulk summaries retain the first failure's message.</summary>
    public string RemoteError { get; }

    /// <summary>Confirmed successful transfer attempts in a bulk sequence, not inferred moved stacks.</summary>
    public int Completed { get; }

    /// <summary>Unconfirmed or not-attempted source stacks, including ownership lost after reconciliation.</summary>
    public int Failed { get; }

    /// <summary>The original backend error code, retained even when recovery fails.</summary>
    public string RemoteErrorCode { get; }

    /// <summary>Recovery failure is distinct from the original mutation error.</summary>
    public string RecoveryErrorCode => Status == StashTransferStatus.RecoveryFailed ? "RECONCILIATION_FAILED" : null;

    public bool IsSuccess => Status == StashTransferStatus.Success;

    private StashTransferOutcome(
        StashTransferStatus status,
        StashOperationResult rule,
        string remoteError,
        int completed = 0,
        int failed = 0,
        string remoteErrorCode = null)
    {
        Status = status;
        Rule = rule;
        RemoteError = remoteError;
        Completed = completed;
        Failed = failed;
        RemoteErrorCode = remoteErrorCode;
    }

    public static StashTransferOutcome Success(int completed = 0) =>
        new(StashTransferStatus.Success, StashOperationResult.Success, null, completed);

    public StashTransferOutcome WithCounts(int completed, int failed) =>
        new(Status, Rule, RemoteError, completed, failed, RemoteErrorCode);

    public static StashTransferOutcome Rejected(StashOperationResult rule) =>
        new(StashTransferStatus.Rejected, rule, null);

    public static StashTransferOutcome Blocked() =>
        new(StashTransferStatus.Blocked, StashOperationResult.Success, null);

    public static StashTransferOutcome RemoteFailed(string remoteError, string remoteErrorCode = null) =>
        new(StashTransferStatus.RemoteFailed, StashOperationResult.Success, remoteError, remoteErrorCode: remoteErrorCode);

    public static StashTransferOutcome RecoveryFailed(
        StashOperationResult rule, string remoteError = null, string remoteErrorCode = null,
        int completed = 0, int failed = 0) =>
        new(StashTransferStatus.RecoveryFailed, rule, remoteError, completed, failed, remoteErrorCode);

    public static StashTransferOutcome Diverged(StashOperationResult rule) =>
        new(StashTransferStatus.Diverged, rule, null);

    public static StashTransferOutcome Unavailable() =>
        new(StashTransferStatus.Unavailable, StashOperationResult.Success, null);

    public static StashTransferOutcome Partial(
        int completed, int failed, string remoteError = null, string remoteErrorCode = null,
        StashOperationResult rule = StashOperationResult.Success) =>
        new(StashTransferStatus.Partial, rule, remoteError, completed, failed, remoteErrorCode);

    public override string ToString() => Status switch
    {
        StashTransferStatus.Rejected or StashTransferStatus.Diverged => $"{Status} ({Rule})",
        StashTransferStatus.RemoteFailed => $"{Status} ({RemoteErrorCode}: {RemoteError})",
        StashTransferStatus.RecoveryFailed =>
            $"{Status} ({RecoveryErrorCode}; {Rule}; {RemoteErrorCode}: {RemoteError}; {Completed} confirmed, {Failed} unconfirmed/not attempted)",
        StashTransferStatus.Partial => $"{Status} ({Completed} confirmed, {Failed} unconfirmed/not attempted)",
        _ => Status.ToString()
    };
}

/// <summary>
/// One-shot holder of the loss notice that Town shows after the Raid runner is gone. The first
/// capture of a departure wins, so repeated shutdown callbacks never replace or duplicate it.
/// </summary>
public sealed class RaidLossNoticeSlot
{
    private RaidLossNotice _pending;

    public bool HasPending => _pending != null;

    public bool Capture(RaidLossNotice notice)
    {
        if (notice == null || _pending != null)
        {
            return false;
        }

        _pending = notice;
        return true;
    }

    public bool TryConsume(out RaidLossNotice notice)
    {
        notice = _pending;
        _pending = null;
        return notice != null;
    }
}

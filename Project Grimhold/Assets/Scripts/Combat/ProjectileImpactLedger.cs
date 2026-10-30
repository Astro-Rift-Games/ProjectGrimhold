/// <summary>
/// Fixed-capacity per-projectile re-impact memory. Pure C# working copy of the projectile's
/// networked arrays: entries are (target EntityId value, last contact tick); id 0 is a free slot.
/// </summary>
public sealed class ProjectileImpactLedger
{
    public const int Capacity = 8;

    private readonly int[] _targetIds = new int[Capacity];
    private readonly int[] _ticks = new int[Capacity];

    public int GetTargetId(int index) => _targetIds[index];
    public int GetTick(int index) => _ticks[index];

    public void SetEntry(int index, int targetId, int tick)
    {
        _targetIds[index] = targetId;
        _ticks[index] = tick;
    }

    public void Clear()
    {
        System.Array.Clear(_targetIds, 0, Capacity);
        System.Array.Clear(_ticks, 0, Capacity);
    }

    /// <summary>Once per target: records the first impact. A full ledger fails closed (no impact).</summary>
    public bool TryRecordFirstImpact(int targetId, int tick)
    {
        int free = -1;
        for (int i = 0; i < Capacity; i++)
        {
            if (_targetIds[i] == targetId) return false;
            if (free < 0 && _targetIds[i] == 0) free = i;
        }

        if (free < 0) return false;
        SetEntry(free, targetId, tick);
        return true;
    }

    /// <summary>
    /// Once per contact: an entry seen on the previous or current event is the same continuous contact.
    /// Older entries mean the target left contact and may be impacted again or have their slot reused.
    /// A full ledger of continuous contacts fails closed (no impact).
    /// </summary>
    public bool TryBeginContact(int targetId, int tick)
    {
        int free = -1;
        for (int i = 0; i < Capacity; i++)
        {
            bool continuous = _targetIds[i] != 0 && _ticks[i] >= tick - 1;
            if (_targetIds[i] == targetId)
            {
                _ticks[i] = tick;
                return !continuous;
            }

            if (free < 0 && !continuous) free = i;
        }

        if (free < 0) return false;
        SetEntry(free, targetId, tick);
        return true;
    }
}

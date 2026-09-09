using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Read-only projection of one confirmed application-level Loadout.
/// The application services remain the source of truth; this object owns only a reusable
/// presentation snapshot and its subscription to profile commits.
/// </summary>
public sealed class LocalLoadoutInventoryReadSource : IInventoryReadSource, IPreparedEquipmentReadSource, IDisposable
{
    private readonly ApplicationStashContext _context;
    private readonly IPlayerLoadoutService _loadoutService;
    private readonly ProfileId _profileId;
    private readonly List<LootEntry> _snapshot = new();
    private readonly ReadOnlyCollection<LootEntry> _readOnlySnapshot;
    private bool _disposed;

    public LocalLoadoutInventoryReadSource(
        ApplicationStashContext context,
        ProfileId profileId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loadoutService = context.LoadoutService ??
            throw new ArgumentException("The application context has no Loadout service.", nameof(context));
        if (!profileId.IsValid)
        {
            throw new ArgumentException("The observed profile must be valid.", nameof(profileId));
        }

        _profileId = profileId;
        _readOnlySnapshot = _snapshot.AsReadOnly();
        _context.ProfileCommitted += OnProfileCommitted;
    }

    public int SlotCapacity => LocalProfileSnapshot.MaxLoadoutSlots;
    public int Revision { get; private set; }

    public event Action Changed;

    public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
    {
        content = null;
        if (_disposed)
        {
            return false;
        }

        IReadOnlyList<StashItem> loadout = _loadoutService.GetLoadout(_profileId);
        if (loadout == null || loadout.Count > SlotCapacity)
        {
            return false;
        }

        _snapshot.Clear();
        for (int index = 0; index < loadout.Count; index++)
        {
            StashItem item = loadout[index];
            if (!item.IsValid)
            {
                _snapshot.Clear();
                return false;
            }

            _snapshot.Add(new LootEntry(item.LootId, item.Amount));
        }

        content = _readOnlySnapshot;
        return true;
    }

    public bool TryGetPreparedEquipment(out PreparedEquipmentLoadout equipment)
    {
        equipment = default;
        if (_disposed)
        {
            return false;
        }

        equipment = _loadoutService.GetPreparedEquipment(_profileId);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context.ProfileCommitted -= OnProfileCommitted;
        Changed = null;
        _snapshot.Clear();
    }

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (_disposed || profileId != _profileId)
        {
            return;
        }

        Revision++;
        Changed?.Invoke();
    }
}

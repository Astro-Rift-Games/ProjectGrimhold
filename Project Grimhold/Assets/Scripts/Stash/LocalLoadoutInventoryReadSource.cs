using System;
using System.Collections.Generic;

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
    private readonly TownInventoryProjection _readOnlySnapshot;
    private readonly List<StashItem> _physicalSnapshot = new();
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
        _readOnlySnapshot = new TownInventoryProjection(_snapshot, _physicalSnapshot);
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
        _physicalSnapshot.Clear();
        for (int index = 0; index < loadout.Count; index++)
        {
            StashItem item = loadout[index];
            if (!item.IsValid)
            {
                _snapshot.Clear();
                _physicalSnapshot.Clear();
                return false;
            }

            _snapshot.Add(new LootEntry(item.LootId, item.Amount));
            _physicalSnapshot.Add(item);
        }

        _readOnlySnapshot.ConfirmedAttributes = ReadConfirmedAttributes();
        content = _readOnlySnapshot;
        return true;
    }

    /// <summary>Reads only this profile's confirmed state; missing state never becomes zero attributes.</summary>
    public CharacterAttributeState? ReadConfirmedAttributes() =>
        !_disposed && _context.Store != null && _context.Store.ProfileId == _profileId &&
        _context.Store.TryGetCharacterAttributeState(out CharacterAttributeState attributes)
            ? attributes : null;

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
        _physicalSnapshot.Clear();
        _readOnlySnapshot.ConfirmedAttributes = null;
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

using UnityEngine;

/// <summary>
/// Scene-resident Town presenter of the loss notice a technical Raid failure leaves behind.
/// It reads the one-shot notice from the application-owned coordinator, so it works after the
/// Raid runner is gone, and shows it until the player continues.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownRaidLossNoticePresenter : MonoBehaviour
{
    [SerializeField] private TownRaidLossNoticeView _view;

    private void OnEnable()
    {
        if (_view != null)
        {
            _view.ContinueRequested += OnContinueRequested;
        }
    }

    private void OnDisable()
    {
        if (_view != null)
        {
            _view.ContinueRequested -= OnContinueRequested;
        }
    }

    private void Update()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (_view == null || _view.IsOpen || coordinator == null ||
            coordinator.State != SessionConnectionState.Town ||
            !coordinator.TryConsumePendingLossNotice(out RaidLossNotice notice))
        {
            return;
        }

        ApplicationStashContext stash = FindAnyObjectByType<ApplicationStashContext>();
        LootDefinitionCatalog catalog = stash != null && stash.Store != null ? stash.Store.LootCatalog : null;
        _view.Present(TownRaidLossNoticePresentation.Build(notice, lootId => ResolveName(catalog, lootId)));
        Debug.Log(
            $"[FIX-53] Loss notice shown. Reason={notice.Reason}, Items={notice.Lost.Entries.Count}.",
            this);
    }

    private void OnContinueRequested()
    {
        _view.Hide();
        Debug.Log("[FIX-53] Loss notice dismissed.", this);
    }

    private static string ResolveName(LootDefinitionCatalog catalog, LootId lootId) =>
        catalog != null && catalog.TryGet(lootId.Value, out LootDefinition definition) && definition != null
            ? definition.DisplayName
            : null;
}

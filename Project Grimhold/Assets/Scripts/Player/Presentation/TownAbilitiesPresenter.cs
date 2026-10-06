using System;
using UnityEngine;

/// <summary>
/// Plugs the Abilities tab into the Town player menu. It reads the confirmed profile through
/// <see cref="LocalProfileStore"/>, keeps the filter and the selection, and routes equip/clear through
/// <see cref="TownAbilityMutationEndpoint"/>, which owns the Ready gate. The view never touches the store.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownAbilitiesPresenter : MonoBehaviour
{
    [SerializeField] private TownAbilitiesView _viewPrefab;
    [SerializeField] private AbilityDefinitionCatalog _abilityCatalog;

    private TownPlayerMenuPresenter _menu;
    private TownAbilitiesView _view;
    private ApplicationStashContext _context;
    private ITownAbilityMutationEndpoint _endpoint;
    private Func<bool> _canMutate;
    private TownAbilitiesFilter _filter = TownAbilitiesFilter.All;
    private AbilityId _selected;
    private bool _isListening;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private AbilityPlayModeDebugger _debugger;
#endif

    /// <summary>
    /// Creates the Abilities content and registers it as a tab of the menu. <paramref name="canMutate"/>
    /// is the Town Ready gate; it is read on every refresh and by the mutation endpoint.
    /// </summary>
    public void Register(TownPlayerMenuPresenter menu, ApplicationStashContext context, Func<bool> canMutate)
    {
        Unregister();
        if (menu == null)
        {
            return;
        }

        if (_viewPrefab == null)
        {
            Debug.LogError($"{nameof(TownAbilitiesPresenter)} is missing its serialized view prefab.", this);
            return;
        }

        _menu = menu;
        _context = context;
        _canMutate = canMutate ?? (() => false);
        _filter = TownAbilitiesFilter.All;
        _selected = default;
        _endpoint = CreateEndpoint();

        _view = Instantiate(_viewPrefab, transform, false);
        _view.name = _viewPrefab.name;
        _view.Close();
        _view.FilterRequested += OnFilterRequested;
        _view.AbilitySelected += OnAbilitySelected;
        _view.EquipRequested += OnEquipRequested;
        _view.ClearRequested += OnClearRequested;
        _menu.RegisterTab(new TownMenuTabRegistration(
            TownMenuTabIds.Abilities,
            "Abilities",
            (RectTransform)_view.transform,
            OnShown,
            OnHidden));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // No production source unlocks abilities yet; the F8 overlay fills the repertoire for testing.
        _debugger = gameObject.AddComponent<AbilityPlayModeDebugger>();
        _debugger.Initialize(context, _abilityCatalog);
#endif
    }

    public void Unregister()
    {
        StopListening();
        if (_menu != null)
        {
            _menu.UnregisterTab(TownMenuTabIds.Abilities);
        }

        _menu = null;
        _context = null;
        _endpoint = null;
        _canMutate = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_debugger != null)
        {
            Destroy(_debugger);
            _debugger = null;
        }
#endif
        if (_view == null)
        {
            return;
        }

        _view.FilterRequested -= OnFilterRequested;
        _view.AbilitySelected -= OnAbilitySelected;
        _view.EquipRequested -= OnEquipRequested;
        _view.ClearRequested -= OnClearRequested;
        Destroy(_view.gameObject);
        _view = null;
    }

    private void OnDestroy() => Unregister();

    private ITownAbilityMutationEndpoint CreateEndpoint()
    {
        LocalProfileStore store = _context != null ? _context.Store : null;
        if (store == null || _abilityCatalog == null)
        {
            return null;
        }

        return new TownAbilityMutationEndpoint(store, _abilityCatalog, () => _canMutate(), Refresh);
    }

    private void OnShown()
    {
        if (_view != null)
        {
            _view.Open();
        }

        StartListening();
        // The Ready state can change without a profile commit, so showing the tab always refreshes.
        Refresh();
    }

    private void OnHidden()
    {
        StopListening();
        if (_view != null)
        {
            _view.Close();
        }
    }

    private void StartListening()
    {
        if (_isListening || _context == null)
        {
            return;
        }

        _context.ProfileCommitted += OnProfileCommitted;
        _isListening = true;
    }

    private void StopListening()
    {
        if (!_isListening)
        {
            return;
        }

        if (_context != null)
        {
            _context.ProfileCommitted -= OnProfileCommitted;
        }

        _isListening = false;
    }

    private void OnProfileCommitted(ProfileId profileId) => Refresh();

    private void OnFilterRequested(TownAbilitiesFilter filter)
    {
        _filter = filter;
        Refresh();
    }

    private void OnAbilitySelected(AbilityId id)
    {
        _selected = id;
        Refresh();
    }

    private void OnEquipRequested(AbilityId id, UniversalAbilitySlot slot)
    {
        if (_endpoint == null)
        {
            return;
        }

        TownAbilityMutationResult result = _endpoint.TryEquip(slot, id);
        switch (result.Outcome)
        {
            case TownAbilityMutationOutcome.Success:
                // The endpoint already refreshed the view after persisting.
                break;
            case TownAbilityMutationOutcome.Rejected:
                Debug.LogWarning(
                    $"[{nameof(TownAbilitiesPresenter)}] Equipping {id.Value} to {slot} was rejected: {result.Preparation}.",
                    this);
                Refresh();
                break;
            default:
                // Blocked by the Ready gate: nothing was attempted; show the locked state.
                Refresh();
                break;
        }
    }

    private void OnClearRequested(UniversalAbilitySlot slot)
    {
        if (_endpoint == null)
        {
            return;
        }

        TownAbilityMutationResult result = _endpoint.TryClear(slot);
        if (result.Outcome != TownAbilityMutationOutcome.Success)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_view == null)
        {
            return;
        }

        LocalProfileStore store = _context != null ? _context.Store : null;
        if (store == null || _abilityCatalog == null || _endpoint == null ||
            !store.TryGetCharacterAttributeState(out CharacterAttributeState attributes) ||
            !TownAbilitiesBuilder.TryBuild(
                _abilityCatalog.Definitions,
                store.GetUnlockedAbilities(),
                store.GetPreparedAbilities(),
                attributes,
                out TownAbilitiesPresentation presentation))
        {
            _view.PresentUnavailable();
            return;
        }

        _selected = TownAbilitiesSelection.Resolve(presentation.Filtered(_filter), _selected);
        _view.Present(presentation, _filter, _selected, _canMutate(), _endpoint.CanEquip);
    }
}

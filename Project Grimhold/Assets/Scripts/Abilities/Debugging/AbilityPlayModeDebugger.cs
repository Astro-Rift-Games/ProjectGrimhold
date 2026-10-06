#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Developer-only overlay (F8) that unlocks abilities in the local profile so the Town Abilities tab can
/// be tested while no production source grants them. Created by <see cref="TownAbilitiesPresenter"/> for the
/// local player only and compiled out of release builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityPlayModeDebugger : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F8;

    private ApplicationStashContext _context;
    private AbilityDefinitionCatalog _catalog;
    private bool _visible;
    private Rect _windowRect = new(12f, 440f, 380f, 340f);
    private Vector2 _scroll;

    public void Initialize(ApplicationStashContext context, AbilityDefinitionCatalog catalog)
    {
        _context = context;
        _catalog = catalog;
    }

    private LocalProfileStore Store => _context != null ? _context.Store : null;

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _visible = !_visible;
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        _windowRect = GUI.Window(888124, _windowRect, DrawWindow, $"Abilities Debug [{ToggleKey}]");
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    private void DrawWindow(int windowId)
    {
        LocalProfileStore store = Store;
        if (store == null || !store.IsAvailable || _catalog == null)
        {
            GUILayout.Label("Waiting for the profile store and the ability catalog...");
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            return;
        }

        if (GUILayout.Button($"Unlock all ({_catalog.DefinitionCount} abilities)"))
        {
            int unlocked = AbilityDebugUnlocker.UnlockAll(store, _catalog);
            Debug.Log($"[AbilityDebugger] Unlocked {unlocked} abilities.", this);
        }

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (AbilityDefinition definition in _catalog.Definitions)
        {
            if (definition == null)
            {
                continue;
            }

            bool unlocked = store.IsAbilityUnlocked(definition.AbilityId);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{definition.DisplayName} ({definition.Id})", GUILayout.ExpandWidth(true));
            if (unlocked)
            {
                GUILayout.Label("Unlocked", GUILayout.Width(70f));
            }
            else if (GUILayout.Button("Unlock", GUILayout.Width(70f)))
            {
                Debug.Log($"[AbilityDebugger] Unlock '{definition.Id}': {AbilityDebugUnlocker.Unlock(store, definition)}", this);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }
}
#endif

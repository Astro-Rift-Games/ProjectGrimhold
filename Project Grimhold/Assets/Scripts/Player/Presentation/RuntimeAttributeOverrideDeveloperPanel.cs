using UnityEngine;

/// <summary>Development-only IMGUI controls for the local character in Town and Raid.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeAttributeOverrideDeveloperPanel : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly CharacterAttribute[] Attributes =
    {
        CharacterAttribute.Vitality,
        CharacterAttribute.Resistance,
        CharacterAttribute.Strength,
        CharacterAttribute.Dexterity,
        CharacterAttribute.Intelligence,
        CharacterAttribute.Luck
    };

    private NetworkRaidParticipant _participant;
    private RuntimeAttributeOverrideNetworkController _controller;
    private SocialPlayerCharacter _socialPlayer;
    private ApplicationStashContext _applicationContext;
    private RuntimeAttributeOverrideSession _localSession;
    private Rect _windowRect = new(12f, 140f, 470f, 335f);
    private bool _isExpanded;

    private void Awake()
    {
        _participant = GetComponent<NetworkRaidParticipant>();
        _controller = GetComponent<RuntimeAttributeOverrideNetworkController>();
        _socialPlayer = GetComponent<SocialPlayerCharacter>();
        _applicationContext = FindAnyObjectByType<ApplicationStashContext>();
        _localSession = _applicationContext?.RuntimeAttributeOverrides;
    }

    private void OnGUI()
    {
        if (!IsLocalTarget())
        {
            return;
        }

        uint rawId = _participant != null
            ? _participant.Object.Id.Raw
            : _socialPlayer.Object.Id.Raw;
        int windowId = unchecked((int)rawId);
        if (!_isExpanded)
        {
            if (GUI.Button(new Rect(12f, 140f, 145f, 28f), "Show Attribute Tool"))
            {
                _isExpanded = true;
            }
            return;
        }

        _windowRect = GUI.Window(windowId, _windowRect, DrawWindow, "Runtime Attribute Override");
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_participant != null ? "Raid (authoritative)" : "Town (local setup)");
        if (GUILayout.Button("Hide", GUILayout.Width(52f)))
        {
            _isExpanded = false;
        }
        GUILayout.EndHorizontal();

        if (!TryGetSnapshots(
                out CharacterAttributeState persistent,
                out CharacterAttributeState effective))
        {
            GUILayout.Label("Attribute snapshots unavailable.");
            GUI.DragWindow();
            return;
        }

        for (int index = 0; index < Attributes.Length; index++)
        {
            CharacterAttribute attribute = Attributes[index];
            persistent.TryGetValue(attribute, out int persistentValue);
            effective.TryGetValue(attribute, out int effectiveValue);

            GUILayout.BeginHorizontal();
            GUILayout.Label(attribute.ToString(), GUILayout.Width(90f));
            GUILayout.Label($"Persistent: {persistentValue}", GUILayout.Width(90f));
            GUILayout.Label($"Runtime: {effectiveValue}", GUILayout.Width(78f));
            DrawAdjustmentButton(attribute, -5, persistent);
            DrawAdjustmentButton(attribute, -1, persistent);
            DrawAdjustmentButton(attribute, 1, persistent);
            DrawAdjustmentButton(attribute, 5, persistent);
            if (GUILayout.Button("Reset", GUILayout.Width(48f)))
            {
                ResetAttribute(attribute);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Reset all"))
        {
            ResetAll();
        }

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
    }

    private void DrawAdjustmentButton(
        CharacterAttribute attribute,
        int amount,
        in CharacterAttributeState persistent)
    {
        string label = amount > 0 ? $"+{amount}" : amount.ToString();
        if (GUILayout.Button(label, GUILayout.Width(32f)))
        {
            Adjust(attribute, amount, persistent);
        }
    }

    private bool IsLocalTarget() =>
        (_participant != null && _controller != null &&
         _participant.Object != null && _participant.Object.IsValid &&
         _participant.HasInputAuthority) ||
        (_socialPlayer != null && _socialPlayer.Object != null &&
         _socialPlayer.Object.IsValid && _socialPlayer.HasInputAuthority);

    private bool TryGetSnapshots(
        out CharacterAttributeState persistent,
        out CharacterAttributeState effective)
    {
        persistent = default;
        effective = default;
        if (_participant != null)
        {
            return _participant.TryGetPersistentCharacterAttributeState(out persistent) &&
                _participant.TryGetCharacterAttributeState(out effective);
        }

        RuntimeAttributeOverrideSession session = GetLocalSession();
        return session != null && _applicationContext.Store != null &&
            _applicationContext.Store.TryGetCharacterAttributeState(out persistent) &&
            session.TryGetEffectiveState(persistent, out effective);
    }

    private void Adjust(
        CharacterAttribute attribute,
        int amount,
        in CharacterAttributeState persistent)
    {
        RuntimeAttributeOverrideSession session = GetLocalSession();
        if (session != null && session.TryAdjust(attribute, amount, persistent))
        {
            SynchronizeRaid(session.State);
            return;
        }

        _controller?.RequestAdjustment(attribute, amount);
    }

    private void ResetAttribute(CharacterAttribute attribute)
    {
        RuntimeAttributeOverrideSession session = GetLocalSession();
        if (session != null)
        {
            session.Reset(attribute);
            SynchronizeRaid(session.State);
            return;
        }

        _controller?.RequestReset(attribute);
    }

    private void ResetAll()
    {
        RuntimeAttributeOverrideSession session = GetLocalSession();
        if (session != null)
        {
            session.ResetAll();
            SynchronizeRaid(session.State);
            return;
        }

        _controller?.RequestResetAll();
    }

    private RuntimeAttributeOverrideSession GetLocalSession()
    {
        if (_localSession != null)
        {
            return _localSession;
        }

        _localSession = _applicationContext?.RuntimeAttributeOverrides;
        return _localSession;
    }

    private void SynchronizeRaid(in RuntimeAttributeOverrideState state)
    {
        if (_controller != null)
        {
            _controller.RequestState(state);
        }
    }
#else
    private void Awake()
    {
        enabled = false;
    }
#endif
}

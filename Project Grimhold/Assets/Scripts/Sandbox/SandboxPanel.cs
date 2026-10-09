#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dev-only IMGUI panel (F9) for the ability sandbox. It only issues requests through
/// <see cref="SandboxPlayerController"/> and <see cref="SandboxEnemySpawner"/> (State Authority applies
/// them) and reads live state; selection and clamping logic lives in <see cref="SandboxPanelState"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class SandboxPanel : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F9;
    private const float RefreshIntervalSeconds = 0.25f;

    private static readonly CharacterAttribute[] Attributes =
        (CharacterAttribute[])Enum.GetValues(typeof(CharacterAttribute));

    private static readonly EquipmentSlot[] EquipmentSlots =
    {
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetAOffHand,
        EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.WeaponSetBOffHand,
        EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots
    };

    [SerializeField] private SandboxPlayerController _player;
    [SerializeField] private SandboxEnemySpawner _spawner;
    [SerializeField] private AbilityDefinitionCatalog _abilityCatalog;
    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField, Min(1)] private int _maxSpawnCount = SandboxSpawnPlanner.DefaultMaxCount;

    private readonly SandboxPanelState _state = new SandboxPanelState();
    private readonly List<LootDefinition> _equipmentEntries = new List<LootDefinition>();
    private readonly List<TrainingDummyCharacter> _dummies = new List<TrainingDummyCharacter>();

    private PlayerCharacter _avatar;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private PlayerManaNetworkController _mana;
    private PlayerStaminaNetworkController _stamina;
    private NetworkRaidParticipant _participant;
    private float _nextRefresh;
    private bool _visible = true;
    private Rect _windowRect = new Rect(12f, 12f, 470f, 560f);
    private Vector2 _scroll;
    private string _status = string.Empty;
    private string _healthText = "50";
    private string _teleportX = "0";
    private string _teleportY = "0";

    private void Awake()
    {
        if (_player == null) _player = FindFirstObjectByType<SandboxPlayerController>();
        if (_spawner == null) _spawner = FindFirstObjectByType<SandboxEnemySpawner>();
        BuildEquipmentEntries();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _visible = !_visible;
        }

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshReferences();
        }
    }

    private void RefreshReferences()
    {
        _avatar = null;
        _abilityRuntime = null;
        _mana = null;
        _stamina = null;
        _participant = null;
        if (_player != null && _player.Object != null && _player.Object.IsValid && _player.TryGetPlayer(out _avatar))
        {
            _avatar.TryGetComponent(out _abilityRuntime);
            _avatar.TryGetComponent(out _mana);
            _avatar.TryGetComponent(out _stamina);
            if (_avatar.TryGetComponent(out RaidAvatarParticipantLink link))
            {
                link.TryResolveParticipant(out _participant);
            }
        }

        _dummies.Clear();
        _dummies.AddRange(FindObjectsByType<TrainingDummyCharacter>(FindObjectsSortMode.None));
    }

    private void BuildEquipmentEntries()
    {
        _equipmentEntries.Clear();
        if (_lootCatalog == null)
        {
            return;
        }

        for (int i = 0; i < _lootCatalog.DefinitionCount; i++)
        {
            if (_lootCatalog.TryGetByIndex(i, out LootDefinition definition) && definition != null && IsEquipment(definition.Category))
            {
                _equipmentEntries.Add(definition);
            }
        }
    }

    private static bool IsEquipment(LootCategory category) =>
        category == LootCategory.Weapon || category == LootCategory.Shield || category == LootCategory.Helmet ||
        category == LootCategory.Armor || category == LootCategory.Gloves || category == LootCategory.Boots;

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        _windowRect = GUI.Window(888125, _windowRect, DrawWindow, $"Ability Sandbox [{ToggleKey}]");
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    private void DrawWindow(int windowId)
    {
        DrawTabs();
        if (_player == null || _avatar == null)
        {
            GUILayout.Label("Waiting for the sandbox controller and the local player...");
        }

        _scroll = GUILayout.BeginScrollView(_scroll);
        switch (_state.Tab)
        {
            case SandboxPanelTab.Player: DrawPlayerTab(); break;
            case SandboxPanelTab.Abilities: DrawAbilitiesTab(); break;
            case SandboxPanelTab.Enemies: DrawEnemiesTab(); break;
            case SandboxPanelTab.Dummy: DrawDummyTab(); break;
        }

        GUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(_status))
        {
            GUILayout.Label(_status);
        }

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (SandboxPanelTab tab in Enum.GetValues(typeof(SandboxPanelTab)))
        {
            bool selected = _state.Tab == tab;
            if (GUILayout.Toggle(selected, tab.ToString(), GUI.skin.button) && !selected)
            {
                _state.SetTab(tab);
            }
        }

        GUILayout.EndHorizontal();
    }

    // ---- Player ---------------------------------------------------------------------------------

    private void DrawPlayerTab()
    {
        if (_player == null)
        {
            return;
        }

        GUILayout.Label("Vitals");
        if (_avatar != null)
        {
            GUILayout.Label($"Health {_avatar.Health:0}/{_avatar.MaxHealth:0}   {ManaText()}   {StaminaText()}");
        }

        GUILayout.BeginHorizontal();
        _healthText = GUILayout.TextField(_healthText, GUILayout.Width(70f));
        if (GUILayout.Button("Set Health"))
        {
            if (SandboxPanelState.TryParseNumber(_healthText, out float health))
                Report(_player.RequestSetHealth(health), "Set Health");
            else
                _status = "Health must be a finite number.";
        }

        if (GUILayout.Button("Refill Health")) Report(_player.RequestRefillHealth(), "Refill Health");
        if (GUILayout.Button("Refill Mana")) Report(_player.RequestRefillMana(), "Refill Mana");
        GUILayout.EndHorizontal();

        bool god = GUILayout.Toggle(_player.GodMode, "God mode (refills Health every tick)");
        if (god != _player.GodMode) Report(_player.RequestSetGodMode(god), "God mode");
        bool mana = GUILayout.Toggle(_player.InfiniteMana, "Infinite mana (refills every tick)");
        if (mana != _player.InfiniteMana) Report(_player.RequestSetInfiniteMana(mana), "Infinite mana");
        GUILayout.Label("Stamina refill is not supported (no hook).");

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Teleport X/Y", GUILayout.Width(90f));
        _teleportX = GUILayout.TextField(_teleportX, GUILayout.Width(60f));
        _teleportY = GUILayout.TextField(_teleportY, GUILayout.Width(60f));
        if (GUILayout.Button("Teleport"))
        {
            if (SandboxPanelState.TryParseVector(_teleportX, _teleportY, out Vector2 target))
                Report(_player.RequestTeleport(target), "Teleport");
            else
                _status = "Teleport needs two finite numbers.";
        }

        GUILayout.EndHorizontal();
        if (GUILayout.Button("Reset cooldowns")) Report(_player.RequestResetCooldowns(), "Reset cooldowns");

        DrawAttributes();
        DrawEquipment();
    }

    private void DrawAttributes()
    {
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Attributes (session offsets)");
        GUILayout.Label("Step", GUILayout.Width(34f));
        if (GUILayout.Button("-", GUILayout.Width(24f))) _state.AdjustAttributeStep(-1);
        GUILayout.Label(_state.AttributeStep.ToString(), GUILayout.Width(22f));
        if (GUILayout.Button("+", GUILayout.Width(24f))) _state.AdjustAttributeStep(1);
        GUILayout.EndHorizontal();

        bool hasState = false;
        CharacterAttributeState effective = default;
        if (_participant != null)
        {
            hasState = _participant.TryGetCharacterAttributeState(out effective);
        }

        foreach (CharacterAttribute attribute in Attributes)
        {
            GUILayout.BeginHorizontal();
            string value = hasState && effective.TryGetValue(attribute, out int current) ? current.ToString() : "?";
            GUILayout.Label($"{attribute}: {value}", GUILayout.Width(150f));
            if (GUILayout.Button("-", GUILayout.Width(28f)))
                Report(_player.RequestAttributeAdjustment(attribute, -_state.AttributeStep), "Attribute");
            if (GUILayout.Button("+", GUILayout.Width(28f)))
                Report(_player.RequestAttributeAdjustment(attribute, _state.AttributeStep), "Attribute");
            if (GUILayout.Button("Reset", GUILayout.Width(56f)))
                Report(_player.RequestAttributeReset(attribute), "Attribute reset");
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Reset all attributes")) Report(_player.RequestAttributeResetAll(), "Reset all attributes");
    }

    private void DrawEquipment()
    {
        GUILayout.Space(6f);
        GUILayout.Label("Equipment (bypasses inventory provenance)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_state.WeaponSet == WeaponSetSlot.SetA, "Set A", GUI.skin.button)) _state.WeaponSet = WeaponSetSlot.SetA;
        if (GUILayout.Toggle(_state.WeaponSet == WeaponSetSlot.SetB, "Set B", GUI.skin.button)) _state.WeaponSet = WeaponSetSlot.SetB;
        _state.PreferOffHand = GUILayout.Toggle(_state.PreferOffHand, "Weapon in Off Hand");
        GUILayout.EndHorizontal();

        if (_lootCatalog == null)
        {
            GUILayout.Label("No loot catalog assigned.");
        }

        foreach (LootDefinition definition in _equipmentEntries)
        {
            if (GUILayout.Button($"{definition.DisplayName} [{definition.Category}]"))
            {
                if (SandboxLoadoutRules.TryResolveEquipmentSlot(
                        definition.Category, _state.WeaponSet, _state.PreferOffHand, out EquipmentSlot slot))
                    Report(_player.RequestEquip(definition.LootId, slot), $"Equip {definition.DisplayName}");
                else
                    _status = $"{definition.DisplayName} cannot go to that slot.";
            }
        }

        GUILayout.Label("Unequip");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < EquipmentSlots.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            if (GUILayout.Button(EquipmentSlots[i].ToString(), GUILayout.MinWidth(40f)))
                Report(_player.RequestUnequip(EquipmentSlots[i]), "Unequip");
        }

        GUILayout.EndHorizontal();
    }

    // ---- Abilities ------------------------------------------------------------------------------

    private void DrawAbilitiesTab()
    {
        if (_player == null || _abilityCatalog == null)
        {
            GUILayout.Label("Sandbox controller or ability catalog missing.");
            return;
        }

        bool ignoreRules = GUILayout.Toggle(
            _player.IgnoreSessionRules, "Ignore session rules (skip attribute requirements when casting)");
        if (ignoreRules != _player.IgnoreSessionRules)
            Report(_player.RequestSetIgnoreSessionRules(ignoreRules), "Ignore session rules");
        GUILayout.Label("Resource, cooldown and aim rules still apply. Abilities with no behaviour cannot be cast.");

        GUILayout.Label($"Slot 1: {SlotName(_state.Slot1Index)}    Slot 2: {SlotName(_state.Slot2Index)}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply to player"))
            Report(_player.RequestAbilitySlots(_state.Slot1Index, _state.Slot2Index), "Apply ability slots");
        if (GUILayout.Button("Clear Slot 1")) _state.ClearSlot(1);
        if (GUILayout.Button("Clear Slot 2")) _state.ClearSlot(2);
        GUILayout.EndHorizontal();

        bool hasAttributes = false;
        CharacterAttributeState effective = default;
        if (_participant != null)
        {
            hasAttributes = _participant.TryGetCharacterAttributeState(out effective);
        }

        IReadOnlyList<AbilityDefinition> definitions = _abilityCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            AbilityDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            bool hasBehaviour = _abilityRuntime != null && _abilityRuntime.SandboxHasBehaviour(definition.AbilityId);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                SandboxRuleBypass.DescribeAbility(definition.DisplayName, hasBehaviour) +
                RequirementSuffix(definition, hasAttributes, effective));
            GUI.enabled = hasBehaviour;
            if (GUILayout.Button("Slot 1", GUILayout.Width(60f))) SelectAbility(1, i, hasBehaviour);
            if (GUILayout.Button("Slot 2", GUILayout.Width(60f))) SelectAbility(2, i, hasBehaviour);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Live");
        if (_abilityRuntime == null)
        {
            GUILayout.Label("Ability runtime not available.");
            return;
        }

        DrawSlotReadout(UniversalAbilitySlot.Slot1);
        DrawSlotReadout(UniversalAbilitySlot.Slot2);
        GUILayout.Label($"{ManaText()}   {StaminaText()}");
    }

    private static string RequirementSuffix(
        AbilityDefinition definition, bool hasAttributes, in CharacterAttributeState effective)
    {
        IReadOnlyList<CharacterAttributeRequirement> requirements = definition.AttributeRequirements.Requirements;
        if (requirements.Count == 0)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder();
        foreach (CharacterAttributeRequirement requirement in requirements)
        {
            int? current = hasAttributes && effective.TryGetValue(requirement.Attribute, out int value) ? value : (int?)null;
            text.Append("  [").Append(SandboxRuleBypass.DescribeRequirement(
                requirement.Attribute.ToString(), requirement.MinimumValue, current)).Append(']');
        }

        return text.ToString();
    }

    private void DrawSlotReadout(UniversalAbilitySlot slot)
    {
        string phase = _abilityRuntime.TryGetExecutionSnapshot(slot, out AbilityExecutionSnapshot snapshot)
            ? $"{snapshot.Phase} (#{snapshot.Sequence})"
            : "unbound";
        GUILayout.Label(
            $"{slot}: cooldown {_abilityRuntime.GetRemainingCooldownSeconds(slot):0.0}s   phase {phase}   " +
            $"last failure {_abilityRuntime.GetLastActivationFailure(slot)}");
    }

    private void SelectAbility(int slot, int catalogIndex, bool hasBehaviour)
    {
        _status = _state.TrySelectAbility(slot, catalogIndex, hasBehaviour, out string error)
            ? string.Empty
            : error;
    }

    private string SlotName(int index)
    {
        IReadOnlyList<AbilityDefinition> definitions = _abilityCatalog.Definitions;
        return index >= 0 && index < definitions.Count && definitions[index] != null
            ? definitions[index].DisplayName
            : "(empty)";
    }

    // ---- Enemies --------------------------------------------------------------------------------

    private void DrawEnemiesTab()
    {
        if (_spawner == null)
        {
            GUILayout.Label("No SandboxEnemySpawner in the scene.");
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Kind: {_state.Kind}", GUILayout.Width(120f));
        if (GUILayout.Button("<", GUILayout.Width(28f))) _state.CycleKind(-1);
        if (GUILayout.Button(">", GUILayout.Width(28f))) _state.CycleKind(1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Pattern: {_state.Pattern}", GUILayout.Width(120f));
        if (GUILayout.Button("<", GUILayout.Width(28f))) _state.CyclePattern(-1);
        if (GUILayout.Button(">", GUILayout.Width(28f))) _state.CyclePattern(1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Count: {_state.Count}", GUILayout.Width(120f));
        if (GUILayout.Button("-10", GUILayout.Width(40f))) _state.AdjustCount(-10, _maxSpawnCount);
        if (GUILayout.Button("-", GUILayout.Width(28f))) _state.AdjustCount(-1, _maxSpawnCount);
        if (GUILayout.Button("+", GUILayout.Width(28f))) _state.AdjustCount(1, _maxSpawnCount);
        if (GUILayout.Button("+10", GUILayout.Width(40f))) _state.AdjustCount(10, _maxSpawnCount);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Spacing: {_state.Spacing:0.0}", GUILayout.Width(120f));
        if (GUILayout.Button("-", GUILayout.Width(28f))) _state.AdjustSpacing(-0.5f);
        if (GUILayout.Button("+", GUILayout.Width(28f))) _state.AdjustSpacing(0.5f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = _avatar != null;
        if (GUILayout.Button("Spawn at player"))
        {
            Vector2 center = _avatar.transform.position;
            Report(
                _spawner.RequestSpawn(_state.Kind, _state.Count, _state.Pattern, center, _state.Spacing, float.NaN),
                "Spawn");
        }

        GUI.enabled = true;
        if (GUILayout.Button("Clear all")) Report(_spawner.RequestClearAll(), "Clear all");
        GUILayout.EndHorizontal();
        GUILayout.Label($"Spawned (this peer's view): {_spawner.SpawnedCount}");
        GUILayout.Label("Spawn list is tracked on the State Authority only.");
    }

    // ---- Dummy ----------------------------------------------------------------------------------

    private void DrawDummyTab()
    {
        float total = 0f;
        int hits = 0;
        foreach (TrainingDummyCharacter dummy in _dummies)
        {
            if (dummy == null) continue;
            total += dummy.DamageLog.TotalDamage;
            hits += dummy.DamageLog.HitCount;
        }

        GUILayout.Label($"Dummies in scene: {_dummies.Count}   Total damage: {total:0.#}   Hits: {hits}");
        const int maxRows = 8;
        int rows = 0;
        foreach (TrainingDummyCharacter dummy in _dummies)
        {
            if (dummy == null) continue;
            if (rows++ >= maxRows) { GUILayout.Label("..."); break; }
            DummyDamageLog log = dummy.DamageLog;
            GUILayout.Label($"{dummy.name}: last {log.LastDamage:0.#}  total {log.TotalDamage:0.#}  hits {log.HitCount}");
        }

        if (GUILayout.Button("Reset damage counters"))
        {
            foreach (TrainingDummyCharacter dummy in _dummies)
            {
                if (dummy != null) dummy.DamageLog.Reset();
            }
        }

        GUILayout.Label("Counters are recorded on the State Authority only.");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private string ManaText() =>
        _mana != null && _mana.TryGetMaximumMana(out float max) ? $"Mana {_mana.CurrentMana:0}/{max:0}" : "Mana ?";

    private string StaminaText() =>
        _stamina != null && _stamina.TryGetMaximumStamina(out float max)
            ? $"Stamina {_stamina.CurrentStamina:0}/{max:0}"
            : "Stamina ?";

    private void Report(bool submitted, string label)
    {
        _status = submitted ? $"{label}: requested." : $"{label}: rejected locally (see Console).";
    }
}
#endif

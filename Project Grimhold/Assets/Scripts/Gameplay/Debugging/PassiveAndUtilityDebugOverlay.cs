#if UNITY_EDITOR
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Editor-only debug overlay for live validation of US-76:
/// - Passive treasure modifiers (Pilgrim's Light stamina regen, Void Bell sprint & chimes, Wanderer's Eye vision & minimap).
/// - Utility cooldowns on Player and retention across Loot Containers (chests/corpses) for TASK-265.
/// Toggled with F10.
/// </summary>
public sealed class PassiveAndUtilityDebugOverlay : MonoBehaviour
{
    private static PassiveAndUtilityDebugOverlay s_instance;

    private bool _showOverlay = true;
    private bool _enableConsoleLogs = true;
    private float _searchTimer;

    private PlayerCharacter _localPlayer;
    private PlayerStaminaNetworkController _staminaController;
    private PlayerInventoryPassiveEffectsController _passiveController;
    private PlayerConsumableNetworkController _consumableController;
    private PlayerMovementNetworkController _movementController;
    private PlayerLootReceiver _lootReceiver;

    // Stamina measurement
    private float _lastMeasuredStamina;
    private float _measuredRegenPerSecond;
    private float _staminaSampleTimer;

    // Previous states for logging
    private bool _lastHasPilgrimLight;
    private bool _lastHasVoidBell;
    private bool _lastHasWandererEye;
    private int _lastChimeCount;
    private bool _lastCompassOnCooldown;
    private bool _lastDetectorOnCooldown;
    private string _lastCompassDirectionText = "Ninguna aún (usa la brújula para orientar)";

    private readonly List<NetworkLootContainer> _nearbyContainers = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_INCLUDE_TESTS
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Gameplay")
        {
            return;
        }
#endif
        if (s_instance != null) return;
        var go = new GameObject("PassiveAndUtilityDebugOverlay");
        s_instance = go.AddComponent<PassiveAndUtilityDebugOverlay>();
        DontDestroyOnLoad(go);
    }

    private void OnDisable()
    {
        if (_consumableController != null)
        {
            _consumableController.RuneCompassActivated -= OnCompassActivated;
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            _showOverlay = !_showOverlay;
        }

        _searchTimer -= Time.deltaTime;
        if (_searchTimer <= 0f)
        {
            _searchTimer = 0.5f;
            ResolveLocalPlayer();
            FindNearbyContainers();
        }

        UpdateStaminaMeasurement();
        MonitorStateChangesForLogs();
    }

    private void ResolveLocalPlayer()
    {
        if (_localPlayer != null && _localPlayer.Object != null && _localPlayer.Object.IsValid)
        {
            return;
        }

        _localPlayer = null;
        var players = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].Object != null && players[i].Object.IsValid && players[i].HasInputAuthority)
            {
                _localPlayer = players[i];
                break;
            }
        }

        if (_localPlayer == null && players.Length > 0)
        {
            _localPlayer = players[0];
        }

        if (_localPlayer != null)
        {
            _staminaController = _localPlayer.GetComponent<PlayerStaminaNetworkController>();
            _passiveController = _localPlayer.PassiveEffectsController ?? _localPlayer.GetComponent<PlayerInventoryPassiveEffectsController>();
            _consumableController = _localPlayer.GetComponent<PlayerConsumableNetworkController>();
            _movementController = _localPlayer.GetComponent<PlayerMovementNetworkController>();
            _lootReceiver = _localPlayer.GetComponent<PlayerLootReceiver>();
            _lastMeasuredStamina = _staminaController != null ? _staminaController.CurrentStamina : 0f;
            if (_consumableController != null)
            {
                _consumableController.RuneCompassActivated -= OnCompassActivated;
                _consumableController.RuneCompassActivated += OnCompassActivated;
            }
        }
    }

    private void OnCompassActivated(CardinalDirection dir, float duration)
    {
        string localized = CardinalDirectionHelper.GetLocalizedName(dir);
        string abbr = CardinalDirectionHelper.GetAbbreviation(dir);
        _lastCompassDirectionText = $"Santuario hacia el {localized} ({abbr})";
        Debug.Log($"<color=#33E6B8>[US-76 Debug]</color> ¡Brújula Rúnica Activada! Santuario detectado hacia el <b>{localized} ({abbr})</b>. Duración: {duration:F1}s");
    }

    private void FindNearbyContainers()
    {
        _nearbyContainers.Clear();
        if (_localPlayer == null) return;

        Vector3 playerPos = _localPlayer.transform.position;
        var containers = FindObjectsByType<NetworkLootContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] == null || containers[i].gameObject == _localPlayer.gameObject) continue;
            float dist = Vector3.Distance(playerPos, containers[i].transform.position);
            if (dist <= 10f)
            {
                _nearbyContainers.Add(containers[i]);
            }
        }
    }

    private void UpdateStaminaMeasurement()
    {
        if (_staminaController == null) return;

        _staminaSampleTimer += Time.deltaTime;
        if (_staminaSampleTimer >= 0.25f)
        {
            float current = _staminaController.CurrentStamina;
            float delta = current - _lastMeasuredStamina;
            if (delta > 0f)
            {
                _measuredRegenPerSecond = delta / _staminaSampleTimer;
            }
            else if (delta < 0f)
            {
                _measuredRegenPerSecond = 0f;
            }
            _lastMeasuredStamina = current;
            _staminaSampleTimer = 0f;
        }
    }

    private void MonitorStateChangesForLogs()
    {
        if (!_enableConsoleLogs || _passiveController == null) return;

        bool hasPilgrim = _passiveController.HasPilgrimLight;
        if (hasPilgrim != _lastHasPilgrimLight)
        {
            _lastHasPilgrimLight = hasPilgrim;
            float rate = _staminaController != null ? _staminaController.EffectiveRegenerationPerSecond : 15f;
            Debug.Log($"<color=#75D375>[US-76 Debug]</color> Luz del Peregrino: {(hasPilgrim ? "EQUIPADA (+35%)" : "REMOBIDA (Normal)")} -> Regeneración efectiva = {rate:F2}/s");
        }

        bool hasBell = _passiveController.HasVoidBell;
        if (hasBell != _lastHasVoidBell)
        {
            _lastHasVoidBell = hasBell;
            Debug.Log($"<color=#B075D3>[US-76 Debug]</color> Campana del Vacío: {(hasBell ? "EQUIPADA (+15% velocidad sprint, chimes cada 2s al esprintar)" : "REMOBIDA")}");
        }

        int chimes = _passiveController.PresentedVoidBellChimeCount;
        if (chimes != _lastChimeCount)
        {
            _lastChimeCount = chimes;
            Debug.Log($"<color=#B075D3>[US-76 Debug]</color> ¡Campanada del Vacío #{chimes} emitida! (VFX anillo violeta y alerta acústica a enemigos en 15m)");
        }

        bool hasEye = _passiveController.HasWandererEye;
        if (hasEye != _lastHasWandererEye)
        {
            _lastHasWandererEye = hasEye;
            Debug.Log($"<color=#D3B075>[US-76 Debug]</color> Ojo del Errante: {(hasEye ? "EQUIPADO (Visión reducida a 0.75x, minimapa bloqueado)" : "REMOBIDO (Visión normal, minimapa activo)")}");
        }

        if (_consumableController != null && _consumableController.Runner != null)
        {
            var runner = _consumableController.Runner;
            bool compassActive = !_consumableController.GetUtilityCooldown(PlayerConsumableNetworkController.RuneCompassId).ExpiredOrNotRunning(runner);
            if (compassActive != _lastCompassOnCooldown)
            {
                _lastCompassOnCooldown = compassActive;
                Debug.Log($"<color=#75B0D3>[TASK-265 Debug]</color> Brújula Rúnica cooldown: {(compassActive ? "INICIADO (30s)" : "EXPIRADO (Listo para usar)")}");
            }

            bool detectorActive = !_consumableController.GetUtilityCooldown(PlayerConsumableNetworkController.ArcaneDetectorId).ExpiredOrNotRunning(runner);
            if (detectorActive != _lastDetectorOnCooldown)
            {
                _lastDetectorOnCooldown = detectorActive;
                Debug.Log($"<color=#75B0D3>[TASK-265 Debug]</color> Detector Arcano cooldown: {(detectorActive ? "INICIADO (20s)" : "EXPIRADO (Listo para usar)")}");
            }
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        float width = 420f;
        float height = 580f;
        float x = 10f;
        float y = 10f;

        GUI.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        GUILayout.BeginArea(new Rect(x, y, width, height), "US-76 Passive Effects & Utilities Debug (F10)", GUI.skin.window);
        GUI.backgroundColor = Color.white;

        if (_localPlayer == null || _passiveController == null)
        {
            GUILayout.Label("Esperando a que el PlayerCharacter spawnee en Gameplay...");
            GUILayout.EndArea();
            return;
        }

        _enableConsoleLogs = GUILayout.Toggle(_enableConsoleLogs, " Registrar cambios y campanadas en Unity Console");
        GUILayout.Space(4);

        // --- SECCIÓN 1: LUZ DEL PEREGRINO & STAMINA ---
        GUILayout.Label("<b>--- 1. Luz del Peregrino (TRE_PILGRIM_LIGHT) ---</b>");
        bool hasPilgrim = _passiveController.HasPilgrimLight;
        if (hasPilgrim)
        {
            GUI.color = new Color(0.4f, 1f, 0.4f);
            GUILayout.Label("• Estado: EQUIPADA en inventario (+35% regen)");
        }
        else
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("• Estado: NO equipada (Regeneración base 15.0/s)");
        }
        GUI.color = Color.white;

        if (_staminaController != null)
        {
            _staminaController.TryGetMaximumStamina(out float maxStamina);
            float current = _staminaController.CurrentStamina;
            GUILayout.Label($"• Resistencia: {current:F1} / {maxStamina:F1} (Exhausto: {_staminaController.IsExhausted})");

            Rect barRect = GUILayoutUtility.GetRect(width - 24, 14);
            GUI.Box(barRect, "");
            float pct = maxStamina > 0 ? Mathf.Clamp01(current / maxStamina) : 0f;
            GUI.backgroundColor = hasPilgrim ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.85f, 0.75f, 0.2f);
            GUI.Box(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), "");
            GUI.backgroundColor = Color.white;

            float baseRate = 15f;
            float effectiveRate = _staminaController.EffectiveRegenerationPerSecond;
            GUILayout.Label($"• Tasa teórica: {effectiveRate:F2}/s {(hasPilgrim ? "[+35% ACTIVO]" : "[BASE]")} | Medida en vivo: {_measuredRegenPerSecond:F1}/s");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Gastar 40 Stamina"))
            {
                _staminaController.TrySpend(40f);
            }
            if (GUILayout.Button("Vaciar Stamina (Exhaust)"))
            {
                _staminaController.TrySpend(current);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        // --- SECCIÓN 2: CAMPANA DEL VACÍO ---
        GUILayout.Label("<b>--- 2. Campana del Vacío (TRE_VOID_BELL) ---</b>");
        bool hasBell = _passiveController.HasVoidBell;
        if (hasBell)
        {
            GUI.color = new Color(0.8f, 0.5f, 1f);
            GUILayout.Label("• Estado: EQUIPADA en inventario");
        }
        else
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("• Estado: NO equipada");
        }
        GUI.color = Color.white;

        bool isMoving = _movementController != null && _movementController.IsMoving;
        bool isSprinting = _movementController != null && _movementController.IsSprinting;
        GUILayout.Label($"• Movimiento: {(isMoving ? "En movimiento" : "Quieto")} | Esprint (LeftShift): {(isSprinting ? "<color=#55FF55>ACTIVO (6.9 m/s)</color>" : "Inactivo")}");
        GUILayout.Label($"• Multiplicador de sprint: {(_passiveController.SprintSpeedMultiplier * 1.5f):F2}x (Base 1.50x, con Campana 1.725x)");
        GUILayout.Label($"• Campanadas presentadas localmente: {_passiveController.PresentedVoidBellChimeCount}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Probar Campanada Local (Audio + VFX)"))
        {
            _passiveController.PresentVoidBellChime(_consumableController != null ? _consumableController.Runner : null);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- SECCIÓN 3: OJO DEL ERRANTE ---
        GUILayout.Label("<b>--- 3. Ojo del Errante (TRE_WANDERER_EYE) ---</b>");
        bool hasEye = _passiveController.HasWandererEye;
        if (hasEye)
        {
            GUI.color = new Color(1f, 0.6f, 0.2f);
            GUILayout.Label("• Estado: EQUIPADO (Visión reducida a 0.75x, Minimapa bloqueado)");
        }
        else
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("• Estado: NO equipado (Visión normal 1.0x, Minimapa visible)");
        }
        GUI.color = Color.white;

        GUILayout.Space(8);

        // --- SECCIÓN 4: UTILIDADES Y COOLDOWNS (TASK-265) ---
        GUILayout.Label("<b>--- 4. Cooldowns de Utilidades (TASK-265) ---</b>");
        if (_consumableController != null && _consumableController.Runner != null)
        {
            var runner = _consumableController.Runner;
            TickTimer compassTimer = _consumableController.GetUtilityCooldown(PlayerConsumableNetworkController.RuneCompassId);
            TickTimer detectorTimer = _consumableController.GetUtilityCooldown(PlayerConsumableNetworkController.ArcaneDetectorId);

            float? compassRem = compassTimer.RemainingTime(runner);
            float? detectorRem = detectorTimer.RemainingTime(runner);

            string compassStr = compassRem.HasValue && compassRem.Value > 0 ? $"<color=#FF8844>{compassRem.Value:F1}s restantes</color>" : "<color=#44FF44>LISTA (Sin cooldown)</color>";
            string detectorStr = detectorRem.HasValue && detectorRem.Value > 0 ? $"<color=#FF8844>{detectorRem.Value:F1}s restantes</color>" : "<color=#44FF44>LISTO (Sin cooldown)</color>";

            GUILayout.Label($"• Brújula Rúnica (Jugador): {compassStr}");
            GUILayout.Label($"  <color=#75D3B0>→ Orientación:</color> <b>{_lastCompassDirectionText}</b>");
            GUILayout.Label($"• Detector Arcano (Jugador): {detectorStr}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Usar Brújula Rúnica"))
            {
                _consumableController.TryRequestConsume(PlayerConsumableNetworkController.RuneCompassId);
            }
            if (GUILayout.Button("Usar Detector Arcano"))
            {
                _consumableController.TryRequestConsume(PlayerConsumableNetworkController.ArcaneDetectorId);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reiniciar Cooldowns de Jugador"))
            {
                _consumableController.ClearUtilityCooldown(PlayerConsumableNetworkController.RuneCompassId);
                _consumableController.ClearUtilityCooldown(PlayerConsumableNetworkController.ArcaneDetectorId);
            }

            GUILayout.Space(4);
            GUILayout.Label($"<b>Cofres / Contenedores cercanos ({_nearbyContainers.Count}):</b>");
            if (_nearbyContainers.Count == 0)
            {
                GUILayout.Label("<i>(No hay contenedores a menos de 10m. Usa el spawner F8 para generar uno)</i>");
            }
            else
            {
                for (int i = 0; i < _nearbyContainers.Count; i++)
                {
                    var container = _nearbyContainers[i];
                    if (container == null) continue;
                    float dist = Vector3.Distance(_localPlayer.transform.position, container.transform.position);

                    TickTimer cCompass = container.GetUtilityCooldown(PlayerConsumableNetworkController.RuneCompassId);
                    TickTimer cDetector = container.GetUtilityCooldown(PlayerConsumableNetworkController.ArcaneDetectorId);
                    float? cCompRem = cCompass.RemainingTime(runner);
                    float? cDetRem = cDetector.RemainingTime(runner);

                    string storedInfo = "";
                    if (cCompRem.HasValue && cCompRem.Value > 0) storedInfo += $" [Brújula: {cCompRem.Value:F1}s]";
                    if (cDetRem.HasValue && cDetRem.Value > 0) storedInfo += $" [Detector: {cDetRem.Value:F1}s]";
                    if (string.IsNullOrEmpty(storedInfo)) storedInfo = " (Sin cooldowns almacenados)";

                    GUILayout.Label($"• {container.name} a {dist:F1}m:{storedInfo}");
                }
            }
        }

        GUILayout.EndArea();
    }
}
#endif

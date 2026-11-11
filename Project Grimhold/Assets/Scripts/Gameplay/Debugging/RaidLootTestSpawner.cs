#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Runtime developer/testing tool for testing consumables and treasures in PlayMode.
/// Allows spawning specific test loot pickups or arbitrary LootDefinitions directly at the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidLootTestSpawner : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Default loot pickup network prefab to spawn.")]
    [SerializeField] private NetworkObject _pickupPrefab;

    [Tooltip("Shared catalog for translating loot identifiers.")]
    [SerializeField] private LootDefinitionCatalog _lootCatalog;

    [Header("Hotkeys & UI")]
    [SerializeField] private KeyCode _spawnAllHotkey = KeyCode.F8;

    [Header("Test Consumables")]
    [SerializeField] private LootDefinition _manaPotion;
    [SerializeField] private LootDefinition _manaFragment;
    [SerializeField] private LootDefinition _healthPotion;

    [Header("Test Throwables")]
    [SerializeField] private LootDefinition _explosiveBomb;
    [SerializeField] private LootDefinition _smokeBomb;
    [SerializeField] private LootDefinition _throwingDagger;

    [Header("Test MVP Treasures")]
    [SerializeField] private LootDefinition _chaosCoin;
    [SerializeField] private LootDefinition _pilgrimLight;
    [SerializeField] private LootDefinition _sanctuaryHeart;
    [SerializeField] private LootDefinition _voidBell;
    [SerializeField] private LootDefinition _famineIdol;
    [SerializeField] private LootDefinition _wandererEye;

    [Header("Test Expedition Utilities (TASK-265)")]
    [SerializeField] private LootDefinition _runeCompass;
    [SerializeField] private LootDefinition _arcaneDetector;
    [SerializeField] private LootDefinition _etherDust;
    [SerializeField] private NetworkObject _lootContainerPrefab;

    [Header("Custom Spawn Slot")]
    public LootDefinition CustomItemToSpawn;
    public int CustomItemAmount = 1;

    private void Awake()
    {
        if (_lootCatalog == null)
        {
            _lootCatalog = Resources.Load<LootDefinitionCatalog>("Loot/Catalogs/LootDefinitionCatalog");
        }
        if (_lootCatalog != null)
        {
            if (_explosiveBomb == null) _lootCatalog.TryGet("CON_EXPLOSIVE_BOMB", out _explosiveBomb);
            if (_smokeBomb == null) _lootCatalog.TryGet("CON_SMOKE_BOMB", out _smokeBomb);
            if (_throwingDagger == null) _lootCatalog.TryGet("CON_THROWING_DAGGER", out _throwingDagger);
            if (_runeCompass == null) _lootCatalog.TryGet("UTL_RUNE_COMPASS", out _runeCompass);
            if (_arcaneDetector == null) _lootCatalog.TryGet("UTL_ARCANE_DETECTOR", out _arcaneDetector);
            if (_etherDust == null) _lootCatalog.TryGet("RES_ETHER_DUST", out _etherDust);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(_spawnAllHotkey))
        {
            SpawnAllTestLoot();
        }
    }

    [ContextMenu("Spawn All Test Items At Player (F8)")]
    public void SpawnAllTestLoot()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer)
        {
            Debug.LogWarning("[LootTestSpawner] Cannot spawn loot: Active runner is null or not State Authority/Host.");
            return;
        }

        Vector3 origin = GetPlayerSpawnAnchor();

        var items = new (LootDefinition def, int amount)[]
        {
            (_manaPotion, 2),
            (_manaFragment, 5),
            (_healthPotion, 2),
            (_explosiveBomb, 3),
            (_smokeBomb, 2),
            (_throwingDagger, 5),
            (_chaosCoin, 1),
            (_pilgrimLight, 1),
            (_sanctuaryHeart, 1),
            (_voidBell, 1),
            (_famineIdol, 1),
            (_wandererEye, 1),
            (_runeCompass, 1),
            (_arcaneDetector, 1),
            (_etherDust, 3)
        };

        float spacing = 1.0f;
        int count = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].def != null)
            {
                float offsetX = ((count % 5) - 2) * spacing;
                float offsetY = -((count / 5) + 1) * spacing;
                Vector3 spawnPos = origin + new Vector3(offsetX, offsetY, 0f);
                SpawnLootAt(runner, items[i].def, items[i].amount, spawnPos);
                count++;
            }
        }

        Debug.Log($"[LootTestSpawner] Spawned {count} test items at position {origin}.");
    }

    public void SpawnManaConsumables()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        Vector3 origin = GetPlayerSpawnAnchor();
        if (_manaPotion != null) SpawnLootAt(runner, _manaPotion, 2, origin + new Vector3(-0.75f, -1f, 0f));
        if (_manaFragment != null) SpawnLootAt(runner, _manaFragment, 5, origin + new Vector3(0.75f, -1f, 0f));
    }

    public void SpawnHealthConsumable()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        Vector3 origin = GetPlayerSpawnAnchor();
        if (_healthPotion != null) SpawnLootAt(runner, _healthPotion, 2, origin + new Vector3(0f, -1f, 0f));
    }

    public void SpawnTreasures()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        Vector3 origin = GetPlayerSpawnAnchor();
        var treasures = new LootDefinition[] { _chaosCoin, _pilgrimLight, _sanctuaryHeart, _voidBell, _famineIdol, _wandererEye };
        for (int i = 0; i < treasures.Length; i++)
        {
            if (treasures[i] != null)
            {
                Vector3 pos = origin + new Vector3((i - 2.5f) * 0.9f, -1.2f, 0f);
                SpawnLootAt(runner, treasures[i], 1, pos);
            }
        }
    }

    public void SpawnExpeditionUtilities()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        Vector3 origin = GetPlayerSpawnAnchor();
        if (_runeCompass != null) SpawnLootAt(runner, _runeCompass, 1, origin + new Vector3(-1.0f, -1.2f, 0f));
        if (_arcaneDetector != null) SpawnLootAt(runner, _arcaneDetector, 1, origin + new Vector3(0f, -1.2f, 0f));
        if (_etherDust != null) SpawnLootAt(runner, _etherDust, 3, origin + new Vector3(1.0f, -1.2f, 0f));
    }

    public void SpawnTestContainer()
    {
        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        if (_lootContainerPrefab == null)
        {
            _lootContainerPrefab = Resources.Load<NetworkObject>("Prefabs/LootContainer");
        }
        if (_lootContainerPrefab == null)
        {
            Debug.LogWarning("[LootTestSpawner] Cannot spawn test container: _lootContainerPrefab is null.");
            return;
        }

        Vector3 spawnPos = GetPlayerSpawnAnchor() + new Vector3(1.5f, 0f, 0f);
        try
        {
            runner.Spawn(_lootContainerPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[LootTestSpawner] Spawned test container at {spawnPos}.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LootTestSpawner] Failed to spawn container: {ex.Message}");
        }
    }

    public void SpawnCustomItem()
    {
        if (CustomItemToSpawn == null)
        {
            Debug.LogWarning("[LootTestSpawner] No CustomItemToSpawn selected.");
            return;
        }

        var runner = FindActiveRunner();
        if (runner == null || !runner.IsServer) return;
        Vector3 origin = GetPlayerSpawnAnchor();
        SpawnLootAt(runner, CustomItemToSpawn, Mathf.Max(1, CustomItemAmount), origin + Vector3.down);
    }

    private void SpawnLootAt(NetworkRunner runner, LootDefinition definition, int amount, Vector3 position)
    {
        if (_pickupPrefab == null || definition == null)
        {
            Debug.LogWarning($"[LootTestSpawner] Cannot spawn {definition?.name ?? "null"}: Missing pickup prefab.");
            return;
        }

        LootEntry entry = new LootEntry(definition.LootId, amount);
        try
        {
            runner.Spawn(
                _pickupPrefab,
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    if (instance != null && instance.TryGetComponent<NetworkLootPickup>(out var pickup))
                    {
                        pickup.TrySetSpawnContentOverride(callbackRunner, instance, entry);
                    }
                });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LootTestSpawner] Failed to spawn {definition.LootId}: {ex.Message}");
        }
    }

    private Vector3 GetPlayerSpawnAnchor()
    {
        var cameraBinder = FindAnyObjectByType<LocalPlayerCameraBinder>();
        if (cameraBinder != null)
        {
            return cameraBinder.transform.position;
        }

        var characters = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pc in characters)
        {
            if (pc.Object != null && pc.Object.IsValid && pc.HasInputAuthority)
            {
                return pc.transform.position;
            }
        }

        if (characters.Length > 0)
        {
            return characters[0].transform.position;
        }

        return transform.position;
    }

    private NetworkRunner FindActiveRunner()
    {
        return NetworkRunner.GetRunnerForGameObject(gameObject) ?? FindAnyObjectByType<NetworkRunner>();
    }
}

#endif

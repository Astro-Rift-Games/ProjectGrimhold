#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEngine;
using ProjectGrimhold.Gameplay.Enemies.Specter;
using ProjectGrimhold.Gameplay.Visibility;

namespace ProjectGrimhold.Editor
{
    /// <summary>
    /// Explicit, idempotent Editor authoring for the Catacombs Specter Prefab Variant (US-78 / TASK-275 / TASK-285).
    /// Creates NetworkEnemySpecter.prefab as a Prefab Variant of NetworkEnemy.prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class AuthorSpecterPrefab
    {
        private const string BasePrefabPath = "Assets/Prefabs/Enemies/NetworkEnemy.prefab";
        private const string PrefabDir = "Assets/Prefabs/Enemies";
        private const string PrefabPath = "Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab";
        private const string SystemsPath = "Assets/Prefabs/Systems.prefab";
        private const string LootTablePath = "Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset";
        private const string TelegraphMaterialPath = "Assets/Art/Materials/SpecterTelegraphLine.mat";

        [MenuItem("Grimhold/Author Specter Prefab Variant")]
        public static void CreateOrUpdateSpecterPrefab()
        {
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[AuthorSpecterPrefab] Base prefab not found at {BasePrefabPath}");
                return;
            }

            // Instantiate base prefab as variant root
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            root.name = "NetworkEnemySpecter";

            try
            {
                // 1. Resolve and configure EnemyCharacter
                var enemyChar = root.GetComponent<EnemyCharacter>();
                if (enemyChar == null)
                {
                    Debug.LogError("[AuthorSpecterPrefab] Base prefab is missing EnemyCharacter!");
                    return;
                }

                // Disable standard melee/ranged AI on Specter variant
                var movementAi = root.GetComponent<EnemyMovementAIController>();
                if (movementAi != null) movementAi.enabled = false;

                var combatAi = root.GetComponent<EnemyCombatAIController>();
                if (combatAi != null) combatAi.enabled = false;

                var fsm = root.GetComponent<EnemyFSM>();
                if (fsm != null) fsm.enabled = false;

                var pathfinding = root.GetComponent<EnemyPathfindingNavigator>();
                if (pathfinding != null) pathfinding.enabled = false;

                // 2. Add and configure SpecterAIController
                var specterAi = root.GetComponent<SpecterAIController>();
                if (specterAi == null)
                {
                    specterAi = root.AddComponent<SpecterAIController>();
                }

                var motor = root.GetComponent<Kinematic2DMovementMotor>();
                var rb = root.GetComponent<Rigidbody2D>();
                var col = root.GetComponent<Collider2D>();

                var aiSerialized = new SerializedObject(specterAi);
                aiSerialized.FindProperty("_chaseSpeed").floatValue = 2.2f;
                aiSerialized.FindProperty("_attackRange").floatValue = 5.0f;
                aiSerialized.FindProperty("_detectionRange").floatValue = 7.0f;
                aiSerialized.FindProperty("_disengageRange").floatValue = 12.0f;
                aiSerialized.FindProperty("_leashRadius").floatValue = 15.0f;
                aiSerialized.FindProperty("_detonationDamage").floatValue = 25.0f;
                aiSerialized.FindProperty("_detonationKnockback").floatValue = 2.0f;
                aiSerialized.FindProperty("_playerLayer").intValue = 1 << 7; // Character layer (128)
                aiSerialized.FindProperty("_obstacleLayer").intValue = (1 << 6) | (1 << 11); // WorldCollision | Obstacles
                aiSerialized.FindProperty("_character").objectReferenceValue = enemyChar;
                aiSerialized.FindProperty("_movementMotor").objectReferenceValue = motor;
                aiSerialized.FindProperty("_rigidbody").objectReferenceValue = rb;
                aiSerialized.FindProperty("_collider").objectReferenceValue = col;
                aiSerialized.ApplyModifiedPropertiesWithoutUndo();

                // Wire SpecterAIController into EnemyCharacter
                var charSerialized = new SerializedObject(enemyChar);
                charSerialized.FindProperty("_maxHealth").floatValue = 60f;
                charSerialized.FindProperty("_knockbackResistance").floatValue = 1.0f;
                charSerialized.FindProperty("_specterController").objectReferenceValue = specterAi;
                charSerialized.ApplyModifiedPropertiesWithoutUndo();

                // 3. Configure Loot & Progression
                var lootTable = AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(LootTablePath);
                var randomLoot = root.GetComponent<LootContainerRandomContentConfig>();
                if (randomLoot != null && lootTable != null)
                {
                    var randomLootSerialized = new SerializedObject(randomLoot);
                    randomLootSerialized.FindProperty("_table").objectReferenceValue = lootTable;
                    randomLootSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var killXp = root.GetComponent<KillExperienceSource>();
                if (killXp != null)
                {
                    var xpSerialized = new SerializedObject(killXp);
                    xpSerialized.FindProperty("_killExperience").longValue = 45L;
                    xpSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var missionSource = root.GetComponent<EnemyMissionProgressDefeatSource>();
                if (missionSource != null)
                {
                    var missionSerialized = new SerializedObject(missionSource);
                    missionSerialized.FindProperty("_targetId").stringValue = "specter";
                    missionSerialized.FindProperty("_zoneId").stringValue = "catacombs";
                    missionSerialized.FindProperty("_defeatProgressAmount").intValue = 1;
                    missionSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var promptMetadata = root.GetComponentInChildren<InteractionPromptMetadata>(true);
                if (promptMetadata != null)
                {
                    var promptSerialized = new SerializedObject(promptMetadata);
                    var textProp = promptSerialized.FindProperty("_promptText");
                    if (textProp != null) textProp.stringValue = "Espectro";
                    promptSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                // 4. Configure Visuals & Telegraph
                var bodyTrans = root.transform.Find("Body");
                SpriteRenderer sr = null;
                if (bodyTrans != null)
                {
                    sr = bodyTrans.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = new Color(0.7f, 0.85f, 1.0f, 0.5f);
                    }

                    // Disable standard combat presenters and animators that conflict with Specter
                    var combatPresenter = bodyTrans.GetComponent<EnemyCombatPresenter>();
                    if (combatPresenter != null) combatPresenter.enabled = false;

                    var animView = bodyTrans.GetComponent<EnemyAnimatorView>();
                    if (animView != null) animView.enabled = false;

                    var animator = bodyTrans.GetComponent<Animator>();
                    if (animator != null) animator.enabled = false;

                    var audioListener = bodyTrans.GetComponent<EnemyAnimationAudioListener>();
                    if (audioListener != null) audioListener.enabled = false;
                }

                // Telegraph LineRenderer child
                Transform telegraphTrans = root.transform.Find("TelegraphLine");
                GameObject telegraphObj;
                if (telegraphTrans == null)
                {
                    telegraphObj = new GameObject("TelegraphLine");
                    telegraphObj.transform.SetParent(root.transform, false);
                }
                else
                {
                    telegraphObj = telegraphTrans.gameObject;
                }

                var line = telegraphObj.GetComponent<LineRenderer>();
                if (line == null)
                {
                    line = telegraphObj.AddComponent<LineRenderer>();
                }
                line.useWorldSpace = true;
                line.loop = true;
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.positionCount = 32;
                line.enabled = false;

                var telegraphMat = AssetDatabase.LoadAssetAtPath<Material>(TelegraphMaterialPath);
                if (telegraphMat != null)
                {
                    line.sharedMaterial = telegraphMat;
                }

                // Add and wire SpecterVisualPresenter
                var presenter = root.GetComponent<SpecterVisualPresenter>();
                if (presenter == null)
                {
                    presenter = root.AddComponent<SpecterVisualPresenter>();
                }

                var presenterSerialized = new SerializedObject(presenter);
                presenterSerialized.FindProperty("_controller").objectReferenceValue = specterAi;
                presenterSerialized.FindProperty("_spriteRenderer").objectReferenceValue = sr;
                presenterSerialized.FindProperty("_telegraphLine").objectReferenceValue = line;
                presenterSerialized.FindProperty("_incorporealAlpha").floatValue = 0.5f;
                presenterSerialized.FindProperty("_manifestedAlpha").floatValue = 1.0f;
                presenterSerialized.FindProperty("_incorporealTint").colorValue = new Color(0.7f, 0.85f, 1.0f, 0.5f);
                presenterSerialized.FindProperty("_manifestedTint").colorValue = new Color(1.0f, 0.9f, 0.9f, 1.0f);
                presenterSerialized.FindProperty("_telegraphColor").colorValue = new Color(0.9f, 0.2f, 0.2f, 0.6f);
                presenterSerialized.FindProperty("_detonationColor").colorValue = new Color(1.0f, 0.8f, 0.2f, 0.9f);
                presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

                // 5. Ensure EntityVisibilityPresenter tracks Specter
                var visibilityPresenter = root.GetComponent<EntityVisibilityPresenter>();
                if (visibilityPresenter != null)
                {
                    var visSerialized = new SerializedObject(visibilityPresenter);
                    visSerialized.FindProperty("_visionRadius").floatValue = 7.0f;
                    visSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                // 6. Save Prefab Variant
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (success)
                {
                    Debug.Log($"[AuthorSpecterPrefab] Successfully saved Prefab Variant at {PrefabPath}");
                }
                else
                {
                    Debug.LogError($"[AuthorSpecterPrefab] Failed to save Prefab Variant at {PrefabPath}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // Ensure LineRenderer material is properly persisted on the Prefab Variant asset
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TelegraphMaterialPath);
            if (mat != null)
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
                {
                    var tl = scope.prefabContentsRoot.transform.Find("TelegraphLine");
                    if (tl != null && tl.TryGetComponent(out LineRenderer lr))
                    {
                        lr.sharedMaterial = mat;
                    }
                }
            }

            // 7. Wire into Systems Prefab table if needed
            EnsurePrefabInSystemsTable();
        }

        private static void EnsurePrefabInSystemsTable()
        {
            var specterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (specterPrefab == null) return;

            var systemsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath);
            if (systemsPrefab == null) return;

            var spawnManager = systemsPrefab.GetComponentInChildren<NetworkSpawnManager>(true);
            if (spawnManager == null) return;

            var serializedObj = new SerializedObject(spawnManager);
            var enemyPrefabsProp = serializedObj.FindProperty("_enemyPrefabs");
            if (enemyPrefabsProp != null && enemyPrefabsProp.isArray)
            {
                bool alreadyPresent = false;
                for (int i = 0; i < enemyPrefabsProp.arraySize; i++)
                {
                    var elem = enemyPrefabsProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == specterPrefab)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    int newIndex = enemyPrefabsProp.arraySize;
                    enemyPrefabsProp.InsertArrayElementAtIndex(newIndex);
                    enemyPrefabsProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = specterPrefab;
                    serializedObj.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(systemsPrefab);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[AuthorSpecterPrefab] Added Specter to Systems.prefab _enemyPrefabs at index {newIndex}");
                }
            }
        }
    }
}
#endif

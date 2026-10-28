using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RaidLootTestSpawner))]
public sealed class RaidLootTestSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (RaidLootTestSpawner)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("--- PlayMode Test Loot Spawning ---", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entra a PlayMode en una raid (Gameplay) para spawnear loot interactivamente con los botones de abajo o presionando F8.", MessageType.Info);
            return;
        }

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("★ Spawn ALL 9 Test Items at Player (F8) ★", GUILayout.Height(36)))
        {
            spawner.SpawnAllTestLoot();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Mana (2x Potion, 5x Fragment)", GUILayout.Height(28)))
        {
            spawner.SpawnManaConsumables();
        }
        if (GUILayout.Button("Spawn Health Potion (2x)", GUILayout.Height(28)))
        {
            spawner.SpawnHealthConsumable();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Spawn All 6 MVP Treasures", GUILayout.Height(28)))
        {
            spawner.SpawnTreasures();
        }

        EditorGUILayout.Space(8);
        if (spawner.CustomItemToSpawn != null)
        {
            if (GUILayout.Button($"Spawn Custom: {spawner.CustomItemToSpawn.DisplayName} (x{spawner.CustomItemAmount})", GUILayout.Height(28)))
            {
                spawner.SpawnCustomItem();
            }
        }
    }
}

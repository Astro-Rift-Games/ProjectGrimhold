using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class InitialMissionGenerator
{
    [MenuItem("Grimhold/Missions/Generate Initial Missions")]
    public static void Generate()
    {
        string folderPath = "Assets/Scriptable Objects/Missions";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scriptable Objects"))
                AssetDatabase.CreateFolder("Assets", "Scriptable Objects");
            AssetDatabase.CreateFolder("Assets/Scriptable Objects", "Missions");
        }

        var catalog = ScriptableObject.CreateInstance<MissionDefinitionCatalog>();
        var definitionsList = new List<MissionDefinition>();

        for (int i = 1; i <= 6; i++)
        {
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.name = $"Mission_RankE_0{i}";
            
            var so = new SerializedObject(mission);
            so.FindProperty("_id").stringValue = $"contract_e_{i:D2}";
            so.ApplyModifiedProperties();

            mission.Title = $"Contrato Rango E - {i}";
            mission.Description = $"Descripción del contrato {i} de rango E.";
            mission.Type = MissionType.Normal;
            mission.RankRequired = MissionRank.E;

            // Phase with Objective
            var phase = new PhaseDefinition();
            var objective = new ObjectiveDefinition
            {
                Family = i <= 3 ? ObjectiveFamily.EliminacionPvE : ObjectiveFamily.Interaccion,
                Condition = new ObjectiveCondition { RequiredAmount = 5 }
            };
            phase.Objectives.Add(objective);
            mission.Phases.Add(phase);

            // Reward
            mission.Rewards.Add(new RewardDefinition { Type = RewardDefinition.RewardType.Experience, Amount = 100 });

            AssetDatabase.CreateAsset(mission, $"{folderPath}/{mission.name}.asset");
            definitionsList.Add(mission);
        }

        var catalogSo = new SerializedObject(catalog);
        var defsProp = catalogSo.FindProperty("_definitions");
        defsProp.arraySize = definitionsList.Count;
        for (int i = 0; i < definitionsList.Count; i++)
        {
            defsProp.GetArrayElementAtIndex(i).objectReferenceValue = definitionsList[i];
        }
        catalogSo.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(catalog, $"{folderPath}/MissionDefinitionCatalog.asset");
        AssetDatabase.SaveAssets();

        Debug.Log("Generated 6 Rank E missions and catalog successfully.");
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

public static class ValidateCatalogs
{
    [MenuItem("Tools/Validate Catalogs")]
    public static void RunValidation()
    {
        string definitionPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        string valuePath = "Assets/Scriptable Objects/RaidLootValueCatalog.asset";

        LootDefinitionCatalog defCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(definitionPath);
        RaidLootValueCatalog valCatalog = AssetDatabase.LoadAssetAtPath<RaidLootValueCatalog>(valuePath);

        if (defCatalog == null) { Debug.LogError("LootDefinitionCatalog not found."); return; }
        if (valCatalog == null) { Debug.LogError("RaidLootValueCatalog not found."); return; }

        bool valid = valCatalog.TryValidate(defCatalog, out string error);
        if (!valid)
        {
            Debug.LogError($"Validation Failed: {error}");
        }
        else
        {
            Debug.Log("Validation Passed!");
        }
    }
}

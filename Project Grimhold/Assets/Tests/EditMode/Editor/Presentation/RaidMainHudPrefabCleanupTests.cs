using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the source RaidMainHud prefab against the dead labels left by earlier HUD tasks and makes sure the
/// LocalGameplayHud nested instance carries no override that targets an object missing from that source.
/// </summary>
public sealed class RaidMainHudPrefabCleanupTests
{
    private const string SourcePrefabPath = "Assets/Prefabs/UI/PlayerUI/RaidMainHud.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";

    private static readonly string[] DeadObjectNames =
    {
        "InventoryText",
        "ExtractionText",
        "QuotaText",
        "SanctuaryText",
    };

    [Test]
    public void SourcePrefabContainsNoDeadLabels()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        Assert.That(source, Is.Not.Null);

        var found = new List<string>();
        foreach (Transform child in source.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(DeadObjectNames, child.name) >= 0)
            {
                found.Add(child.name);
            }
        }

        Assert.That(found, Is.Empty, "dead labels left in the source RaidMainHud prefab");
    }

    [Test]
    public void HudNestedInstanceHasNoOverrideTargetingAMissingSourceObject()
    {
        string sourceGuid = AssetDatabase.AssetPathToGUID(SourcePrefabPath);
        string sourceText = File.ReadAllText(SourcePrefabPath);
        string hudText = File.ReadAllText(HudPrefabPath);

        var sourceIds = new HashSet<string>();
        foreach (Match anchor in Regex.Matches(sourceText, @"^--- !u!\d+ &(-?\d+)", RegexOptions.Multiline))
        {
            sourceIds.Add(anchor.Groups[1].Value);
        }

        var dangling = new List<string>();
        foreach (Match reference in Regex.Matches(hudText, @"\{fileID: (-?\d+), guid: " + sourceGuid + @", type: 3\}"))
        {
            string id = reference.Groups[1].Value;
            // 100100000 is the prefab asset itself (m_SourcePrefab), not a source object.
            if (id != "100100000" && !sourceIds.Contains(id))
            {
                dangling.Add(id);
            }
        }

        Assert.That(dangling, Is.Empty, "LocalGameplayHud overrides/removals target ids absent from RaidMainHud");
    }
}

using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class PlayerAbilityRuntimeCompositionTests
{
    [Test]
    public void RaidAvatar_HasOneRuntimeWithExplicitParticipantAndAuthorizedCatalog()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var runtimes = prefab.GetComponents<PlayerAbilityRuntimeNetworkController>();
        Assert.That(runtimes, Has.Length.EqualTo(1));
        var serialized = new SerializedObject(runtimes[0]);
        Assert.That(serialized.FindProperty("_participantLink").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<RaidAvatarParticipantLink>()));
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(
            "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset");
        Assert.That(serialized.FindProperty("_catalog").objectReferenceValue, Is.SameAs(catalog));
        Assert.That(prefab.GetComponent<NetworkObject>().NetworkedBehaviours, Does.Contain(runtimes[0]));
    }
}

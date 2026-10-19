#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("TASK215")]
public sealed class DownedHudPrefabCompositionTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [Test]
    public void NetworkPlayer_BindsTheDownedHudWithEveryViewReferenceAssigned()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(player, Is.Not.Null);

        var view = player.GetComponentInChildren<RaidDownedHudView>(true);
        var presenter = player.GetComponentInChildren<RaidDownedHudPresenter>(true);
        var binder = player.GetComponentInChildren<LocalPlayerHudBinder>(true);
        Assert.That(view, Is.Not.Null);
        Assert.That(presenter, Is.Not.Null);

        var viewSo = new SerializedObject(view);
        foreach (string field in new[]
                 {
                     "_downedRoot", "_reserveFill", "_reserveText", "_stateText", "_reviveRoot",
                     "_reviveFill", "_reviveText", "_allyDownedRoot", "_allyDownedText", "_messageText"
                 })
        {
            Assert.That(viewSo.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }

        Assert.That(new SerializedObject(presenter).FindProperty("_view").objectReferenceValue, Is.SameAs(view));
        Assert.That(new SerializedObject(binder).FindProperty("_downedHudPresenter").objectReferenceValue, Is.SameAs(presenter));
    }

    [Test]
    public void NetworkPlayer_DownedHudStartsHidden()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var viewSo = new SerializedObject(player.GetComponentInChildren<RaidDownedHudView>(true));
        foreach (string field in new[] { "_downedRoot", "_reviveRoot", "_allyDownedRoot" })
        {
            var go = (GameObject)viewSo.FindProperty(field).objectReferenceValue;
            Assert.That(go.activeSelf, Is.False, field);
        }

        var message = (Component)viewSo.FindProperty("_messageText").objectReferenceValue;
        Assert.That(message.gameObject.activeSelf, Is.False);
    }
}
#endif

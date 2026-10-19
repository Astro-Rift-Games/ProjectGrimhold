#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("TASK214")]
public sealed class QuickSlotPrefabCompositionTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [Test]
    public void NetworkPlayer_BindsTheQuickSlotHudWithFourSlots()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(player, Is.Not.Null);

        var hudView = player.GetComponentInChildren<RaidQuickSlotHudView>(true);
        var hudPresenter = player.GetComponentInChildren<RaidQuickSlotHudPresenter>(true);
        var binder = player.GetComponentInChildren<LocalPlayerHudBinder>(true);

        Assert.That(hudView, Is.Not.Null);
        Assert.That(hudView.HasAllSlots, Is.True, "all four HUD slot views must be assigned");
        Assert.That(hudPresenter, Is.Not.Null);

        var presenterSo = new SerializedObject(hudPresenter);
        Assert.That(presenterSo.FindProperty("_view").objectReferenceValue, Is.SameAs(hudView));
        Assert.That(presenterSo.FindProperty("_lootCatalog").objectReferenceValue, Is.Not.Null);

        var binderSo = new SerializedObject(binder);
        Assert.That(binderSo.FindProperty("_quickSlotHudPresenter").objectReferenceValue, Is.SameAs(hudPresenter));
    }
}
#endif

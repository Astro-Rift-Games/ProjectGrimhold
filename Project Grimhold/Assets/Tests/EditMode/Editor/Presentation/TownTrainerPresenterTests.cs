#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Threading.Tasks;
using NetworkObject = Fusion.NetworkObject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

public sealed class TownTrainerPresenterTests
{
    private const string CatalogPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

    [Test]
    public async Task Presenter_UsesOnlyEndpoint_AndBlocksRepeatedClicksUntilAcknowledged()
    {
        var player = new GameObject("Player");
        var npc = new GameObject("Trainer", typeof(NetworkObject));
        try
        {
            var presenter = player.AddComponent<TownTrainerPresenter>();
            var endpoint = new FakeEndpoint();
            presenter.BindEndpoint(endpoint, null, AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath));
            presenter.OpenForTarget(npc.GetComponent<NetworkObject>());
            var button = presenter.View.transform.Find("TrainerPanel/CardsContainer").GetComponentInChildren<Button>(true);
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.That(endpoint.Calls, Is.EqualTo(1));
            Assert.That(endpoint.Target, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(button.interactable, Is.False);
            endpoint.Completion.SetResult(StashOperationResult.Success);
            await Task.Yield();
            Assert.That(button.interactable, Is.True);
            Assert.That(Feedback(presenter.View), Does.Contain("Has recibido"));
            presenter.ClosePanel();
            Assert.That(presenter.View.IsOpen, Is.False);
            Assert.That(presenter.View.transform.Find("DarkScrim").gameObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    [Test]
    public void Presenter_MissingEndpointOrCatalog_HasNoFallback()
    {
        var player = new GameObject("Player");
        var npc = new GameObject("Trainer", typeof(NetworkObject));
        try
        {
            var presenter = player.AddComponent<TownTrainerPresenter>();
            LogAssert.Expect(LogType.Error, "[TownTrainerPresenter] El catálogo no contiene armas de entrenamiento.");
            LogAssert.Expect(LogType.Error, "[TownTrainerPresenter] Falta el endpoint de armas de entrenamiento.");
            presenter.OpenForTarget(npc.GetComponent<NetworkObject>());
            Assert.That(presenter.View.transform.Find("TrainerPanel/CardsContainer").childCount, Is.Zero);
            Assert.That(Feedback(presenter.View), Does.Contain("no está disponible"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    [Test]
    public void Presenter_ReadyLock_IsVisibleOnOpen()
    {
        var player = new GameObject("Player");
        var npc = new GameObject("Trainer", typeof(NetworkObject));
        try
        {
            var presenter = player.AddComponent<TownTrainerPresenter>();
            var endpoint = new FakeEndpoint { CanMutate = false };
            presenter.BindEndpoint(endpoint, null, AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath));
            presenter.OpenForTarget(npc.GetComponent<NetworkObject>());
            Assert.That(Feedback(presenter.View), Does.Contain("Listo"));
            var buttons = presenter.View.transform.Find("TrainerPanel/CardsContainer").GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Is.Not.Empty);
            Assert.That(buttons.All(button => !button.interactable), Is.True);
            buttons[0].onClick.Invoke();
            Assert.That(endpoint.Calls, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    [Test]
    public async Task Presenter_ReservationPending_ShowsReadableSpanishMessage()
    {
        var player = new GameObject("Player");
        var npc = new GameObject("Trainer", typeof(NetworkObject));
        try
        {
            var presenter = player.AddComponent<TownTrainerPresenter>();
            var endpoint = new FakeEndpoint();
            presenter.BindEndpoint(endpoint, null, AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath));
            presenter.OpenForTarget(npc.GetComponent<NetworkObject>());
            var button = presenter.View.transform.Find("TrainerPanel/CardsContainer").GetComponentInChildren<Button>(true);
            button.onClick.Invoke();
            endpoint.Completion.SetResult(StashOperationResult.ReservationPending);
            await Task.Yield();
            Assert.That(endpoint.Calls, Is.EqualTo(1));
            Assert.That(Feedback(presenter.View), Is.EqualTo("No podés cambiar de arma mientras hay una expedición pendiente."));
            Assert.That(button.interactable, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    private static string Feedback(TownTrainerView view) =>
        view.transform.Find("TrainerPanel/Feedback").GetComponent<TMP_Text>().text;

    private sealed class FakeEndpoint : ITownEquipmentMutationEndpoint
    {
        public bool CanMutate { get; set; } = true;
        public int Calls;
        public EquipmentSlot Target;
        public readonly TaskCompletionSource<StashOperationResult> Completion = new();
        public bool CanEquip(LootId id, EquipmentSlot slot) => CanMutate;
        public StashOperationResult TryEquip(LootId id, EquipmentSlot slot) => StashOperationResult.Success;
        public StashOperationResult TryUnequip(EquipmentSlot slot) => StashOperationResult.Success;
        public Task<StashOperationResult> RequestTrainingWeaponAsync(LootId id, EquipmentSlot targetSlot = EquipmentSlot.None)
        {
            Calls++;
            Target = targetSlot;
            return Completion.Task;
        }
    }
}
#endif

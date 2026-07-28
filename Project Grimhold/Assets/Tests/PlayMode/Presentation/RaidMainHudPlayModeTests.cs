#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class RaidMainHudPlayModeTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string MeleePrefabGuid = "982f360e5acbdd344a8a75bbc0af94ec";

        private NetworkRunner _runner;
        private GameObject _inputReaderObject;
        private NetworkObject _localPlayer;
        private NetworkObject _proxyPlayer;
        private LocalPlayerJoinContext _joinContext;
        private PrimaryAttackStatusSimulationDriver _cooldownDriver;
        private PlayerCorpseGenerationSimulationDriver _defeatDriver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
            }

            if (_inputReaderObject != null)
            {
                Object.DestroyImmediate(_inputReaderObject);
            }
        }

        [Test]
        public void NetworkPlayerPrefabHasOneCompleteRaidHudOnExistingCanvas()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<RaidHudPresenter>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<RaidHudView>(true), Has.Length.EqualTo(1));

            RaidHudPresenter presenter = prefab.GetComponentInChildren<RaidHudPresenter>(true);
            RaidHudView view = prefab.GetComponentInChildren<RaidHudView>(true);
            var serializedPresenter = new SerializedObject(presenter);
            Assert.That(
                serializedPresenter.FindProperty("_view").objectReferenceValue,
                Is.SameAs(view));
            Assert.That(view.MainHudRoot, Is.Not.Null);
            Assert.That(view.MainHudRoot.name, Is.EqualTo("RaidMainHud"));
            Assert.That(view.CooldownRoot, Is.Not.Null);
            Assert.That(view.CooldownRoot.name, Is.EqualTo("RaidCooldownHud"));
            Assert.That(view.CooldownRoot.IsChildOf(view.MainHudRoot.transform), Is.False);
            Assert.That(presenter.transform.IsChildOf(view.MainHudRoot.transform), Is.False);
            AssertViewReferences(view);

            RaidInventoryView inventoryView = prefab.GetComponentInChildren<RaidInventoryView>(true);
            Assert.That(inventoryView.PlayerPanel.TotalValueText, Is.Not.Null);
            Assert.That(
                inventoryView.PlayerPanel.TotalValueText.transform.IsChildOf(
                    inventoryView.PlayerPanel.transform),
                Is.True);

            LocalPlayerHudBinder binder = prefab.GetComponent<LocalPlayerHudBinder>();
            Assert.That(binder, Is.Not.Null);
            var serializedBinder = new SerializedObject(binder);
            AssertObjectReference(serializedBinder, "_raidHudPresenter");
            AssertObjectReference(serializedBinder, "_menuPresenter");
            AssertObjectReference(serializedBinder, "_playerCharacter");
            AssertObjectReference(serializedBinder, "_combatController");

            Assert.That(prefab.GetComponentsInChildren<RaidMenuPresenter>(true), Has.Length.EqualTo(1));
            RaidMenuView menuView = prefab.GetComponentInChildren<RaidMenuView>(true);
            Assert.That(menuView, Is.Not.Null);
            Assert.That(menuView.MenuRoot, Is.Not.Null);
            Assert.That(menuView.TitleText, Is.Not.Null);
            Assert.That(menuView.StatusText, Is.Not.Null);
            Assert.That(menuView.ControlsText, Is.Not.Null);
            Assert.That(menuView.ResumeButton, Is.Not.Null);
            Assert.That(menuView.AbandonButton, Is.Not.Null);

            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                Assert.That(behaviours[index], Is.Not.Null, "NetworkPlayer.prefab contains a Missing Script.");
            }
        }

        [Test]
        public void ClearAndPresentationOperationsKeepSafeValuesAndDefeatVisible()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                RaidHudView view = instance.GetComponentInChildren<RaidHudView>(true);
                view.Clear();
                Assert.That(view.HealthText.text, Is.EqualTo("Salud: — / —"));
                Assert.That(view.ClassText.text, Is.EqualTo("Clase: —"));
                Assert.That(view.AttackText.text, Is.EqualTo("Ataque: —"));
                Assert.That(view.InventoryText.text, Is.EqualTo("Inventario: — / —"));
                Assert.That(view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
                Assert.That(view.HealthFill.fillAmount, Is.Zero);
                Assert.That(view.CooldownFill.fillAmount, Is.Zero);
                Assert.That(view.HealthFill.rectTransform.localScale.x, Is.Zero);
                Assert.That(view.CooldownFill.rectTransform.localScale.x, Is.Zero);
                Assert.That(view.DefeatedRoot.activeSelf, Is.False);

                view.PresentHealth(25f, 100f);
                view.PresentClass("Caballero");
                view.PresentAttack(false, 1.2f, 0.6f);
                view.PresentInventory(3, 16);
                view.PresentDefeated(true);

                Assert.That(view.HealthText.text, Is.EqualTo("Salud: 25 / 100"));
                Assert.That(view.HealthFill.fillAmount, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(view.HealthFill.rectTransform.localScale.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(view.ClassText.text, Is.EqualTo("Clase: Caballero"));
                Assert.That(view.AttackText.text, Is.EqualTo($"Ataque: {1.2f:0.0}s"));
                Assert.That(view.CooldownFill.fillAmount, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(view.CooldownFill.rectTransform.localScale.x, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(view.InventoryText.text, Is.EqualTo("Inventario: 3 / 16"));
                Assert.That(view.DefeatedRoot.activeSelf, Is.True);
                Assert.That(view.MainHudRoot.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [UnityTest]
        public IEnumerator SingleRunnerExercisesBindingCooldownRecoveryAndDefeat()
        {
            yield return StartRunner();

            NetworkPrefabId playerId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(MeleePrefabGuid));
            NetworkObject playerPrefab = _runner.Config.PrefabTable.Load(playerId, true);
            _localPlayer = _runner.Spawn(
                playerPrefab,
                Vector3.zero,
                Quaternion.identity,
                _runner.LocalPlayer);
            _proxyPlayer = _runner.Spawn(
                playerPrefab,
                new Vector3(3f, 0f, 0f),
                Quaternion.identity,
                inputAuthority: null);
            yield return null;

            LocalPlayerHudBinder binder = _localPlayer.GetComponent<LocalPlayerHudBinder>();
            RaidHudPresenter presenter = _localPlayer.GetComponentInChildren<RaidHudPresenter>(true);
            RaidHudView view = _localPlayer.GetComponentInChildren<RaidHudView>(true);
            RaidInventoryPresenter inventoryPresenter =
                _localPlayer.GetComponentInChildren<RaidInventoryPresenter>(true);
            RaidInventoryView inventoryView =
                _localPlayer.GetComponentInChildren<RaidInventoryView>(true);
            GameObject localHud = view.GetComponentInParent<Canvas>(true).gameObject;
            GameObject proxyHud = _proxyPlayer
                .GetComponentInChildren<RaidHudView>(true)
                .GetComponentInParent<Canvas>(true)
                .gameObject;

            Assert.That(localHud.activeSelf, Is.True);
            Assert.That(proxyHud.activeSelf, Is.False);
            Assert.That(ReadPresenterFlag(presenter, "_isBound"), Is.True);
            Assert.That(view.ClassText.text, Is.EqualTo("Clase: —"));
            Assert.That(view.HealthText.text, Is.Not.EqualTo("Salud: — / —"));
            Assert.That(view.InventoryText.text, Is.EqualTo("Inventario: 0 / 16"));
            Assert.That(inventoryView.PlayerPanel.TotalValueText.text, Is.EqualTo("Valor: 0"));
            Assert.That(view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));

            _joinContext.Initialize(new PlayerJoinData(PlayerClassId.Melee));
            yield return WaitUntil(
                () => view.ClassText.text == "Clase: Caballero",
                "The cached join context did not resolve the late class.");

            PlayerCombatNetworkController combat =
                _localPlayer.GetComponent<PlayerCombatNetworkController>();
            Assert.That(combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus readyStatus), Is.True);
            Assert.That(readyStatus.IsAvailable, Is.True);

            _cooldownDriver.Target = combat;
            _cooldownDriver.RequestedCooldownSeconds = 0.2f;
            _cooldownDriver.IsRequested = true;
            yield return WaitUntil(
                () => combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    !status.IsAvailable &&
                    status.CooldownRemainingSeconds > 0f,
                "The controller never reported the staged cooldown.");

            PropertyInfo cooldownProperty = typeof(PlayerCombatNetworkController).GetProperty(
                "AttackCooldown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo sequenceProperty = typeof(PlayerCombatNetworkController).GetProperty(
                "AttackSequence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cooldownProperty, Is.Not.Null);
            Assert.That(sequenceProperty, Is.Not.Null);
            TickTimer timerBefore = (TickTimer)cooldownProperty.GetValue(combat);
            int sequenceBefore = (int)sequenceProperty.GetValue(combat);

            Assert.That(combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus coolingStatus), Is.True);

            Assert.That(coolingStatus.IsAvailable, Is.False);
            Assert.That((TickTimer)cooldownProperty.GetValue(combat), Is.EqualTo(timerBefore));
            Assert.That((int)sequenceProperty.GetValue(combat), Is.EqualTo(sequenceBefore));
            Assert.That(view.CooldownFill.fillAmount, Is.InRange(0f, 1f));

            yield return WaitUntil(
                () => combat.TryGetPrimaryAttackStatus(out PrimaryAttackStatus status) &&
                    status.IsAvailable,
                "The primary attack did not become available when its cooldown expired.");

            PlayerLootReceiver receiver = _localPlayer.GetComponent<PlayerLootReceiver>();
            FieldInfo catalogField = typeof(PlayerLootReceiver).GetField(
                "_lootCatalog",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(catalogField, Is.Not.Null);
            object catalog = catalogField.GetValue(receiver);
            catalogField.SetValue(receiver, null);
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "RaidInventoryPresenter could not read a complete loot value. The inventory will retry without showing a subtotal.");
            inventoryPresenter.enabled = false;
            inventoryPresenter.enabled = true;
            Assert.That(inventoryView.PlayerPanel.TotalValueText.text, Is.EqualTo("Valor: —"));
            Assert.That(ReadFlag(inventoryPresenter, "_playerValueRefreshPending"), Is.True);
            yield return null;
            yield return null;
            Assert.That(inventoryView.PlayerPanel.TotalValueText.text, Is.EqualTo("Valor: —"));

            catalogField.SetValue(receiver, catalog);
            yield return WaitUntil(
                () => inventoryView.PlayerPanel.TotalValueText.text == "Valor: 0" &&
                    !ReadFlag(inventoryPresenter, "_playerValueRefreshPending"),
                "The inventory did not recover its pending loot value read.");
            inventoryView.PlayerPanel.TotalValueText.havePropertiesChanged = false;
            yield return null;
            yield return null;
            Assert.That(inventoryView.PlayerPanel.TotalValueText.havePropertiesChanged, Is.False);

            int listenerCountBefore = CountReaderChangedListeners(
                _runner.GetComponent<LocalInputContext>());
            binder.enabled = false;
            yield return null;
            Assert.That(localHud.activeSelf, Is.False);
            Assert.That(view.ClassText.text, Is.EqualTo("Clase: —"));
            binder.enabled = true;
            yield return WaitUntil(
                () => localHud.activeSelf && view.ClassText.text == "Clase: Caballero",
                "The local HUD did not rebind after re-enable.");
            Assert.That(
                CountReaderChangedListeners(_runner.GetComponent<LocalInputContext>()),
                Is.EqualTo(listenerCountBefore));

            PlayerCharacter character = _localPlayer.GetComponent<PlayerCharacter>();
            _defeatDriver.Target = character;
            _defeatDriver.Receiver = receiver;
            _defeatDriver.IsRequested = true;
            yield return WaitUntil(
                () => !character.IsAlive && view.DefeatedRoot.activeSelf,
                "Defeat was not presented by the raid HUD.");
            Assert.That(localHud.activeSelf, Is.True);
            Assert.That(view.MainHudRoot.activeSelf, Is.True);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("RaidMainHudSingleRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            LocalInputContext inputContext = runnerObject.AddComponent<LocalInputContext>();
            _joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
            _cooldownDriver = runnerObject.AddComponent<PrimaryAttackStatusSimulationDriver>();
            _defeatDriver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();

            _inputReaderObject = new GameObject("RaidMainHudInputReader");
            PlayerInputReader inputReader = _inputReaderObject.AddComponent<PlayerInputReader>();
            Assert.That(inputContext.TryRegister(inputReader), Is.True);

            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"raid-main-hud-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            int framesRemaining = 180;
            while (!predicate() && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private static int CountReaderChangedListeners(LocalInputContext context)
        {
            FieldInfo field = typeof(LocalInputContext).GetField(
                "ReaderChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var callbacks = field.GetValue(context) as Delegate;
            return callbacks?.GetInvocationList().Length ?? 0;
        }

        private static bool ReadPresenterFlag(RaidHudPresenter presenter, string fieldName)
        {
            return ReadFlag(presenter, fieldName);
        }

        private static bool ReadFlag(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(owner);
        }

        private static void AssertViewReferences(RaidHudView view)
        {
            Assert.That(view.HealthText, Is.Not.Null);
            Assert.That(view.HealthFill, Is.Not.Null);
            Assert.That(view.ClassText, Is.Not.Null);
            Assert.That(view.AttackText, Is.Not.Null);
            Assert.That(view.CooldownRoot, Is.Not.Null);
            Assert.That(view.CooldownFill, Is.Not.Null);
            Assert.That(view.InventoryText, Is.Not.Null);
            Assert.That(view.ExtractionText, Is.Not.Null);
            Assert.That(view.DefeatedRoot, Is.Not.Null);
        }

        private static void AssertObjectReference(SerializedObject owner, string propertyName)
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.objectReferenceValue, Is.Not.Null);
        }
    }
}
#endif

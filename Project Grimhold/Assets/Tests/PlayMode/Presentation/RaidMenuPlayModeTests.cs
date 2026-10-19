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
    public sealed class RaidMenuPlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";

        private NetworkRunner _runner;
        private GameObject _inputReaderObject;
        private PlayerInputReader _inputReader;
        private NetworkObject _playerObject;
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

        [UnityTest]
        public IEnumerator RaidMenu_OpenCloseAndAbandon_OperatesCleanlyAndShutsDown()
        {
            yield return StartRunnerAndSpawnPlayer();

            RaidMenuPresenter menuPresenter = _playerObject.GetComponentInChildren<RaidMenuPresenter>(true);
            RaidMenuView menuView = _playerObject.GetComponentInChildren<RaidMenuView>(true);

            Assert.That(menuPresenter, Is.Not.Null, "RaidMenuPresenter missing on player hierarchy.");
            Assert.That(menuView, Is.Not.Null, "RaidMenuView missing on player hierarchy.");

            Assert.That(menuPresenter.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);

            menuPresenter.OpenMenu();
            yield return null;

            Assert.That(menuPresenter.IsOpen, Is.True);
            Assert.That(menuView.IsOpen, Is.True);
            Assert.That(menuView.TitleText.text, Is.EqualTo("Menú de Incursión"));
            Assert.That(ReadSuppressionCount(_inputReader), Is.EqualTo(1));

            menuPresenter.CloseMenu();
            yield return null;

            Assert.That(menuPresenter.IsOpen, Is.False);
            Assert.That(menuView.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.Zero);

            var abandonTask = menuPresenter.AbandonRaidAsync();
            while (!abandonTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(abandonTask.IsCompletedSuccessfully, Is.True);
            Assert.That(_runner == null || !_runner.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator RaidMenu_DefeatedPlayer_DisplaysDefeatAndPreservesInputSuppressionOnClose()
        {
            yield return StartRunnerAndSpawnPlayer();

            PlayerCharacter character = _playerObject.GetComponent<PlayerCharacter>();
            PlayerLootReceiver receiver = _playerObject.GetComponent<PlayerLootReceiver>();
            RaidMenuPresenter menuPresenter = _playerObject.GetComponentInChildren<RaidMenuPresenter>(true);
            RaidMenuView menuView = _playerObject.GetComponentInChildren<RaidMenuView>(true);

            _defeatDriver.Target = character;
            _defeatDriver.Receiver = receiver;
            _defeatDriver.IsRequested = true;

            yield return WaitUntil(
                () => !character.IsAlive,
                "Character defeat simulation failed to trigger.");

            yield return null;

            Assert.That(menuPresenter.IsOpen, Is.True);
            Assert.That(menuView.TitleText.text, Is.EqualTo("DERROTA"));
            Assert.That(menuView.ResumeButton.gameObject.activeSelf, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.GreaterThan(0));

            menuPresenter.CloseMenu();
            yield return null;

            Assert.That(menuPresenter.IsOpen, Is.False);
            Assert.That(ReadSuppressionCount(_inputReader), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator RaidMenu_ResultsLootAndLevelUp_ArePresentedFromTheSerializedPrefab()
        {
            yield return StartRunnerAndSpawnPlayer();

            RaidMenuView menuView = _playerObject.GetComponentInChildren<RaidMenuView>(true);
            LootDefinitionCatalog catalog = _playerObject.GetComponent<PlayerLootReceiver>().LootCatalog;
            Assert.That(catalog.TryGetByIndex(0, out LootDefinition first), Is.True);
            Assert.That(catalog.TryGetByIndex(1, out LootDefinition second), Is.True);

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    new[] { new LootEntry(first.LootId, 2), new LootEntry(second.LootId, 1) },
                    out RaidResultsLootSummary lost,
                    out string error),
                Is.True,
                error);

            menuView.PresentResultsLoot(lost, catalog);
            Transform grid = FindChild(menuView.transform, "LootGrid");
            Assert.That(CountActiveChildren(grid), Is.EqualTo(2));
            Assert.That(
                FindChild(menuView.transform, "LootHeaderText").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("BOTÍN PERDIDO"));
            Assert.That(FindChild(menuView.transform, "LootValueText").gameObject.activeSelf, Is.False);

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    new LootEntry[0], out RaidResultsLootSummary empty, out error),
                Is.True,
                error);
            menuView.PresentResultsLoot(empty, catalog);
            Assert.That(CountActiveChildren(grid), Is.Zero);
            Assert.That(FindChild(menuView.transform, "LootEmptyText").gameObject.activeSelf, Is.True);

            menuView.ClearResultsLoot();
            Assert.That(FindChild(menuView.transform, "ResultLootPanel").gameObject.activeSelf, Is.False);

            menuView.PresentLevelUp(5, animate: true);
            Transform banner = FindChild(menuView.transform, "LevelUpBanner");
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(banner.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("¡NIVEL 5!"));
            menuView.HideLevelUp();
            Assert.That(banner.gameObject.activeSelf, Is.False);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail($"'{name}' was not found under the raid menu view.");
            return null;
        }

        private static int CountActiveChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator StartRunnerAndSpawnPlayer()
        {
            var runnerObject = new GameObject("RaidMenuPlayModeRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _defeatDriver = runnerObject.AddComponent<PlayerCorpseGenerationSimulationDriver>();
            LocalInputContext inputContext = runnerObject.AddComponent<LocalInputContext>();

            _inputReaderObject = new GameObject("RaidMenuPlayModeInputReader");
            _inputReader = _inputReaderObject.AddComponent<PlayerInputReader>();
            Assert.That(inputContext.TryRegister(_inputReader), Is.True);

            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"raid-menu-test-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());

            NetworkPrefabId playerId = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PlayerPrefabGuid));
            NetworkObject playerPrefab = _runner.Config.PrefabTable.Load(playerId, true);

            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.");
            _playerObject = _runner.Spawn(
                playerPrefab,
                Vector3.zero,
                Quaternion.identity,
                _runner.LocalPlayer);

            yield return null;
            Assert.That(_playerObject, Is.Not.Null);
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

        private static int ReadSuppressionCount(PlayerInputReader reader)
        {
            FieldInfo field = typeof(PlayerInputReader).GetField(
                "_gameplaySuppressionCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(reader);
        }
    }
}
#endif

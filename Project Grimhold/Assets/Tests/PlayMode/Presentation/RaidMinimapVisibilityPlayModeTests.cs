#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class RaidMinimapVisibilityPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string SanctuaryPath = "Assets/Prefabs/ExtractionSanctuary.prefab";
        private const string GameplayMinimapLayoutPath = "Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset";

        private NetworkRunner _runner;
        private EntityRegistry _registry;
        private ExtractionSanctuaryAssignmentService _assignments;
        private ExtractionProgressSimulationDriver _driver;
        private NetworkObject _avatar;
        private ExtractionSanctuary _sanctuary;
        private PlayerExtractionProgressController _progress;
        private PlayerExtractionController _extraction;
        private RaidMinimapPresenter _presenter;
        private RaidMinimapView _view;
        private Image _localMarker;
        private Image _icon;
        private Image _arrow;
        private RectTransform _map;
        private CanvasGroup _visibility;
        private GameObject _inputOwner;
        private GameObject _nextSessionOwner;
        private GameObject _sceneMinimap;
        private RaidTeamId _firstTeam;
        private RaidTeamId _secondTeam;
        private int _authoredImageCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(RaidTeamId.TryCreate(1, out _firstTeam), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out _secondTeam), Is.True);
            var runnerOwner = new GameObject("MinimapVisibilitySingleRunner");
            _runner = runnerOwner.AddComponent<NetworkRunner>();
            _registry = runnerOwner.AddComponent<EntityRegistry>();
            LocalInputContext inputContext = PlayModeRunnerComposition.AddLocalInputContext(_runner);
            _inputOwner = new GameObject("MinimapVisibilityInputReader");
            Assert.That(inputContext.TryRegister(_inputOwner.AddComponent<PlayerInputReader>()), Is.True);
            _assignments = runnerOwner.AddComponent<ExtractionSanctuaryAssignmentService>();
            Assert.That(_assignments.Initialize(_runner, GameMode.Host), Is.True);
            // Reuse the runner-local fixture pool rather than composing unrelated match directors.
            PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
            _driver = runnerOwner.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"minimap-visibility-{Guid.NewGuid():N}",
                SceneManager = runnerOwner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerOwner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId participantId), Is.True);
            NetworkObject participant = _runner.Spawn(
                Load(ParticipantGuid), Vector3.zero, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkRaidParticipant>().Initialize(
                    "11111111111111111111111111111111", participantId,
                    ProgressionBalanceDefaults.InitialCharacterAttributeState,
                    ExperienceCurve.InitialLevel, 0, "minimap-visibility-generation"));
            _avatar = _runner.Spawn(
                Load(PlayerGuid), Vector3.zero, Quaternion.identity, inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                {
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant);
                    // Bind the minimap explicitly; other HUD sections are outside this fixture.
                    instance.GetComponent<LocalPlayerHudBinder>().enabled = false;
                });
            Assert.That(participant.GetComponent<NetworkRaidParticipant>().TrySetCurrentAvatar(_avatar), Is.True);
            _runner.SetPlayerObject(_runner.LocalPlayer, participant);
            participant.AssignInputAuthority(_runner.LocalPlayer);
            _avatar.AssignInputAuthority(_runner.LocalPlayer);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, _avatar);
            _progress = _avatar.GetComponent<PlayerExtractionProgressController>();
            _extraction = _avatar.GetComponent<PlayerExtractionController>();
            _sanctuary = _runner.Spawn(
                Load(AssetDatabase.AssetPathToGUID(SanctuaryPath)), Vector3.right * 2f,
                Quaternion.identity, inputAuthority: null).GetComponent<ExtractionSanctuary>();
            _view = _avatar.GetComponentInChildren<RaidMinimapView>(true);
            _presenter = _avatar.GetComponentInChildren<RaidMinimapPresenter>(true);
            _avatar.transform.Find("LocalGameplayHud").gameObject.SetActive(true);
            _localMarker = ReadField<Image>(_view, "_localMarker");
            _icon = ReadField<Image>(_view, "_sanctuaryIcon");
            _arrow = ReadField<Image>(_view, "_sanctuaryArrow");
            _map = ReadField<RectTransform>(_view, "_mapRect");
            _visibility = _view.GetComponent<CanvasGroup>();
            _authoredImageCount = _view.GetComponentsInChildren<Image>(true).Length;
            Assert.That(_view.TryValidateConfiguration(out string error), Is.True, error);
            // The HUD reads the layout declared by the loaded Raid scene.
            _sceneMinimap = new GameObject("RaidSceneMinimap", typeof(RaidSceneMinimap));
            _sceneMinimap.GetComponent<RaidSceneMinimap>().EditorConfigure(
                AssetDatabase.LoadAssetAtPath<MinimapLayout>(GameplayMinimapLayoutPath), null);
            BindCurrentPlayer();
            yield return NextPresentation();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _presenter?.Unbind();
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted)
                {
                    yield return null;
                }
            }

            if (_runner != null) Object.DestroyImmediate(_runner.gameObject);
            if (_inputOwner != null) Object.DestroyImmediate(_inputOwner);
            if (_nextSessionOwner != null) Object.DestroyImmediate(_nextSessionOwner);
            if (_sceneMinimap != null) Object.DestroyImmediate(_sceneMinimap);
        }

        [UnityTest]
        public IEnumerator QuotaAndAssignmentRevealIconAndArrowBeforeRitualCompletion()
        {
            Assert.That(_progress.TryGetSnapshot(out ExtractionProgressSnapshot beforeQuota), Is.True);
            Assert.That(beforeQuota.IsQuotaComplete, Is.False);
            AssertMarkers(local: true, icon: false, arrow: false);
            yield return CompleteQuota();
            Assert.That(_progress.TryGetSnapshot(out ExtractionProgressSnapshot quota), Is.True);
            Assert.That(quota.IsQuotaComplete, Is.True);
            Assert.That(_assignments.TryGetAssignment(_firstTeam).Success, Is.False);
            AssertMarkers(local: true, icon: false, arrow: false);

            yield return AssignSanctuary(_firstTeam);
            Assert.That(_sanctuary.TryGetRitualProgress(_firstTeam, out ExtractionRitualSnapshot ritual), Is.True);
            Assert.That(ritual.State, Is.EqualTo(ExtractionRitualState.NotStarted));
            Assert.That(_sanctuary.CanUseExtraction(_firstTeam), Is.False);
            AssertMarkers(local: true, icon: true, arrow: false);
            Assert.That(_icon.rectTransform.anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(_icon.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));

            _sanctuary.transform.position = new Vector3(1000f, 1000f);
            yield return NextPresentation();
            AssertMarkers(local: true, icon: false, arrow: true);
            Assert.That(_arrow.rectTransform.anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(_arrow.rectTransform.anchoredPosition.y, Is.GreaterThan(0f));
            float authoredArrowCorrection = ReadField<float>(_presenter, "_arrowBaseAngleCorrection");
            Assert.That(Mathf.DeltaAngle(_arrow.rectTransform.localEulerAngles.z, 45f + authoredArrowCorrection),
                Is.EqualTo(0f).Within(0.01f));
            // Fusion may overwrite direct avatar Transform edits on the next tick. Exercise
            // the presenter's public observation seam with an owned, stable Transform instead.
            _inputOwner.transform.SetPositionAndRotation(_avatar.transform.position, Quaternion.identity);
            _presenter.Bind(_runner, _avatar, _inputOwner.transform, _extraction,
                _assignments, _registry, _progress);
            yield return NextPresentation();
            Vector2 previousMapPosition = _map.anchoredPosition;
            _inputOwner.transform.SetPositionAndRotation(_inputOwner.transform.position + Vector3.up,
                Quaternion.Euler(0f, 0f, 90f));
            yield return NextPresentation();
            float projectedStep = ReadField<float>(_presenter, "_uiUnitsPerWorldUnit") *
                ReadField<float>(_presenter, "_zoom");
            Assert.That(_map.anchoredPosition.y, Is.EqualTo(previousMapPosition.y - projectedStep).Within(0.001f));
            Assert.That(_map.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_localMarker.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(_localMarker.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [UnityTest]
        public IEnumerator SceneEdgeCellsAlignWithCenteredPlayerWithoutApplyingLegacyOffset()
        {
            MinimapLayout layout = _map.GetComponent<RaidMinimapGraphic>().Layout;
            Vector2Int[] edges = new Vector2Int[4];
            bool found = false;
            for (int y = 0; y < layout.SizeInCells.y; y++)
            {
                for (int x = 0; x < layout.SizeInCells.x; x++)
                {
                    Vector2Int cell = layout.MinimumCell + new Vector2Int(x, y);
                    if ((layout.GetCellFlags(cell) & MinimapLayoutCellFlags.Floor) == 0) continue;
                    if (!found)
                    {
                        for (int i = 0; i < edges.Length; i++) edges[i] = cell;
                        found = true;
                    }
                    if (cell.x < edges[0].x) edges[0] = cell;
                    if (cell.x > edges[1].x) edges[1] = cell;
                    if (cell.y < edges[2].y) edges[2] = cell;
                    if (cell.y > edges[3].y) edges[3] = cell;
                }
            }
            Assert.That(found, Is.True);
            _presenter.Bind(_runner, _avatar, _inputOwner.transform, _extraction,
                _assignments, _registry, _progress);
            float scale = ReadField<float>(_presenter, "_uiUnitsPerWorldUnit") * ReadField<float>(_presenter, "_zoom");
            Canvas canvas = _view.GetComponentInParent<Canvas>();
            float pixelTolerance = 0.501f / canvas.scaleFactor;
            foreach (Vector2Int edge in edges)
            {
                Vector2 fromMinimum = edge - layout.MinimumCell;
                Vector2 playerWorld = layout.WorldPivotPosition - layout.WorldSize * 0.5f +
                    Vector2.Scale(fromMinimum + Vector2.one * 0.5f, layout.CellSize);
                _inputOwner.transform.position = playerWorld;
                yield return NextPresentation();
                Vector2 expected = (layout.WorldPivotPosition - playerWorld) * scale;
                Assert.That(_map.anchoredPosition.x, Is.EqualTo(expected.x).Within(pixelTolerance), edge.ToString());
                Assert.That(_map.anchoredPosition.y, Is.EqualTo(expected.y).Within(pixelTolerance), edge.ToString());
                Assert.That(_localMarker.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
        }

        [UnityTest]
        public IEnumerator AnotherTeamsAssignmentIsNeverPresentedToTheLocalPlayer()
        {
            yield return CompleteQuota();
            yield return AssignSanctuary(_firstTeam);
            AssertMarkers(local: true, icon: true, arrow: false);
            SetTeamOverride(_secondTeam);
            yield return NextPresentation();
            Assert.That(_assignments.TryGetAssignment(_firstTeam).Success, Is.True);
            Assert.That(_assignments.TryGetAssignment(_secondTeam).Success, Is.False);
            AssertMarkers(local: true, icon: false, arrow: false);
            _sanctuary.transform.position = Vector3.right * 1000f;
            yield return NextPresentation();
            AssertMarkers(local: true, icon: false, arrow: false);
        }

        [UnityTest]
        public IEnumerator AssignmentLossRemovesBothMarkersWithoutHidingTheLocalPlayer()
        {
            yield return CompleteQuota();
            yield return AssignSanctuary(_firstTeam);
            _sanctuary.transform.position = Vector3.left * 1000f;
            yield return NextPresentation();
            AssertMarkers(local: true, icon: false, arrow: true);
            _runner.Despawn(_sanctuary.Object);
            yield return NextPresentation();
            Assert.That(_assignments.TryGetAssignment(_firstTeam).Success, Is.False);
            AssertMarkers(local: true, icon: false, arrow: false);
        }

        [UnityTest]
        public IEnumerator ExtractedPlayerLosesExtractionMarkerButKeepsCenteredLocalMarker()
        {
            yield return CompleteQuota();
            yield return AssignSanctuary(_firstTeam);
            AssertMarkers(local: true, icon: true, arrow: false);
            // This fixture owns presentation, not extraction persistence. Release its owned
            // saver through the normal subscription lifecycle before firing completion, and
            // stop its simulation callbacks from retrying an intentionally uncomposed commit.
            PlayerExtractionLootSaver lootSaver = _avatar.GetComponent<PlayerExtractionLootSaver>();
            Assert.That(lootSaver, Is.Not.Null);
            lootSaver.enabled = false;
            lootSaver.Despawned(_runner, true);
            yield return Execute(_ => typeof(PlayerExtractionController).GetMethod(
                "CompleteExtraction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_extraction, null));
            yield return NextPresentation();
            Assert.That(_extraction.State, Is.EqualTo(ExtractionState.Extracted));
            AssertMarkers(local: true, icon: false, arrow: false);
        }

        [UnityTest]
        public IEnumerator ReenableUnbindAndNewSessionClearPreviousAssignmentPresentation()
        {
            yield return CompleteQuota();
            yield return AssignSanctuary(_firstTeam);
            AssertMarkers(local: true, icon: true, arrow: false);
            _presenter.enabled = false;
            AssertMarkers(local: false, icon: false, arrow: false);
            _presenter.enabled = true;
            AssertMarkers(local: false, icon: false, arrow: false);
            yield return NextPresentation();
            AssertMarkers(local: true, icon: true, arrow: false);

            _nextSessionOwner = new GameObject("MinimapVisibilityNextSession");
            NetworkRunner nextRunner = _nextSessionOwner.AddComponent<NetworkRunner>();
            _presenter.Bind(nextRunner, null, null, null, null, null);
            AssertMarkers(local: false, icon: false, arrow: false);
            yield return NextPresentation();
            AssertMarkers(local: false, icon: false, arrow: false);
            BindCurrentPlayer();
            yield return NextPresentation();
            AssertMarkers(local: true, icon: true, arrow: false);
            _presenter.Unbind();
            yield return NextPresentation();
            AssertMarkers(local: false, icon: false, arrow: false);
        }

        [UnityTest]
        public IEnumerator MissingPlayerObjectBindingCannotPresentAnyPlayerOrExtractionMarker()
        {
            yield return CompleteQuota();
            yield return AssignSanctuary(_firstTeam);
            _sanctuary.transform.position = Vector3.left * 1000f;
            yield return NextPresentation();
            AssertMarkers(local: true, icon: false, arrow: true);
            // A session/HUD transition can have no current avatar. Single mode cannot prove
            // Host/Client authority loss: RemoveInputAuthority is a Host/Server operation.
            _presenter.Bind(_runner, null, _avatar.transform, _extraction,
                _assignments, _registry, _progress);
            AssertMarkers(local: false, icon: false, arrow: false);
            yield return NextPresentation();
            AssertMarkers(local: false, icon: false, arrow: false);
            BindCurrentPlayer();
            yield return NextPresentation();
            AssertMarkers(local: true, icon: false, arrow: true);
        }

        private void BindCurrentPlayer()
        {
            _presenter.Bind(_runner, _avatar, _avatar.transform, _extraction,
                _assignments, _registry, _progress);
        }

        private IEnumerator CompleteQuota()
        {
            yield return Execute(runner => Assert.That(_progress.TryApplyContribution(
                new ExtractionProgressContribution(ExtractionProgressSourceType.LootFirstAcquisition,
                    new EntityId(8001), long.MaxValue, runner.Tick)), Is.True));
            yield return NextPresentation();
        }

        private IEnumerator AssignSanctuary(RaidTeamId team)
        {
            // The assignment service reads the real authoritative sanctuary reservation. Quota
            // selection is covered by ExtractionProgressPlayModeTests; this fixture drives its
            // existing reservation seam to isolate presentation from match-director simulation.
            yield return Execute(_ => Assert.That(_sanctuary.TryAssignTeam(team), Is.True));
            Assert.That(_assignments.TryGetAssignment(team).SanctuaryId, Is.EqualTo(_sanctuary.Id));
            yield return NextPresentation();
        }

        private void SetTeamOverride(RaidTeamId team)
        {
            MethodInfo method = typeof(PlayerExtractionProgressController).GetMethod(
                "SetTeamIdOverrideForTesting", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_progress, new object[] { team });
        }

        private void AssertMarkers(bool local, bool icon, bool arrow)
        {
            Assert.That(_localMarker.enabled, Is.EqualTo(local), "Only the current local player is presented.");
            Assert.That(_icon.enabled, Is.EqualTo(icon), "The private interior extraction marker must not leak.");
            Assert.That(_arrow.enabled, Is.EqualTo(arrow), "The private edge extraction marker must not leak.");
            Assert.That(_visibility.alpha, Is.EqualTo(local ? 1f : 0f));
            Assert.That(_view.GetComponentsInChildren<Image>(true), Has.Length.EqualTo(_authoredImageCount),
                "Tracking must not create markers for other runtime entities.");
        }

        private IEnumerator Execute(Action<NetworkRunner> action)
        {
            _driver.ClearException();
            _driver.PendingAction = action;
            int frames = 120;
            while (_driver.PendingAction != null && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(_driver.PendingAction, Is.Null, "The fixture requires an authoritative simulation tick.");
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private static IEnumerator NextPresentation()
        {
            yield return null;
            yield return null;
        }

        private NetworkObject Load(string guid)
        {
            Assert.That(guid, Is.Not.Empty);
            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private static T ReadField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(owner);
        }
    }
}
#endif

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    public sealed class RaidMissionTrackerPresenterTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";

        private MissionDefinitionCatalog _catalog;
        private InMemoryLocalProfileRepository _repository;
        private ProfileId _profileId;
        private LocalProfileStore _store;
        private GameObject _presenterGo;
        private RaidMissionTrackerPresenter _presenter;
        private GameObject _viewGo;
        private RaidMissionTrackerView _view;
        private GameObject _popupGo;
        private TextMeshProUGUI _popupHeader;
        private TextMeshProUGUI _popupTitle;
        private TextMeshProUGUI _popupDetail;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(_catalog, Is.Not.Null);

            _profileId = new ProfileId("test-tracker-presenter");
            _repository = new InMemoryLocalProfileRepository();
            var lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _repository.Initialize(_profileId, lootCatalog);

            var initialSnapshot = new LocalProfileSnapshot
            {
                ProfileId = _profileId,
                GuildReputation = 0
            };
            _repository.TrySave(initialSnapshot, out _);

            _store = new LocalProfileStore(_repository, _profileId, missionCatalog: _catalog);

            _viewGo = new GameObject("TestTrackerView");
            _view = _viewGo.AddComponent<RaidMissionTrackerView>();

            _popupGo = new GameObject("PopupRoot");
            _popupGo.transform.SetParent(_viewGo.transform, false);
            _popupGo.SetActive(false);

            _popupHeader = new GameObject("Header").AddComponent<TextMeshProUGUI>();
            _popupHeader.transform.SetParent(_popupGo.transform, false);

            _popupTitle = new GameObject("Title").AddComponent<TextMeshProUGUI>();
            _popupTitle.transform.SetParent(_popupGo.transform, false);

            _popupDetail = new GameObject("Detail").AddComponent<TextMeshProUGUI>();
            _popupDetail.transform.SetParent(_popupGo.transform, false);

            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(RaidMissionTrackerView).GetField("_popupRoot", flags)?.SetValue(_view, _popupGo);
            typeof(RaidMissionTrackerView).GetField("_popupHeaderText", flags)?.SetValue(_view, _popupHeader);
            typeof(RaidMissionTrackerView).GetField("_popupTitleText", flags)?.SetValue(_view, _popupTitle);
            typeof(RaidMissionTrackerView).GetField("_popupDetailText", flags)?.SetValue(_view, _popupDetail);

            _presenterGo = new GameObject("TestTrackerPresenter");
            _presenter = _presenterGo.AddComponent<RaidMissionTrackerPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Unbind();
            if (_viewGo != null) Object.DestroyImmediate(_viewGo);
            if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
        }

        [Test]
        public void Tracker_WhenNoActiveMissions_PresentsEmpty()
        {
            _presenter.Bind(_store, _catalog, _view);
            Assert.That(_presenter.IsBound, Is.True);
        }

        [Test]
        public void Tracker_WhenMissionsAccepted_PresentsOnlyActiveMissions()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            Assert.That(_catalog.TryGet("CON_E_002", out var m2), Is.True);

            _store.TryAcceptMission(m1);
            _store.TryAcceptMission(m2);

            _presenter.Bind(_store, _catalog, _view);

            var active = _store.GetActiveMissions();
            Assert.That(active.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tracker_ReactivelyUpdatesProgress_WhenContributionEventApplied()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _store.TryAcceptMission(m1);

            _presenter.Bind(_store, _catalog, _view);

            // Apply PvE contribution (kill 1 creature, requires 3)
            var ev = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 1, _profileId);
            _store.TryApplyMissionProgress(ev);

            var active = _store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(1));
            Assert.That(active[0].State, Is.EqualTo(MissionState.Activa));

            // Complete the remaining 2 kills
            var evComplete = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 2, _profileId);
            _store.TryApplyMissionProgress(evComplete);

            active = _store.GetActiveMissions();
            Assert.That(active[0].ObjectiveProgress[0].CurrentAmount, Is.EqualTo(3));
            Assert.That(active[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
        }

        [Test]
        public void Tracker_WhenObjectiveAdvances_TriggersProgressPopup()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _store.TryAcceptMission(m1);

            _presenter.Bind(_store, _catalog, _view);

            var ev = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 1, _profileId);
            _store.TryApplyMissionProgress(ev);

            Assert.That(_view.IsPopupActive, Is.True);
            Assert.That(_popupHeader.text, Does.Contain("PROGRESO"));
            Assert.That(_popupTitle.text, Is.EqualTo(m1.Title));
            Assert.That(_popupDetail.text, Does.Contain("1/3"));
        }

        [Test]
        public void Tracker_WhenMissionCompleted_TriggersCompletionPopup()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _store.TryAcceptMission(m1);

            _presenter.Bind(_store, _catalog, _view);

            var ev = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 3, _profileId);
            _store.TryApplyMissionProgress(ev);

            Assert.That(_view.IsPopupActive, Is.True);
            Assert.That(_popupHeader.text, Does.Contain("COMPLETADO"));
            Assert.That(_popupTitle.text, Is.EqualTo(m1.Title));
        }

        [Test]
        public void Tracker_Collapsible_TogglesStateCorrectly()
        {
            _presenter.Bind(_store, _catalog, _view);
            Assert.That(_view.IsCollapsed, Is.False);

            _view.ToggleCollapse();
            Assert.That(_view.IsCollapsed, Is.True);

            _view.ToggleCollapse();
            Assert.That(_view.IsCollapsed, Is.False);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Missions
{
    public sealed class TownMissionGuildPresenterTests
    {
        private const string CatalogAssetPath = "Assets/Scriptable Objects/Missions/MissionDefinitionCatalog.asset";

        private MissionDefinitionCatalog _catalog;
        private InMemoryLocalProfileRepository _repository;
        private ProfileId _profileId;
        private LocalProfileStore _store;
        private GameObject _presenterGo;
        private TownMissionGuildPresenter _presenter;
        private GameObject _viewGo;
        private TownMissionGuildView _view;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<MissionDefinitionCatalog>(CatalogAssetPath);
            Assert.That(_catalog, Is.Not.Null);

            _profileId = new ProfileId("test-guild-presenter");
            _repository = new InMemoryLocalProfileRepository();
            var lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _repository.Initialize(_profileId, lootCatalog);

            var initialSnapshot = new LocalProfileSnapshot
            {
                ProfileId = _profileId,
                GuildReputation = 0
            };
            _repository.TrySave(initialSnapshot, out _);

            _store = new LocalProfileStore(_repository, _profileId, missionCatalog: _catalog, stashCapacity: 2);

            _presenterGo = new GameObject("TestPresenter");
            _presenter = _presenterGo.AddComponent<TownMissionGuildPresenter>();
            _presenter.BindProfile(_store);

            _viewGo = new GameObject("TestView");
            _view = _viewGo.AddComponent<TownMissionGuildView>();
            var feedbackGo = new GameObject("FeedbackText").AddComponent<TMPro.TextMeshProUGUI>();
            feedbackGo.transform.SetParent(_viewGo.transform, false);
            typeof(TownMissionGuildView).GetField("_feedbackText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_view, feedbackGo);
            _presenter.SetView(_view);
        }

        [TearDown]
        public void TearDown()
        {
            if (_viewGo != null) Object.DestroyImmediate(_viewGo);
            if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
        }

        [Test]
        public void Presenter_Refresh_PresentsRankEAvailableMissions_InAvailableTab()
        {
            _view.SetTab(GuildViewTab.Available);
            _presenter.Refresh();

            // All 6 Rank E missions should be available initially
            var active = _store.GetActiveMissions();
            Assert.That(active.Count, Is.EqualTo(0));
            Assert.That(_view.ActiveTab, Is.EqualTo(GuildViewTab.Available));
        }

        [Test]
        public void Presenter_AcceptMission_AllowsUpToThree_AndRejectsFourth()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            Assert.That(_catalog.TryGet("CON_E_002", out var m2), Is.True);
            Assert.That(_catalog.TryGet("CON_E_003", out var m3), Is.True);
            Assert.That(_catalog.TryGet("CON_E_004", out var m4), Is.True);

            // Accept 1st
            _presenter.OnAcceptRequested(m1);
            Assert.That(_store.GetActiveMissions().Count, Is.EqualTo(1));

            // Accept 2nd
            _presenter.OnAcceptRequested(m2);
            Assert.That(_store.GetActiveMissions().Count, Is.EqualTo(2));

            // Accept 3rd
            _presenter.OnAcceptRequested(m3);
            Assert.That(_store.GetActiveMissions().Count, Is.EqualTo(3));

            // Attempt 4th - rejected by limit of 3
            _presenter.OnAcceptRequested(m4);
            Assert.That(_store.GetActiveMissions().Count, Is.EqualTo(3), "4th contract must be rejected by max simultaneous limit.");
        }

        [Test]
        public void Presenter_AbandonMission_TransitionsActiveMissionToAbandonada()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _presenter.OnAcceptRequested(m1);
            Assert.That(_store.GetActiveMissions().Count, Is.EqualTo(1));
            Assert.That(_store.GetActiveMissions()[0].State, Is.EqualTo(MissionState.Activa));

            _presenter.OnAbandonRequested(m1);
            var active = _store.GetActiveMissions();
            Assert.That(active[0].State, Is.EqualTo(MissionState.Abandonada));
        }

        [Test]
        public void Presenter_ClaimMission_RejectsAndWarns_WhenStashIsFull_WithoutLosingMission()
        {
            // Setup contract that gives item reward
            var itemMission = ScriptableObject.CreateInstance<MissionDefinition>();
            typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(itemMission, "m_item_reward");
            itemMission.Title = "Item Reward Contract";
            itemMission.Rewards = new List<RewardDefinition>
            {
                new RewardDefinition { Type = RewardDefinition.RewardType.Item, Amount = 1, ReferenceId = "MAT_IRON" }
            };

            _presenter.OnAcceptRequested(itemMission);

            // Complete mission to PendienteDeReclamar
            var current = _repository.Snapshot.Clone();
            int idx = current.ActiveMissions.FindIndex(m => m.MissionId == itemMission.MissionId);
            current.ActiveMissions[idx].State = MissionState.PendienteDeReclamar;

            // Fill Stash completely (capacity = 2)
            current.Stash.Clear();
            current.Stash.Add(new StashItem(new LootId("FILLER_1"), 1));
            current.Stash.Add(new StashItem(new LootId("FILLER_2"), 1));
            _repository.TrySave(current, out _);

            // Claim should fail due to full stash
            _presenter.OnClaimRequested(itemMission);

            // Mission must still be PendienteDeReclamar (not lost or consumed)
            var active = _store.GetActiveMissions();
            Assert.That(active[0].State, Is.EqualTo(MissionState.PendienteDeReclamar));
            Assert.That(_store.GuildReputation, Is.EqualTo(0));
        }

        [Test]
        public void Presenter_ClaimMission_SucceedsWhenStashHasSpace_GrantsRewardsAndMarksReclaimed()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _presenter.OnAcceptRequested(m1);

            // Complete m1 to PendienteDeReclamar
            var current = _repository.Snapshot.Clone();
            int idx = current.ActiveMissions.FindIndex(m => m.MissionId == m1.MissionId);
            current.ActiveMissions[idx].State = MissionState.PendienteDeReclamar;
            _repository.TrySave(current, out _);

            _presenter.OnClaimRequested(m1);

            // Mission is now Reclamada and rewards granted
            var active = _store.GetActiveMissions();
            Assert.That(active[0].State, Is.EqualTo(MissionState.Reclamada));
            Assert.That(_store.GuildReputation, Is.EqualTo(5)); // CON_E_001 gives 5 RP
        }

        [Test]
        public void Presenter_Tabs_FilterAvailableAndActiveMissionsSeparately()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            _presenter.OnAcceptRequested(m1);

            _view.SetTab(GuildViewTab.Active);
            _presenter.Refresh();
            Assert.That(_view.ActiveTab, Is.EqualTo(GuildViewTab.Active));

            _view.SetTab(GuildViewTab.Available);
            _presenter.Refresh();
            Assert.That(_view.ActiveTab, Is.EqualTo(GuildViewTab.Available));
        }

        [Test]
        public void Presenter_AcceptMission_DisplaysFeedback_AndSelectingAnotherMissionClearsIt()
        {
            Assert.That(_catalog.TryGet("CON_E_001", out var m1), Is.True);
            Assert.That(_catalog.TryGet("CON_E_002", out var m2), Is.True);

            _presenter.OnAcceptRequested(m1);
            Assert.That(_view.FeedbackText, Does.Contain("aceptado"));

            // Selecting another mission clears the feedback
            var entryMethod = _view.GetType().GetMethod("OnMissionEntrySelected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            entryMethod?.Invoke(_view, new object[] { m2 });

            Assert.That(_view.FeedbackText, Is.Empty);
        }

        [Test]
        public void View_ClearFeedback_ImmediatelyEmptiesFeedbackText()
        {
            _view.ShowFeedback("Operación exitosa");
            Assert.That(_view.FeedbackText, Is.EqualTo("Operación exitosa"));

            _view.ClearFeedback();
            Assert.That(_view.FeedbackText, Is.Empty);
        }
    }
}

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TeamExtractionPresentationTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private RaidHudPresenter _presenter;
        private RaidHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(false);
            _presenter = _instance.GetComponentInChildren<RaidHudPresenter>(true);
            _view = _instance.GetComponentInChildren<RaidHudView>(true);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);
            _presenter.Bind(null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [Test]
        public void View_PresentSanctuaryAssigned_WithTeamPresence_FormatsCorrectly()
        {
            _view.PresentSanctuaryAssigned(1, 2);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado (1/2 en el área)"));

            _view.PresentSanctuaryAssigned(2, 2);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado (2/2 en el área)"));

            _view.PresentSanctuaryAssigned(1, 1);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));

            _view.PresentSanctuaryAssigned(0, 0);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));
        }

        [Test]
        public void View_PresentExtractionCountdown_WithTeamPresence_FormatsCorrectly()
        {
            _view.PresentExtractionCountdown(2.4f, 1, 2);
            Assert.That(_view.ExtractionText.text, Does.Contain("(1/2 en el área)"));
            Assert.That(_view.ExtractionText.text, Does.StartWith("Extracción: "));

            _view.PresentExtractionCountdown(2.4f, 2, 2);
            Assert.That(_view.ExtractionText.text, Does.Contain("(2/2 en el área)"));

            _view.PresentExtractionCountdown(2.4f, 1, 1);
            Assert.That(_view.ExtractionText.text, Does.Not.Contain("en el área"));

            _view.PresentExtractionCountdown(2.4f, 0, 0);
            Assert.That(_view.ExtractionText.text, Does.Not.Contain("en el área"));
        }

        [Test]
        public void Presenter_SanctuarySectionPresence_UpdatesViewAndRespectsDirtyCheck()
        {
            InvokeRefreshSanctuarySectionPresence(true, 1, 2);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado (1/2 en el área)"));

            _view.SanctuaryText.text = "dirty-marker";
            InvokeRefreshSanctuarySectionPresence(true, 1, 2);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("dirty-marker"), "Dirty check should skip re-presentation for identical state.");

            InvokeRefreshSanctuarySectionPresence(true, 2, 2);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado (2/2 en el área)"), "Changed presence must trigger presentation.");

            InvokeRefreshSanctuarySectionPresence(false, 0, 0);
            Assert.That(_view.SanctuaryText.text, Is.Empty, "Disabling sanctuary must clear the section.");
        }

        [Test]
        public void Presenter_RitualStatusSectionPresence_CountdownWithPresence_UpdatesViewAndRespectsDirtyCheck()
        {
            var countdown = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                new EntityId(10),
                2.5f,
                5f,
                0.5f);

            InvokeRefreshRitualStatusSectionPresence(true, countdown, true, default, 2, 2);
            Assert.That(_view.ExtractionText.text, Does.Contain("(2/2 en el área)"));

            _view.ExtractionText.text = "dirty-marker-countdown";
            InvokeRefreshRitualStatusSectionPresence(true, countdown, true, default, 2, 2);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("dirty-marker-countdown"), "Dirty check should skip re-presentation for identical countdown presence.");

            InvokeRefreshRitualStatusSectionPresence(true, countdown, true, default, 1, 2);
            Assert.That(_view.ExtractionText.text, Does.Contain("(1/2 en el área)"), "Changed presence must update the countdown view.");
        }

        [Test]
        public void ExtractionSanctuaryPresenter_ResolveObservedCues_AssignedAndCancelledArePrivateToAssignedTeam()
        {
            // Assigned cue: only for local owner
            AssertCue("Assigned", hasPreviousSnapshot: true, ExtractionRitualState.NotStarted, ExtractionRitualState.NotStarted, previouslyReserved: false, currentlyReserved: true, isLocalOwner: true);
            AssertCue("None", hasPreviousSnapshot: true, ExtractionRitualState.NotStarted, ExtractionRitualState.NotStarted, previouslyReserved: false, currentlyReserved: true, isLocalOwner: false);

            // Cancelled cue: only for local owner
            AssertCue("Cancelled", hasPreviousSnapshot: true, ExtractionRitualState.InProgress, ExtractionRitualState.Cancelled, previouslyReserved: true, currentlyReserved: true, isLocalOwner: true);
            AssertCue("None", hasPreviousSnapshot: true, ExtractionRitualState.InProgress, ExtractionRitualState.Cancelled, previouslyReserved: true, currentlyReserved: true, isLocalOwner: false);

            // Started cue: public (both local owner and rivals hear it)
            AssertCue("Started", hasPreviousSnapshot: true, ExtractionRitualState.NotStarted, ExtractionRitualState.InProgress, previouslyReserved: true, currentlyReserved: true, isLocalOwner: true);
            AssertCue("Started", hasPreviousSnapshot: true, ExtractionRitualState.NotStarted, ExtractionRitualState.InProgress, previouslyReserved: true, currentlyReserved: true, isLocalOwner: false);

            // Completed cue: public (both hear it)
            AssertCue("Completed", hasPreviousSnapshot: true, ExtractionRitualState.InProgress, ExtractionRitualState.Completed, previouslyReserved: true, currentlyReserved: true, isLocalOwner: true);
            AssertCue("Completed", hasPreviousSnapshot: true, ExtractionRitualState.InProgress, ExtractionRitualState.Completed, previouslyReserved: true, currentlyReserved: true, isLocalOwner: false);
        }

        [Test]
        public void Coordinator_TryGetTeamPresence_WithOverride_ReturnsExpectedValues()
        {
            GameObject root = new GameObject("TestCoordinatorRoot");
            try
            {
                TeamExtractionCoordinator coordinator = root.AddComponent<TeamExtractionCoordinator>();
                coordinator.TeamPresenceOverrideForTest = teamId => (present: 1, required: 2);

                Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId teamId), Is.True);
                bool result = coordinator.TryGetTeamPresence(teamId, out int present, out int required);
                Assert.That(result, Is.True);
                Assert.That(present, Is.EqualTo(1));
                Assert.That(required, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private void InvokeRefreshSanctuarySectionPresence(bool hasSanctuary, int presentCount, int requiredCount)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "RefreshSanctuarySectionPresence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, new object[] { hasSanctuary, presentCount, requiredCount });
        }

        private void InvokeRefreshRitualStatusSectionPresence(
            bool hasCountdown,
            ExtractionCountdownSnapshot countdown,
            bool hasSanctuary,
            ExtractionRitualSnapshot ritual,
            int presentCount,
            int requiredCount)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "RefreshRitualStatusSectionPresence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, new object[] { hasCountdown, countdown, hasSanctuary, ritual, presentCount, requiredCount });
        }

        private static void AssertCue(
            string expected,
            bool hasPreviousSnapshot,
            ExtractionRitualState previousState,
            ExtractionRitualState currentState,
            bool previouslyReserved,
            bool currentlyReserved,
            bool isLocalOwner)
        {
            MethodInfo resolver = typeof(ExtractionSanctuaryPresenter).GetMethod(
                "ResolveObservedCues",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null);
            object result = resolver.Invoke(null, new object[]
            {
                hasPreviousSnapshot,
                previousState,
                currentState,
                previouslyReserved,
                currentlyReserved,
                isLocalOwner
            });
            Assert.That(result.ToString(), Is.EqualTo(expected));
        }
    }
}
#endif

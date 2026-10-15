#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tests.EditMode.Networking
{
    [TestFixture]
    public sealed class RaidTeammateAbsenceExecutionTests
    {
        [Test]
        public void Evaluate_RejectsTargetWhenNotRaiding()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Defeated);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedAlreadyResolved));
        }

        [Test]
        public void Evaluate_RejectsWhenNotTeammates()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Raiding);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: false,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedNotTeammate));
        }

        [Test]
        public void Evaluate_RejectsWhenConfirmerUnavailable()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Raiding);

            // Confirmer disconnected
            TeammateAbsenceDecisionResult resultDisconnected = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: false,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(resultDisconnected, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedConfirmerUnavailable));

            // Confirmer not raiding (e.g. defeated)
            TeammateAbsenceDecisionResult resultDefeated = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Defeated);

            Assert.That(resultDefeated, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedConfirmerUnavailable));
        }

        [Test]
        public void Evaluate_RejectsWhenTargetReconnected()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: false,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Raiding);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedReconnected));
        }

        [Test]
        public void Evaluate_RejectsWhenBudgetNotExhausted()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 60,
                state: RaidParticipantState.Raiding);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedBudgetNotExhausted));
        }

        [Test]
        public void Evaluate_AcceptsWhenAllConditionsMet()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Raiding);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.Confirmable));
        }

        [Test]
        public void Evaluate_RejectsWhenTargetAvatarUnresolvable()
        {
            var targetSnapshot = new RaidConnectivitySnapshot(
                isDisconnected: true,
                remainingBudgetTicks: 0,
                state: RaidParticipantState.Raiding);

            TeammateAbsenceDecisionResult result = RaidTeammateAbsenceDecisionRules.Evaluate(
                targetSnapshot,
                isTeammate: true,
                isConfirmerConnected: true,
                confirmerState: RaidParticipantState.Raiding,
                isTargetAvatarResolvable: false);

            Assert.That(result, Is.EqualTo(TeammateAbsenceDecisionResult.RejectedTargetAvatarUnavailable));
        }

        [Test]
        public void View_PresentsDisconnectedAndRemainingBudget()
        {
            var go = new GameObject("TestTeammateHudView");
            var root = new GameObject("Root");
            root.transform.SetParent(go.transform);
            var textGo = new GameObject("HealthText");
            textGo.transform.SetParent(root.transform);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform);
            var fill = fillGo.AddComponent<Image>();

            var view = go.AddComponent<RaidTeammateHudView>();
            // Use reflection to wire serialized fields
            typeof(RaidTeammateHudView).GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, root);
            typeof(RaidTeammateHudView).GetField("_healthText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, text);
            typeof(RaidTeammateHudView).GetField("_healthFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, fill);

            // Present with budget remaining
            view.PresentHealth(80f, 100f, isDisconnected: true, remainingBudgetSeconds: 25, isDecisionAvailable: false);
            Assert.That(view.HealthText.text, Is.EqualTo("Compañero: 80 / 100 [Desconectado: 25s]"));
            Assert.That(view.ContinueWithoutTeammateButton.gameObject.activeSelf, Is.False);

            // Present with budget exhausted and decision available
            view.PresentHealth(80f, 100f, isDisconnected: true, remainingBudgetSeconds: 0, isDecisionAvailable: true);
            Assert.That(view.HealthText.text, Is.EqualTo("Compañero: 80 / 100 [Desconectado]"));
            Assert.That(view.ContinueWithoutTeammateButton.gameObject.activeSelf, Is.True);

            // Clicking continue button raises event
            bool clicked = false;
            view.ContinueWithoutTeammateRequested += () => clicked = true;
            view.ContinueWithoutTeammateButton.onClick.Invoke();
            Assert.That(clicked, Is.True);

            // Present defeated hides button
            view.PresentDefeated(100f, hasMaximumHealth: true);
            Assert.That(view.ContinueWithoutTeammateButton.gameObject.activeSelf, Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ControlledReturnRegistry_MarkTerminalEnforcesTerminalState()
        {
            var registry = new ControlledReturnRegistry();
            var key = new ControlledReturnKey("profile-1", "generation-1");

            Assert.That(registry.IsTerminal(in key), Is.False);
            registry.MarkTerminal(in key);
            Assert.That(registry.IsTerminal(in key), Is.True);
        }
    }
}
#endif

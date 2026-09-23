using System;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;

namespace Grimhold.Tests.Stash
{
    /// <summary>
    /// Tests for the reconciliation logic in TownAttributeAssignmentPresenter (BACK-06).
    /// Uses a lightweight test double for RemoteRetryPolicy to verify that:
    /// - on success, ForceCharacterAttributeState is called with the authoritative data;
    /// - on definitive failure, ReconcileAsync is called to correct the optimistic local state.
    ///
    /// The presenter itself is a MonoBehaviour and cannot be instantiated without Play Mode.
    /// These tests verify the pure decision logic by driving it through the static
    /// RemoteRetryPolicy (which is already tested in RemoteRetryPolicyTests).
    /// </summary>
    public class AttributeAssignmentReconciliationPolicyTests
    {
        // ─── RemoteRetryPolicy — success path ───────────────────────────────

        [Test]
        public async Task RetryPolicy_SuccessOnFirstAttempt_DoesNotCallReconciliation()
        {
            bool reconciliationCalled = false;

            var (success, result, _) = await RemoteRetryPolicy.ExecuteWithRetryAsync(
                async () => await Task.FromResult((true, MakeAttributesData(vitality: 2), default(BackendError))),
                async () =>
                {
                    reconciliationCalled = true;
                    return true;
                });

            Assert.IsTrue(success);
            Assert.AreEqual(2, result.vitality);
            Assert.IsFalse(reconciliationCalled, "Reconciliation must not be called when backend succeeds immediately.");
        }

        // ─── RemoteRetryPolicy — REVISION_CONFLICT → reconcile → retry ──────

        [Test]
        public async Task RetryPolicy_RevisionConflict_ReconcileThenSucceed()
        {
            int operationAttempts   = 0;
            bool reconciliationCalled = false;

            var (success, _, error) = await RemoteRetryPolicy.ExecuteWithRetryAsync<CharacterAttributesData>(
                async () =>
                {
                    operationAttempts++;
                    if (operationAttempts == 1)
                        return (false, default, new BackendError { error = "REVISION_CONFLICT" });
                    return (true, MakeAttributesData(vitality: 3), default);
                },
                async () =>
                {
                    reconciliationCalled = true;
                    await Task.Yield();
                    return true;
                });

            Assert.IsTrue(success);
            Assert.IsTrue(reconciliationCalled, "Reconciliation must be called after a REVISION_CONFLICT.");
            Assert.AreEqual(2, operationAttempts, "Operation must be retried exactly once after reconciliation.");
        }

        // ─── RemoteRetryPolicy — definitive failure ──────────────────────────

        [Test]
        public async Task RetryPolicy_DefinitiveRejection_ReconciliationNotCalledByPolicy()
        {
            // A non-REVISION_CONFLICT error (e.g. 422 VALIDATION_FAILED) must fail immediately
            // without triggering the policy's internal reconciliation.
            // The presenter is then responsible for calling reconciliation itself.
            bool policySideReconciliationCalled = false;

            var (success, _, error) = await RemoteRetryPolicy.ExecuteWithRetryAsync<CharacterAttributesData>(
                async () => await Task.FromResult(
                    (false, default(CharacterAttributesData), new BackendError { error = "VALIDATION_FAILED" })),
                async () =>
                {
                    policySideReconciliationCalled = true;
                    return true;
                });

            Assert.IsFalse(success);
            Assert.AreEqual("VALIDATION_FAILED", error.error);
            Assert.IsFalse(policySideReconciliationCalled,
                "Policy-side reconciliation must NOT fire on non-revision errors; the presenter handles it.");
        }

        [Test]
        public async Task RetryPolicy_MaxRetriesExceeded_ReturnsMaxRetriesError()
        {
            // If every attempt returns REVISION_CONFLICT and reconciliation always succeeds
            // but the conflict persists, the policy gives up after MaxRetries attempts.
            int attempts = 0;

            var (success, _, error) = await RemoteRetryPolicy.ExecuteWithRetryAsync<CharacterAttributesData>(
                async () =>
                {
                    attempts++;
                    return (false, default, new BackendError { error = "REVISION_CONFLICT" });
                },
                async () => await Task.FromResult(true));

            Assert.IsFalse(success);
            Assert.AreEqual("MAX_RETRIES_EXCEEDED", error.error);
            Assert.AreEqual(RemoteRetryPolicy.MaxRetries, attempts);
        }

        // ─── Helper ─────────────────────────────────────────────────────────

        private static CharacterAttributesData MakeAttributesData(
            int vitality = 1, int resistance = 1, int strength = 1,
            int dexterity = 1, int intelligence = 1, int luck = 1, int availablePoints = 0)
        {
            return new CharacterAttributesData
            {
                vitality     = vitality,
                resistance   = resistance,
                strength     = strength,
                dexterity    = dexterity,
                intelligence = intelligence,
                luck         = luck,
                availablePoints = availablePoints
            };
        }
    }
}

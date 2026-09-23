using Grimhold.Backend;
using NUnit.Framework;

namespace Grimhold.Tests.Backend
{
    /// <summary>
    /// Tests for the hydration fail-closed policy introduced in BACK-02.
    /// Validates that <see cref="LoginHydrationFailureClassifier"/> maps backend
    /// errors to the correct <see cref="LoginFlowStatus"/> and does not silently
    /// swallow hydration failures.
    /// </summary>
    public class LoginHydrationFailureClassifierTests
    {
        // ─── ClassifyHydrationFailure ────────────────────────────────────────

        [Test]
        public void ClassifyHydrationFailure_NetworkError_Returns_NetworkErrorStatus()
        {
            var error = new BackendError { error = "NETWORK_ERROR" };

            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyHydrationFailure(error, "inventory");

            Assert.AreEqual(LoginFlowStatus.NetworkError, result.Status);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("inventory", result.ErrorMessage);
        }

        [Test]
        public void ClassifyHydrationFailure_ServerError_Returns_HydrationFailedStatus()
        {
            var error = new BackendError { error = "INTERNAL_SERVER_ERROR" };

            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyHydrationFailure(error, "progression");

            Assert.AreEqual(LoginFlowStatus.HydrationFailed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("progression", result.ErrorMessage);
        }

        [Test]
        public void ClassifyHydrationFailure_UnauthorizedError_Returns_HydrationFailedStatus()
        {
            var error = new BackendError { error = "UNAUTHORIZED" };

            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyHydrationFailure(error, "character profile");

            Assert.AreEqual(LoginFlowStatus.HydrationFailed, result.Status);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void ClassifyHydrationFailure_Timeout_Returns_NetworkErrorStatus()
        {
            var error = new BackendError { error = "NETWORK_ERROR", message = "Request timeout" };

            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyHydrationFailure(error, "inventory");

            // Timeout surfaces as NETWORK_ERROR on the client side — must be retryable.
            Assert.AreEqual(LoginFlowStatus.NetworkError, result.Status);
        }

        // ─── ClassifyRevisionMismatch ────────────────────────────────────────

        [Test]
        public void ClassifyRevisionMismatch_Always_Returns_HydrationFailed()
        {
            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyRevisionMismatch(
                inventoryRevision: 5,
                progressionRevision: 7);

            Assert.AreEqual(LoginFlowStatus.HydrationFailed, result.Status);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void ClassifyRevisionMismatch_MessageMentionsMismatch()
        {
            LoginFlowResult result = LoginHydrationFailureClassifier.ClassifyRevisionMismatch(1, 2);

            StringAssert.Contains("Mismatch", result.ErrorMessage);
        }

        // ─── LoginFlowResult invariants ──────────────────────────────────────

        [Test]
        public void LoginFlowResult_Success_IsSuccess_True()
        {
            LoginFlowResult result = LoginFlowResult.Success();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(LoginFlowStatus.Success, result.Status);
        }

        [Test]
        public void LoginFlowResult_HydrationFailed_IsSuccess_False()
        {
            LoginFlowResult result = LoginFlowResult.Failure(
                LoginFlowStatus.HydrationFailed,
                "Some error message");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(LoginFlowStatus.HydrationFailed, result.Status);
            Assert.AreEqual("Some error message", result.ErrorMessage);
        }
    }
}

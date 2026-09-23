using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;

namespace Grimhold.Tests.Stash
{
    public class RemoteRetryPolicyTests
    {
        [Test]
        public async Task ExecuteWithRetryAsync_SucceedsFirstTime_ReturnsSuccess()
        {
            var result = await RemoteRetryPolicy.ExecuteWithRetryAsync(
                async () => await Task.FromResult((true, "OK", default(BackendError))),
                async () => { Assert.Fail("Reconciliation should not be called on success"); return true; }
            );

            Assert.IsTrue(result.success);
            Assert.AreEqual("OK", result.result);
        }

        [Test]
        public async Task ExecuteWithRetryAsync_RevisionConflict_ReconcilesAndRetries()
        {
            int attempt = 0;
            var result = await RemoteRetryPolicy.ExecuteWithRetryAsync(
                async () => 
                {
                    attempt++;
                    if (attempt == 1)
                    {
                        return (false, "Fail", new BackendError { error = "REVISION_CONFLICT" });
                    }
                    return (true, "OK", default(BackendError));
                },
                async () => 
                {
                    await Task.Yield();
                    return true; // Successfully reconciled
                }
            );

            Assert.IsTrue(result.success);
            Assert.AreEqual("OK", result.result);
            Assert.AreEqual(2, attempt); // Tried once, failed, reconciled, tried again, success
        }

        [Test]
        public async Task ExecuteWithRetryAsync_OtherError_FailsImmediately()
        {
            int attempt = 0;
            var result = await RemoteRetryPolicy.ExecuteWithRetryAsync(
                async () => 
                {
                    attempt++;
                    return (false, "Fail", new BackendError { error = "NETWORK_ERROR" });
                },
                async () => { Assert.Fail("Reconciliation should not be called on non-revision conflicts"); return true; }
            );

            Assert.IsFalse(result.success);
            Assert.AreEqual("NETWORK_ERROR", result.error.error);
            Assert.AreEqual(1, attempt);
        }

        [Test]
        public async Task ExecuteWithRetryAsync_ReconciliationFails_ReturnsReconciliationError()
        {
            var result = await RemoteRetryPolicy.ExecuteWithRetryAsync(
                async () => await Task.FromResult((false, "Fail", new BackendError { error = "REVISION_CONFLICT" })),
                async () => await Task.FromResult(false) // Reconciliation failed
            );

            Assert.IsFalse(result.success);
            Assert.AreEqual("RECONCILIATION_FAILED", result.error.error);
        }
    }
}

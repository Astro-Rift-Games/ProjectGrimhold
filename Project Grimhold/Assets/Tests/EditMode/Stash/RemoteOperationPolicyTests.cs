#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;

[Category("BACK-06")]
public class RemoteOperationPolicyTests
{
    [Test]
    public async Task ExecuteWithReconciliation_Success_ReturnsTrue()
    {
        var (success, result, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => Task.FromResult((true, 42, default(BackendError))),
            () => Task.FromResult(true)
        );

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public async Task ExecuteWithReconciliation_RevisionConflict_ReconcilesAndReturnsFalse()
    {
        bool reconciled = false;
        var (success, result, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => Task.FromResult((false, 0, new BackendError { error = "REVISION_CONFLICT" })),
            () => { reconciled = true; return Task.FromResult(true); }
        );

        Assert.That(reconciled, Is.True);
        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo("REVISION_CONFLICT"));
    }

    [Test]
    public async Task ExecuteWithReconciliation_TransportFailure_ReconcilesAndReturnsFalse()
    {
        bool reconciled = false;
        var (success, result, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => Task.FromResult((false, 0, new BackendError { error = BackendErrorUtility.Timeout })),
            () => { reconciled = true; return Task.FromResult(true); }
        );

        Assert.That(reconciled, Is.True);
        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo(BackendErrorUtility.Timeout));
    }

    [Test]
    public async Task ExecuteWithReconciliation_DomainError_ReconcilesAndReturnsFalse()
    {
        bool reconciled = false;
        var (success, result, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => Task.FromResult((false, 0, new BackendError { error = "UNSUPPORTED_EQUIPMENT_LAYOUT" })),
            () => { reconciled = true; return Task.FromResult(true); }
        );

        Assert.That(reconciled, Is.True);
        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo("UNSUPPORTED_EQUIPMENT_LAYOUT"));
    }

    [Test]
    public async Task ExecuteWithReconciliation_ReconciliationFails_PropagatesError()
    {
        var (success, result, error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => Task.FromResult((false, 0, new BackendError { error = "REVISION_CONFLICT" })),
            () => Task.FromResult(false)
        );

        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo("RECONCILIATION_FAILED"));
    }
}
#endif

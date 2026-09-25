#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;

[Category("BACK-06")]
public class RemoteIdempotentRetryPolicyTests
{
    [SetUp]
    public void SetUp()
    {
        RemoteIdempotentRetryPolicy.BackoffMs = 0;
    }

    [TearDown]
    public void TearDown()
    {
        RemoteIdempotentRetryPolicy.BackoffMs = 1000;
    }

    [Test]
    public async Task ExecuteWithRetry_Success_DirectlyReturnsSuccess()
    {
        int attempts = 0;
        var (success, result, error) = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(
            () => { attempts++; return Task.FromResult((true, 42, default(BackendError))); }
        );

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteWithRetry_RevisionConflict_RetriesAndSucceeds()
    {
        int attempts = 0;
        var (success, result, error) = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(
            () => { 
                attempts++; 
                if (attempts == 1) return Task.FromResult((false, 0, new BackendError { error = "REVISION_CONFLICT" }));
                return Task.FromResult((true, 42, default(BackendError))); 
            }
        );

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task ExecuteWithRetry_TransportFailure_RetriesAndSucceeds()
    {
        int attempts = 0;
        var (success, result, error) = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(
            () => { 
                attempts++; 
                if (attempts == 1) return Task.FromResult((false, 0, new BackendError { error = BackendErrorUtility.NetworkError }));
                return Task.FromResult((true, 42, default(BackendError))); 
            }
        );

        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task ExecuteWithRetry_ExhaustsRetries_ReturnsFailure()
    {
        int attempts = 0;
        var (success, result, error) = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(
            () => { 
                attempts++; 
                return Task.FromResult((false, 0, new BackendError { error = BackendErrorUtility.Timeout }));
            }
        );

        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo(BackendErrorUtility.Timeout));
        Assert.That(attempts, Is.EqualTo(RemoteIdempotentRetryPolicy.MaxRetries));
    }

    [Test]
    public async Task ExecuteWithRetry_DomainError_FailsImmediatelyWithoutRetry()
    {
        int attempts = 0;
        var (success, result, error) = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(
            () => { 
                attempts++; 
                return Task.FromResult((false, 0, new BackendError { error = "UNSUPPORTED_EQUIPMENT_LAYOUT" }));
            }
        );

        Assert.That(success, Is.False);
        Assert.That(error.error, Is.EqualTo("UNSUPPORTED_EQUIPMENT_LAYOUT"));
        Assert.That(attempts, Is.EqualTo(1));
    }
}
#endif

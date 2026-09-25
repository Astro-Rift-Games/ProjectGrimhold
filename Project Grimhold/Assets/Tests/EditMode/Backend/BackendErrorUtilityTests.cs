#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Grimhold.Backend;
using NUnit.Framework;

[Category("BACK-06")]
public class BackendErrorUtilityTests
{
    [Test]
    public void ClassifyConnectionError_ReturnsNetworkError_WhenErrorIsEmptyOrNull()
    {
        Assert.That(BackendErrorUtility.ClassifyConnectionError(""), Is.EqualTo(BackendErrorUtility.NetworkError));
        Assert.That(BackendErrorUtility.ClassifyConnectionError(null), Is.EqualTo(BackendErrorUtility.NetworkError));
    }

    [Test]
    public void ClassifyConnectionError_ReturnsTimeout_WhenErrorIsTimeout()
    {
        Assert.That(BackendErrorUtility.ClassifyConnectionError("Request timeout"), Is.EqualTo(BackendErrorUtility.Timeout));
        Assert.That(BackendErrorUtility.ClassifyConnectionError("timeout"), Is.EqualTo(BackendErrorUtility.Timeout));
    }

    [Test]
    public void ClassifyConnectionError_ReturnsRequestCancelled_WhenErrorIsAbortOrCancel()
    {
        Assert.That(BackendErrorUtility.ClassifyConnectionError("Request aborted"), Is.EqualTo(BackendErrorUtility.RequestCancelled));
        Assert.That(BackendErrorUtility.ClassifyConnectionError("user cancelled"), Is.EqualTo(BackendErrorUtility.RequestCancelled));
    }

    [Test]
    public void ClassifyConnectionError_ReturnsNetworkError_WhenErrorIsUnknown()
    {
        Assert.That(BackendErrorUtility.ClassifyConnectionError("Unknown network issue"), Is.EqualTo(BackendErrorUtility.NetworkError));
    }

    [Test]
    public void IsTransportFailure_ReturnsTrue_ForTransportErrors()
    {
        Assert.That(BackendErrorUtility.IsTransportFailure(BackendErrorUtility.NetworkError), Is.True);
        Assert.That(BackendErrorUtility.IsTransportFailure(BackendErrorUtility.Timeout), Is.True);
        Assert.That(BackendErrorUtility.IsTransportFailure(BackendErrorUtility.RequestCancelled), Is.True);
    }

    [Test]
    public void IsTransportFailure_ReturnsFalse_ForDomainErrors()
    {
        Assert.That(BackendErrorUtility.IsTransportFailure("REVISION_CONFLICT"), Is.False);
        Assert.That(BackendErrorUtility.IsTransportFailure("UNAUTHORIZED"), Is.False);
        Assert.That(BackendErrorUtility.IsTransportFailure(null), Is.False);
    }
}
#endif

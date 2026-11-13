#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Globalization;
using System.Threading;
using Assert = NUnit.Framework.Assert;

/// <summary>FIX-06: the admission trace line format is stable and greppable (S20, S21).</summary>
public sealed class RaidAdmissionDiagnosticsTests
{
    [Test]
    public void FormatTrace_FullEvent_UsesPinnedFormat()
    {
        string line = RaidAdmissionDiagnostics.FormatTrace(
            "PayloadReceived", "[Player:2]", "profile-a", 1234, 12.3456f, "senderRegistered=False bytes=310");

        Assert.That(
            line,
            Is.EqualTo(
                "[RaidAdmissionTrace] event=PayloadReceived player=[Player:2] profile=profile-a " +
                "tick=1234 t=12.346 detail=senderRegistered=False bytes=310"));
    }

    [Test]
    public void FormatTrace_UnknownValues_RenderPlaceholdersAndOmitDetail()
    {
        string line = RaidAdmissionDiagnostics.FormatTrace("PlayerJoined", "[Player:3]", null, -1, 0f, "  ");

        Assert.That(
            line,
            Is.EqualTo("[RaidAdmissionTrace] event=PlayerJoined player=[Player:3] profile=n/a tick=n/a t=0.000"));
    }

    [Test]
    public void FormatTrace_AlwaysStartsWithSharedPrefix()
    {
        string line = RaidAdmissionDiagnostics.FormatTrace(null, null, null, 0, 1f, null);

        Assert.That(line, Does.StartWith(RaidAdmissionDiagnostics.Prefix + " event=n/a player=n/a profile=n/a tick=0"));
    }

    [Test]
    public void FormatTrace_MultilineDetail_IsFlattenedToOneLine()
    {
        string line = RaidAdmissionDiagnostics.FormatTrace("X", "p", "f", 1, 1f, "a\r\nb\nc");

        Assert.That(line, Does.Not.Contain("\n"));
        Assert.That(line, Does.Not.Contain("\r"));
        Assert.That(line, Does.EndWith("detail=a  b c"));
    }

    [Test]
    public void FormatTrace_LongDetail_IsTruncated()
    {
        string line = RaidAdmissionDiagnostics.FormatTrace(
            "X", "p", "f", 1, 1f, new string('a', RaidAdmissionDiagnostics.MaxDetailLength + 50));

        Assert.That(line, Does.EndWith("detail=" + new string('a', RaidAdmissionDiagnostics.MaxDetailLength)));
    }

    [Test]
    public void FormatTrace_UsesInvariantCultureForRealtime()
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

            string line = RaidAdmissionDiagnostics.FormatTrace("X", "p", "f", 5, 1.5f, null);

            Assert.That(line, Does.EndWith("tick=5 t=1.500"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
#endif

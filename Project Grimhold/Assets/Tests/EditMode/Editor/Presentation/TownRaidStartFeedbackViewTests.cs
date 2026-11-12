#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidStartFeedbackViewTests
{
    private static readonly ProfileId Host = new("host");
    private GameObject _root;
    private TownRaidPreparationView _view;
    private bool _cardFallbackLogExpected;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(nameof(TownRaidStartFeedbackViewTests));
        _view = TownRaidPreparationView.CreateEditorSource(_root.transform);
        _cardFallbackLogExpected = false;
        _view.Open();
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
        }
    }

    [Test]
    public void PartyUnavailable_ShowsTheHostTheRetryNotice()
    {
        _view.ShowStartRejected(TownRaidStartResult.PartyUnavailable);

        TownRaidStartRejectionText.TryGetMessage(TownRaidStartResult.PartyUnavailable, out string expected);
        StringAssert.Contains(expected, StatusText());
    }

    [Test]
    public void PartyUnavailable_KeepsTheCurrentPreparationStatusVisible()
    {
        Present(1);
        string before = StatusText();

        _view.ShowStartRejected(TownRaidStartResult.PartyUnavailable);

        StringAssert.Contains("Jugadores: 2", StatusText());
        StringAssert.Contains(before, StatusText());
    }

    [TestCase(TownRaidStartResult.Started)]
    [TestCase(TownRaidStartResult.Rejected)]
    public void ResultsWithoutNotice_LeaveTheStatusUntouched(TownRaidStartResult result)
    {
        string before = StatusText();

        _view.ShowStartRejected(result);

        Assert.That(StatusText(), Is.EqualTo(before));
    }

    [Test]
    public void Notice_IsClearedWhenTheNextPreparationSnapshotIsPresented()
    {
        Present(1);
        _view.ShowStartRejected(TownRaidStartResult.PartyUnavailable);
        TownRaidStartRejectionText.TryGetMessage(TownRaidStartResult.PartyUnavailable, out string notice);

        Present(2);

        StringAssert.DoesNotContain(notice, StatusText());
    }

    [Test]
    public void Notice_IsClearedWhenThePreparationEnds()
    {
        Present(1);
        _view.ShowStartRejected(TownRaidStartResult.PartyUnavailable);
        TownRaidStartRejectionText.TryGetMessage(TownRaidStartResult.PartyUnavailable, out string notice);

        _view.PresentNoPreparation();

        StringAssert.DoesNotContain(notice, StatusText());
    }

    [Test]
    public void Notice_IsClearedWhenThePanelIsReopened()
    {
        _view.ShowStartRejected(TownRaidStartResult.PartyUnavailable);
        TownRaidStartRejectionText.TryGetMessage(TownRaidStartResult.PartyUnavailable, out string notice);

        _view.Open();

        StringAssert.DoesNotContain(notice, StatusText());
    }

    // The editor-built view has no member card prefab, so its first roster paint logs the fallback error once.
    private void Present(int revision)
    {
        if (!_cardFallbackLogExpected)
        {
            LogAssert.Expect(LogType.Error, "TownRaidPreparationView requires member card prefab and container references; using text roster fallback.");
            _cardFallbackLogExpected = true;
        }

        _view.PresentPreparation(CreatePresentation(revision));
    }

    private string StatusText() =>
        _view.transform.Find("RaidCodePanel/Status").GetComponent<TMP_Text>().text;

    private static TownRaidPreparationPresentation CreatePresentation(int revision)
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var snapshot = new TownRaidPreparationSnapshot(
            code,
            Host,
            TownRaidPreparationState.Waiting,
            new[]
            {
                new TownRaidPreparationMember(Host, true),
                new TownRaidPreparationMember(new ProfileId("client"), true)
            },
            revision);
        Assert.That(
            TownRaidPreparationPresentation.TryCreate(snapshot, Host, out TownRaidPreparationPresentation presentation),
            Is.True);
        return presentation;
    }
}
#endif

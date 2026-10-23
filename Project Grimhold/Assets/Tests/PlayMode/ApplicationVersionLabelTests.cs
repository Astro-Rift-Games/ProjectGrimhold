#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ApplicationVersionLabelTests
{
    private GameObject _root;
    private TextMeshProUGUI _text;
    private ApplicationVersionLabel _label;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Application version test", typeof(RectTransform));
        _text = _root.AddComponent<TextMeshProUGUI>();
        _text.text = string.Empty;
        _text.raycastTarget = false;
        _root.SetActive(false);
        _label = _root.AddComponent<ApplicationVersionLabel>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void OnEnable_DisplaysApplicationVersionWithVPrefix()
    {
        AssignText();
        _root.SetActive(true);

        Assert.That(_text.text, Is.EqualTo($"v{Application.version}"));
        Assert.That(_label.enabled, Is.True);
    }

    [Test]
    public void Reenabling_RefreshesTheDisplayedVersion()
    {
        AssignText();
        _root.SetActive(true);
        _label.enabled = false;
        _text.text = "stale presentation";
        _label.enabled = true;

        Assert.That(_text.text, Is.EqualTo($"v{Application.version}"));
    }

    [Test]
    public void MissingText_ReportsConfigurationErrorAndDisablesComponent()
    {
        LogAssert.Expect(LogType.Error, "[ApplicationVersionLabel] Version text is not assigned.");
        _root.SetActive(true);

        Assert.That(_label.enabled, Is.False);
        Assert.That(_text.text, Is.Empty);
    }

    [Test]
    public void OnEnable_PreservesNonRaycastingInspectorConfiguration()
    {
        AssignText();
        _root.SetActive(true);
        _root.SetActive(false);
        _root.SetActive(true);

        Assert.That(_text.raycastTarget, Is.False);
        Assert.That(_text.text, Is.EqualTo($"v{Application.version}"));
    }

    private void AssignText()
    {
        var serializedLabel = new SerializedObject(_label);
        serializedLabel.FindProperty("_versionText").objectReferenceValue = _text;
        serializedLabel.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif

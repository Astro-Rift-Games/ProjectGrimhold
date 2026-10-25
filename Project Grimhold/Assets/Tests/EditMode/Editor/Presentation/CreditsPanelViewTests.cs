#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class CreditsPanelViewTests
    {
        private GameObject _root;
        private CreditsPanelView _view;
        private Button _back;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _root = new GameObject("CreditsPanelViewTests");
            _view = _root.AddComponent<CreditsPanelView>();
            var child = new GameObject("Back", typeof(RectTransform), typeof(Button));
            child.transform.SetParent(_root.transform);
            _back = child.GetComponent<Button>();
            typeof(CreditsPanelView).GetField("_backButton", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, _back);
            // EditMode does not run Awake automatically.
            typeof(CreditsPanelView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, null);
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Time.timeScale = _timeScale;
        }

        [Test]
        public void BackButton_ClosesAndRaisesClosed()
        {
            int calls = 0;
            _view.Closed += () => calls++;
            _view.Open();
            Assert.That(_view.IsOpen, Is.True);
            _back.onClick.Invoke();
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void OpenAndClose_PreserveTimeScaleAndCloseRaisesClosed()
        {
            Time.timeScale = 0.35f;
            int calls = 0;
            _view.Closed += () => calls++;
            _view.Open();
            Assert.That(Time.timeScale, Is.EqualTo(0.35f));
            _view.Close();
            Assert.That(Time.timeScale, Is.EqualTo(0.35f));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
#endif

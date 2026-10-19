#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    /// <summary>Runtime proof that panel transitions keep logical visibility immediate.</summary>
    [Category("TASK158")]
    public sealed class UiPanelTransitionPlayModeTests
    {
        private GameObject _parent;
        private GameObject _panel;
        private UiPanelTransition _transition;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Parent", typeof(RectTransform));
            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(_parent.transform, false);
            _panel.SetActive(false);
            _transition = _panel.AddComponent<UiPanelTransition>();
            _group = _panel.GetComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_parent);

        [UnityTest]
        public IEnumerator Show_IsVisibleImmediatelyAndFadesToOpaque()
        {
            UiPanelVisibility.SetVisible(_panel, true);

            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.True);
            Assert.That(_group.alpha, Is.EqualTo(0f));
            Assert.That(_group.blocksRaycasts, Is.True);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_panel.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator Hide_IsHiddenImmediatelyStopsInputAndDeactivatesAfterTransition()
        {
            UiPanelVisibility.SetVisible(_panel, true);
            yield return new WaitForSecondsRealtime(0.3f);

            UiPanelVisibility.SetVisible(_panel, false);

            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.False);
            Assert.That(_panel.activeSelf, Is.True);
            Assert.That(_group.blocksRaycasts, Is.False);
            Assert.That(_group.interactable, Is.False);

            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(_panel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ShowDuringHide_ReversesAndStaysActive()
        {
            UiPanelVisibility.SetVisible(_panel, true);
            yield return new WaitForSecondsRealtime(0.3f);
            UiPanelVisibility.SetVisible(_panel, false);
            yield return null;

            UiPanelVisibility.SetVisible(_panel, true);
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(_panel.activeSelf, Is.True);
            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.True);
            Assert.That(_group.alpha, Is.EqualTo(1f));
            Assert.That(_group.blocksRaycasts, Is.True);
        }

        [UnityTest]
        public IEnumerator ParentDisabledMidHide_FinishesHiddenWhenParentReturns()
        {
            UiPanelVisibility.SetVisible(_panel, true);
            yield return new WaitForSecondsRealtime(0.3f);
            UiPanelVisibility.SetVisible(_panel, false);

            _parent.SetActive(false);
            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.False);
            _parent.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(_panel.activeSelf, Is.False);
            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.False);
        }

        [UnityTest]
        public IEnumerator Hide_BeforeFirstEnable_DoesNotThrowAndStaysHidden()
        {
            var inactiveParent = new GameObject("InactiveParent", typeof(RectTransform));
            inactiveParent.SetActive(false);
            var panel = new GameObject("NeverEnabled", typeof(RectTransform));
            panel.transform.SetParent(inactiveParent.transform, false);
            panel.AddComponent<UiPanelTransition>();
            try
            {
                // Same answer as the previous activeSelf check while the panel was never enabled.
                Assert.That(UiPanelVisibility.IsVisible(panel), Is.True);

                Assert.DoesNotThrow(() => UiPanelVisibility.SetVisible(panel, false));
                Assert.That(panel.activeSelf, Is.False);
                Assert.That(UiPanelVisibility.IsVisible(panel), Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(inactiveParent);
            }
        }

        [UnityTest]
        public IEnumerator HideFromParentAwake_StaysHiddenOnceActivated()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            host.SetActive(false);
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            panel.AddComponent<UiPanelTransition>();
            host.AddComponent<HidesChildOnAwake>().Child = panel;
            try
            {
                host.SetActive(true);
                // Long enough for a hide fade too, so the result does not depend on Awake order.
                yield return new WaitForSecondsRealtime(0.25f);

                Assert.That(panel.activeSelf, Is.False);
                Assert.That(UiPanelVisibility.IsVisible(panel), Is.False);
            }
            finally
            {
                Object.Destroy(host);
            }
        }

        private sealed class HidesChildOnAwake : MonoBehaviour
        {
            public GameObject Child;

            private void Awake()
            {
                if (Child != null)
                {
                    UiPanelVisibility.SetVisible(Child, false);
                }
            }
        }

        [UnityTest]
        public IEnumerator DirectDeactivationMidHide_DoesNotSwallowTheNextShow()
        {
            UiPanelVisibility.SetVisible(_panel, true);
            yield return new WaitForSecondsRealtime(0.3f);
            UiPanelVisibility.SetVisible(_panel, false);

            _panel.SetActive(false);
            _panel.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(_panel.activeSelf, Is.True);
            Assert.That(UiPanelVisibility.IsVisible(_panel), Is.True);
        }
    }
}
#endif

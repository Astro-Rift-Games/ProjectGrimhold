#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode.Presentation
{
    public sealed class PlayerStatusFeedbackTests
    {
        [Test]
        public void ConfirmedWeaponBaseline_DoesNotPulseOnBindOrRepeatedState()
        {
            var state = new RaidStatusFeedbackState();
            Assert.That(state.ObserveWeaponSet(WeaponSetSlot.SetA), Is.False);
            Assert.That(state.ObserveWeaponSet(WeaponSetSlot.SetA), Is.False);
            Assert.That(state.ObserveWeaponSet(WeaponSetSlot.SetB), Is.True);
            state.Reset();
            Assert.That(state.ObserveWeaponSet(WeaponSetSlot.SetB), Is.False);
        }

        [Test]
        public void AttributeAccess_IsHiddenWhenUnboundOrBindingFails()
        {
            var root = new GameObject("Unbound Town menu");
            try
            {
                var presenter = root.AddComponent<TownPlayerMenuPresenter>();
                var access = new GameObject("Attributes access", typeof(RectTransform), typeof(Button));
                access.transform.SetParent(root.transform);
                SetField(presenter, "_attributeAccessButton", access.GetComponent<Button>());
                InvokeLifecycle(presenter, "Awake");
                Assert.That(access.activeSelf, Is.False);
                access.SetActive(true);
                presenter.Bind(null, root.transform);
                Assert.That(access.activeSelf, Is.False);
                Assert.That(presenter.IsOpen, Is.False);
            }
            finally
            {
                // The fixture owns this child; do not enter the runtime's deferred Destroy path in EditMode.
                SetField(root.GetComponent<TownPlayerMenuPresenter>(), "_view", null);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AttributeAccess_IsShownOnlyAfterBindingAndHiddenOnDisableOrUnbind()
        {
            var root = new GameObject("Local Town menu");
            try
            {
                var presenter = root.AddComponent<TownPlayerMenuPresenter>();
                var access = new GameObject("Attributes access", typeof(RectTransform), typeof(Button));
                access.transform.SetParent(root.transform);
                SetField(presenter, "_attributeAccessButton", access.GetComponent<Button>());
                var viewRoot = new GameObject("Menu view");
                viewRoot.transform.SetParent(root.transform);
                SetField(presenter, "_view", viewRoot.AddComponent<TownPlayerMenuView>());
                var reader = root.AddComponent<PlayerInputReader>();
                if (typeof(PlayerInputReader).GetField("_inputActions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader) == null)
                    InvokeLifecycle(reader, "Awake");
                presenter.Bind(reader, root.transform);
                Assert.That(access.activeSelf, Is.True);
                Assert.That(presenter.IsOpen, Is.False);
                presenter.Unbind();
                Assert.That(access.activeSelf, Is.False);
                presenter.Bind(reader, root.transform);
                Assert.That(access.activeSelf, Is.True);
                InvokeLifecycle(presenter, "OnDisable");
                Assert.That(access.activeSelf, Is.False);
                Assert.That(typeof(TownPlayerMenuPresenter).GetField("_reader", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter), Is.Null);
            }
            finally
            {
                // The fixture owns this child; do not enter the runtime's deferred Destroy path in EditMode.
                SetField(root.GetComponent<TownPlayerMenuPresenter>(), "_view", null);
                Object.DestroyImmediate(root);
            }
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void InvokeLifecycle(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        [Test]
        public void Notice_ReplacementRestartsConfiguredLifetime()
        {
            var state = new RaidStatusFeedbackState();
            state.ShowNotice("First", 10, 3);
            Assert.That(state.ReadNotice(12), Is.EqualTo("First"));
            state.ShowNotice("Second", 12, 3);
            Assert.That(state.ReadNotice(14.9f), Is.EqualTo("Second"));
            Assert.That(state.ReadNotice(15), Is.Empty);
            state.Reset();
            Assert.That(state.ReadNotice(1), Is.Empty);
        }
    }
}
#endif

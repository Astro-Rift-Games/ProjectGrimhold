#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class PlayerExtractionPresenterTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private PlayerExtractionPresenter _presenter;
        private GameObject _body;
        private GameObject _combatVisuals;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _presenter = _instance.GetComponent<PlayerExtractionPresenter>();
            _body = _instance.transform.Find("Body")?.gameObject;
            _combatVisuals = _instance.transform.Find("CombatVisuals")?.gameObject;

            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_body, Is.Not.Null);
            Assert.That(_combatVisuals, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [Test]
        public void PrefabAssignsOnlyBodyAndCombatVisualsAsExtractionRoots()
        {
            FieldInfo field = typeof(PlayerExtractionPresenter).GetField(
                "_visualRoots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);

            GameObject[] roots = (GameObject[])field.GetValue(_presenter);

            Assert.That(roots, Is.EqualTo(new[] { _body, _combatVisuals }));
        }

        [Test]
        public void ExtractedStateHidesConfiguredRootsWithoutDisablingPlayerObject()
        {
            InvokeApplyState(ExtractionState.Extracted);

            Assert.That(_instance.activeSelf, Is.True);
            Assert.That(_body.activeSelf, Is.False);
            Assert.That(_combatVisuals.activeSelf, Is.False);
        }

        [Test]
        public void NonExtractedStateRestoresConfiguredRoots()
        {
            InvokeApplyState(ExtractionState.Extracted);
            InvokeApplyState(ExtractionState.None);

            Assert.That(_body.activeSelf, Is.True);
            Assert.That(_combatVisuals.activeSelf, Is.True);
        }

        [Test]
        public void DisablingPresenterRestoresVisualRootsWithoutDisablingPlayerObject()
        {
            InvokeApplyState(ExtractionState.Extracted);
            _presenter.enabled = false;

            Assert.That(_instance.activeSelf, Is.True);
            Assert.That(_body.activeSelf, Is.True);
            Assert.That(_combatVisuals.activeSelf, Is.True);
        }

        private void InvokeApplyState(ExtractionState state)
        {
            MethodInfo method = typeof(PlayerExtractionPresenter).GetMethod(
                "ApplyVisualState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, new object[] { state });
        }
    }
}
#endif

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class ScreenDamageTests
    {
        private ScreenDamageConfig _config;
        private ScreenDamageFeedbackState _state;
        [SetUp] public void SetUp() { _config = ScriptableObject.CreateInstance<ScreenDamageConfig>(); _state = new ScreenDamageFeedbackState(_config); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);

        [TestCase(1f, 0f)] [TestCase(.71f, 0f)] [TestCase(.7f, 0f)]
        [TestCase(.4f, .428571f)] [TestCase(.2f, .714286f)] [TestCase(0f, 1f)]
        public void Config_MapsHealthToContinuousSeverity(float hp, float expected)
        {
            Assert.That(_config.Severity(hp), Is.EqualTo(expected).Within(.0001f));
        }
        [Test]
        public void Step_ApproachesLowHealthSmoothlyAndDoesNotFlashOnFirstRead()
        {
            _state.Step(10, 100, true, .016f, 1);
            Assert.That(_state.Flash, Is.Zero);
            Assert.That(_state.Severity, Is.InRange(.001f, .2f));
            _state.Step(10, 100, true, 2, 1);
            Assert.That(_state.Severity, Is.GreaterThan(.85f));
        }
        [Test]
        public void Step_ConfirmedHealthLoss_FlashesForConfiguredAttackAndRelease()
        {
            _state.Step(100, 100, true, 0, 1);
            _state.Step(90, 100, true, 0, 1);
            _state.Step(90, 100, true, .05f, 1);
            Assert.That(_state.Flash, Is.EqualTo(1).Within(.0001f));
            _state.Step(90, 100, true, .1f, 1);
            Assert.That(_state.Flash, Is.EqualTo(.5f).Within(.0001f));
            _state.Step(90, 100, true, .1f, 1);
            Assert.That(_state.Flash, Is.EqualTo(0).Within(.0001f));
        }
        [Test]
        public void Step_HealingAndMaximumChanges_DoNotGenerateImpact()
        {
            _state.Step(50, 100, true, 0, 1);
            _state.Step(70, 100, true, .05f, 1);
            Assert.That(_state.Flash, Is.Zero);
            _state.Step(60, 80, true, .05f, 1);
            Assert.That(_state.Flash, Is.Zero);
        }
        [Test]
        public void Config_HeartbeatOnlyBelowCriticalAndAcceleratesAsHealthFalls()
        {
            Assert.That(_config.HeartbeatHz(.4f), Is.Zero);
            Assert.That(_config.HeartbeatHz(.1f), Is.GreaterThan(_config.HeartbeatHz(.3f)));
        }
        [Test]
        public void Step_DisabledAccessibilityClearsAllFeedbackWithoutReplayingDamage()
        {
            _state.Step(80, 100, true, 0, 0);
            _state.Step(50, 100, true, 0, 0);
            Assert.That(_state.Opacity, Is.Zero);
            _state.Step(50, 100, true, 1, 1);
            Assert.That(_state.Flash, Is.Zero);
        }
        [TestCase(false, 20, 100)] [TestCase(true, 0, 100)] [TestCase(true, 20, 0)]
        public void Step_InactiveDeadOrInvalidSourceClearsAndRebaselines(bool active, float hp, float max)
        {
            _state.Step(10, 100, true, 1, 1);
            _state.Step(hp, max, active, .016f, 1);
            Assert.That(_state.Severity, Is.Zero);
            Assert.That(_state.Opacity, Is.Zero);
            _state.Step(5, 100, true, 0, 1);
            Assert.That(_state.Flash, Is.Zero);
        }
        [Test]
        public void Source_UnspawnedPlayerAndProxy_AreUnavailableWithoutNetworkReads()
        {
            var go = new GameObject("Unspawned screen damage source");
            try
            {
                var source = new PlayerScreenDamageSource(go.AddComponent<PlayerCharacter>());
                Assert.That(source.TryRead(out _, out _), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void ImportedFrames_KeepCenterClearAndAlphaTranslucent()
        {
            for (int f = 0; f < 13; f++)
            {
                var bytes = System.IO.File.ReadAllBytes($"Assets/Art/UI/ScreenDamage/screen-damage-{f:00}.png");
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(texture.LoadImage(bytes), Is.True);
                    Assert.That(texture.width, Is.EqualTo(320));
                    Assert.That(texture.height, Is.EqualTo(180));
                    var pixels = texture.GetPixels32();
                    for (int y = 0; y < 180; y++) for (int x = 0; x < 320; x++)
                    {
                        Assert.That(pixels[y * 320 + x].a, Is.LessThanOrEqualTo(88));
                        if (x >= 112 && x < 208 && y >= 62 && y < 118)
                            Assert.That(pixels[y * 320 + x].a, Is.Zero);
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }
        private sealed class Source : IScreenDamageSource
        {
            public bool Available = true;
            public float Health = 20;
            public bool TryRead(out float health, out float maximum) { health = Health; maximum = 100; return Available; }
        }
        [Test]
        public void Presenter_UnbindDisableAndLostSource_ClearBothImagesAndRebaseline()
        {
            var go = new GameObject("Screen damage lifecycle");
            try
            {
                var lower = new GameObject("Lower", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                lower.transform.SetParent(go.transform);
                var upper = new GameObject("Upper", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                upper.transform.SetParent(go.transform);
                var view = go.AddComponent<ScreenDamageView>();
                var vs = new SerializedObject(view);
                vs.FindProperty("_lower").objectReferenceValue = lower.GetComponent<UnityEngine.UI.Image>();
                vs.FindProperty("_upper").objectReferenceValue = upper.GetComponent<UnityEngine.UI.Image>();
                var frames = vs.FindProperty("_stages"); frames.arraySize = 13;
                for (int i = 0; i < 13; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/ScreenDamage/screen-damage-{i:00}.png");
                vs.ApplyModifiedPropertiesWithoutUndo();
                var presenter = go.AddComponent<LocalPlayerScreenDamagePresenter>();
                var ps = new SerializedObject(presenter);
                ps.FindProperty("_config").objectReferenceValue = _config;
                ps.FindProperty("_view").objectReferenceValue = view;
                ps.ApplyModifiedPropertiesWithoutUndo();
                var source = new Source(); presenter.Bind(source); presenter.Tick(1);
                presenter.Unbind();
                Assert.That(lower.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                Assert.That(upper.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                presenter.Bind(source); presenter.Tick(1); source.Available = false; presenter.Tick(.016f);
                Assert.That(lower.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                source.Available = true; presenter.Tick(1); go.SetActive(false);
                // EditMode does not dispatch MonoBehaviour lifecycle callbacks outside Play Mode.
                typeof(LocalPlayerScreenDamagePresenter).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(presenter, null);
                Assert.That(lower.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                Assert.That(upper.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void ProductivePrefab_HasConfiguredLocalScreenDamage()
        {
            var type = typeof(PlayerCharacter).Assembly.GetType("LocalPlayerScreenDamagePresenter");
            Assert.That(type, Is.Not.Null, "TASK-253 presenter is missing");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            var presenter = prefab.GetComponentInChildren(type, true);
            Assert.That(presenter, Is.Not.Null);
            var so = new SerializedObject(presenter);
            Assert.That(so.FindProperty("_config").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_view").objectReferenceValue, Is.Not.Null);
        }
        [Test]
        public void SettingsPrefab_HasIndependentIntensitySliderWithoutChangingAudioBindings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingsPanel.prefab");
            var options = prefab.GetComponent<ScreenDamageOptionsView>();
            Assert.That(options, Is.Not.Null);
            var so = new SerializedObject(options);
            var slider = (UnityEngine.UI.Slider)so.FindProperty("_intensity").objectReferenceValue;
            Assert.That(slider, Is.Not.Null);
            var audio = new SerializedObject(prefab.GetComponent<SettingsPanelView>());
            foreach (string field in new[] { "_masterSlider", "_musicSlider", "_sfxSlider" })
            {
                Assert.That(audio.FindProperty(field).objectReferenceValue, Is.Not.Null);
                Assert.That(audio.FindProperty(field).objectReferenceValue, Is.Not.SameAs(slider));
            }
        }
    }
}
#endif

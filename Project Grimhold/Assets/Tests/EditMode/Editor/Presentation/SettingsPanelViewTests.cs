#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class SettingsPanelViewTests
    {
        private GameObject _root;
        private SettingsPanelView _view;
        private Slider _master;
        private Slider _music;
        private Slider _sfx;
        private Button _back;
        private FakeAudioVolumeControl _control;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _root = new GameObject("SettingsPanelViewTests");
            _view = _root.AddComponent<SettingsPanelView>();
            _master = CreateSlider("Master", "_masterSlider");
            _music = CreateSlider("Music", "_musicSlider");
            _sfx = CreateSlider("Sfx", "_sfxSlider");
            var backObject = new GameObject("Back", typeof(RectTransform), typeof(Button));
            backObject.transform.SetParent(_root.transform);
            _back = backObject.GetComponent<Button>();
            SetField("_backButton", _back);
            // EditMode AddComponent does not invoke Awake; exercise its real wiring explicitly.
            typeof(SettingsPanelView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_view, null);
            _control = new FakeAudioVolumeControl();
            _view.Bind(_control);
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Time.timeScale = _timeScale;
        }

        [Test]
        public void Open_InitializesNormalizedSlidersWithoutCallingSetters()
        {
            _view.Open();

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_master.value, Is.EqualTo(0.2f));
            Assert.That(_music.value, Is.EqualTo(0.4f));
            Assert.That(_sfx.value, Is.EqualTo(0.6f));
            foreach (Slider slider in new[] { _master, _music, _sfx })
            {
                Assert.That(slider.minValue, Is.Zero);
                Assert.That(slider.maxValue, Is.EqualTo(1f));
            }
            Assert.That(_control.MasterCalls + _control.MusicCalls + _control.SfxCalls, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SliderChange_CallsOnlyItsCorrespondingSetter(int channel)
        {
            _view.Open();
            new[] { _master, _music, _sfx }[channel].value = 0.75f;

            Assert.That(_control.MasterCalls, Is.EqualTo(channel == 0 ? 1 : 0));
            Assert.That(_control.MusicCalls, Is.EqualTo(channel == 1 ? 1 : 0));
            Assert.That(_control.SfxCalls, Is.EqualTo(channel == 2 ? 1 : 0));
            Assert.That(_control.LastValue, Is.EqualTo(0.75f));
        }

        [Test]
        public void BackButton_ClosesAndRaisesClosed()
        {
            int calls = 0;
            _view.Closed += () => calls++;
            _view.Open();
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

        [Test]
        public void OpenWithoutControl_WarnsOnceAndSliderChangesAreIgnored()
        {
            Assert.That(AudioManager.Instance == null, Is.True, "Test requires no live audio singleton.");
            _view.Bind(null);
            LogAssert.Expect(LogType.Warning, "SettingsPanelView has no audio volume control; changes will be ignored.");
            _view.Open();
            _master.value = 0.3f;
            _music.value = 0.5f;
            _sfx.value = 0.7f;
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_control.MasterCalls + _control.MusicCalls + _control.SfxCalls, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private Slider CreateSlider(string name, string field)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Slider));
            child.transform.SetParent(_root.transform);
            Slider slider = child.GetComponent<Slider>();
            slider.minValue = -1f;
            slider.maxValue = 2f;
            SetField(field, slider);
            return slider;
        }

        private void SetField(string name, object value) => typeof(SettingsPanelView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, value);

        private sealed class FakeAudioVolumeControl : IAudioVolumeControl
        {
            public float MasterVolume => 0.2f;
            public float MusicVolume => 0.4f;
            public float SfxVolume => 0.6f;
            public int MasterCalls { get; private set; }
            public int MusicCalls { get; private set; }
            public int SfxCalls { get; private set; }
            public float LastValue { get; private set; }
            public void SetMasterVolume(float value) { MasterCalls++; LastValue = value; }
            public void SetMusicVolume(float value) { MusicCalls++; LastValue = value; }
            public void SetSfxVolume(float value) { SfxCalls++; LastValue = value; }
        }
    }
}
#endif

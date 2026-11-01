#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class LocalPlayerDownedPostProcessPresenterTests
    {
        private DownedPostProcessConfig _config;
        private GameObject _host;
        private LocalPlayerDownedPostProcessPresenter _presenter;
        private FakeSource _source;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DownedPostProcessConfig>();
            _host = new GameObject("DownedPostProcessPresenterTest");
            _presenter = _host.AddComponent<LocalPlayerDownedPostProcessPresenter>();
            var serialized = new SerializedObject(_presenter);
            serialized.FindProperty("_config").objectReferenceValue = _config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _source = new FakeSource();
            _presenter.Bind(_source);
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null)
            {
                _presenter.Unbind();
            }

            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Tick_WhileActive_CreatesNoVolume()
        {
            _presenter.Tick(1f);

            Assert.That(_presenter.ActiveVolume, Is.Null);
        }

        [Test]
        public void Tick_WhileDowned_DrivesAGlobalBlackAndWhiteVolume()
        {
            _source.IsDowned = true;

            _presenter.Tick(_config.FadeInSeconds);

            Volume volume = _presenter.ActiveVolume;
            Assert.That(volume, Is.Not.Null);
            Assert.That(volume.isGlobal, Is.True);
            Assert.That(volume.priority, Is.EqualTo(_config.VolumePriority));
            Assert.That(volume.weight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(volume.sharedProfile.TryGet(out ColorAdjustments adjustments), Is.True);
            Assert.That(adjustments.saturation.overrideState, Is.True);
            Assert.That(adjustments.saturation.value, Is.EqualTo(-100f));
            Assert.That(volume.sharedProfile.TryGet(out Vignette vignette), Is.True);
            Assert.That(vignette.intensity.value, Is.EqualTo(_config.VignetteIntensity).Within(1e-5f));
        }

        [Test]
        public void Tick_ReviveInProgress_RestoresSomeColour()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);

            _source.IsReviving = true;
            _source.Progress = 1f;
            _presenter.Tick(_config.ReviveResponseSeconds);

            _presenter.ActiveVolume.sharedProfile.TryGet(out ColorAdjustments adjustments);
            Assert.That(adjustments.saturation.value, Is.GreaterThan(-100f));
        }

        [Test]
        public void Tick_AfterReviveFadeOut_ReleasesTheVolume()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);
            Volume volume = _presenter.ActiveVolume;
            VolumeProfile profile = volume.sharedProfile;

            _source.IsDowned = false;
            _presenter.Tick(_config.FadeOutSeconds * 0.5f);
            Assert.That(_presenter.ActiveVolume, Is.Not.Null, "colour must return progressively");

            _presenter.Tick(_config.FadeOutSeconds * 0.5f);
            Assert.That(_presenter.ActiveVolume, Is.Null);
            Assert.That(volume == null, Is.True);
            Assert.That(profile == null, Is.True);
        }

        [Test]
        public void Unbind_WhileDowned_RemovesTheEffectImmediately()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);
            Volume volume = _presenter.ActiveVolume;

            _presenter.Unbind();
            _presenter.Tick(1f);

            Assert.That(volume == null, Is.True);
            Assert.That(_presenter.ActiveVolume, Is.Null);
        }

        [Test]
        public void Tick_WhenTheAvatarDisappears_RemovesTheEffect()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);
            Volume volume = _presenter.ActiveVolume;

            _source.IsAvailable = false;
            _presenter.Tick(0.01f);

            Assert.That(_presenter.ActiveVolume, Is.Null);
            Assert.That(volume == null, Is.True);
        }

        [Test]
        public void DestroyingThePresenterObject_DestroysItsVolumeWithoutCallbacks()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);
            Volume volume = _presenter.ActiveVolume;
            VolumeProfile profile = volume.sharedProfile;

            // EditMode skips OnDestroy here, like any teardown path that never reaches the callback.
            Object.DestroyImmediate(_host);
            bool leaked = volume != null;
            if (leaked)
            {
                Object.DestroyImmediate(volume.gameObject);
            }

            Object.DestroyImmediate(profile);
            Assert.That(leaked, Is.False, "the Volume must not outlive its presenter");
        }

        [Test]
        public void Volume_IsNotHiddenFromSceneUnload()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);

            Assert.That(_presenter.ActiveVolume.gameObject.hideFlags & HideFlags.DontSave, Is.Not.EqualTo(HideFlags.DontSave));
        }

        [Test]
        public void Rebind_StartsFromFullColour()
        {
            _source.IsDowned = true;
            _presenter.Tick(_config.FadeInSeconds);

            var next = new FakeSource { IsDowned = true };
            _presenter.Bind(next);
            _presenter.Tick(1f / 60f);

            Assert.That(_presenter.ActiveVolume.weight, Is.LessThan(0.1f));
        }

        private sealed class FakeSource : IDownedPostProcessSource
        {
            public bool IsAvailable { get; set; } = true;
            public bool IsDowned { get; set; }
            public float ReserveFraction { get; set; } = 1f;
            public bool IsReviving { get; set; }
            public float Progress { get; set; }

            public bool TryGetReviveProgress(out float progress)
            {
                progress = Progress;
                return IsReviving;
            }
        }
    }
}
#endif

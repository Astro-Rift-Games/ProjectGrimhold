using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders the Downed black-and-white post-process for the local player only. Bound by
/// <see cref="LocalPlayerHudBinder"/> to the Input Authority avatar, so proxies and other peers
/// never see it. Owns a runtime global <see cref="Volume"/> with its own profile that exists only
/// while the effect is visible; the scene ambient Volume and shared assets are never touched.
/// The HUD is a Screen Space Overlay canvas, so post-processing never alters it.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerDownedPostProcessPresenter : MonoBehaviour
{
    [SerializeField]
    private DownedPostProcessConfig _config;

    private DownedPostProcessBlend _blend;
    private IDownedPostProcessSource _source;
    private bool _reportedMissingConfig;

    private GameObject _volumeObject;
    private Volume _volume;
    private VolumeProfile _profile;
    private ColorAdjustments _adjustments;
    private Vignette _vignette;

    /// <summary>Test seam: the runtime Volume while the effect is visible, otherwise null.</summary>
    internal Volume ActiveVolume => _volume;

    public void Bind(PlayerCharacter character)
    {
        if (character == null ||
            !character.TryGetComponent(out PlayerDownedStateNetworkController downedState) ||
            !character.TryGetComponent(out PlayerDownedRecoveryNetworkController recovery))
        {
            Unbind();
            return;
        }

        Bind(new PlayerDownedPostProcessSource(character, downedState, recovery));
    }

    internal void Bind(IDownedPostProcessSource source)
    {
        Unbind();
        if (source == null || !HasConfig())
        {
            return;
        }

        _blend ??= new DownedPostProcessBlend(_config);
        _source = source;
    }

    /// <summary>Drops the effect immediately; covers disconnect, respawn and return to Town.</summary>
    public void Unbind()
    {
        _source = null;
        ResetPresentation();
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        // A disabled HUD must not leave the screen tinted; a later Tick resumes from Active.
        ResetPresentation();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    internal void Tick(float deltaTime)
    {
        if (_source == null || _blend == null)
        {
            return;
        }

        if (!_source.IsAvailable)
        {
            ResetPresentation();
            return;
        }

        bool isReviving = _source.TryGetReviveProgress(out float reviveProgress);
        DownedPostProcessFrame frame = _blend.Step(
            _source.IsDowned,
            _source.ReserveFraction,
            isReviving,
            reviveProgress,
            deltaTime);

        if (!frame.IsActive)
        {
            ReleaseVolume();
            return;
        }

        EnsureVolume();
        Apply(frame);
    }

    private void Apply(in DownedPostProcessFrame frame)
    {
        _volume.weight = frame.Weight;
        _adjustments.saturation.value = frame.Saturation;
        _adjustments.contrast.value = frame.Contrast;
        _adjustments.postExposure.value = frame.PostExposure;

        _vignette.color.value = frame.VignetteColor;
        _vignette.intensity.value = frame.VignetteIntensity;
        _vignette.smoothness.value = frame.VignetteSmoothness;
    }

    private void EnsureVolume()
    {
        if (_volume != null)
        {
            return;
        }

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "LocalDownedPostProcessProfile";
        _profile.hideFlags = HideFlags.DontSave;

        _adjustments = _profile.Add<ColorAdjustments>();
        _adjustments.saturation.overrideState = true;
        _adjustments.contrast.overrideState = true;
        _adjustments.postExposure.overrideState = true;

        _vignette = _profile.Add<Vignette>();
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;

        // Owned by the presenter's hierarchy so it can never outlive the HUD, even when a teardown
        // path skips OnDestroy; never DontSave, which would survive scene loads.
        _volumeObject = new GameObject("LocalDownedPostProcessVolume");
        _volumeObject.transform.SetParent(transform, false);
        _volume = _volumeObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = _config.VolumePriority;
        _volume.weight = 0f;
        _volume.sharedProfile = _profile;
    }

    private void ReleaseVolume()
    {
        if (_volumeObject != null)
        {
            DestroyObject(_volumeObject);
        }

        if (_profile != null)
        {
            foreach (VolumeComponent component in _profile.components)
            {
                component.Release();
                DestroyObject(component);
            }

            DestroyObject(_profile);
        }

        _volumeObject = null;
        _volume = null;
        _profile = null;
        _adjustments = null;
        _vignette = null;
    }

    private void ResetPresentation()
    {
        ReleaseVolume();
        _blend?.Reset();
    }

    private bool HasConfig()
    {
        if (_config != null)
        {
            return true;
        }

        if (!_reportedMissingConfig)
        {
            _reportedMissingConfig = true;
            Debug.LogError($"{nameof(LocalPlayerDownedPostProcessPresenter)} is missing its {nameof(DownedPostProcessConfig)}.", this);
        }

        return false;
    }

    private static void DestroyObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

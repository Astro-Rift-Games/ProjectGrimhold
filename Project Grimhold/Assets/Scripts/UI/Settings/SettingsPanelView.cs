using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsPanelView : MonoBehaviour
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button _backButton;

    private IAudioVolumeControl _boundControl;
    private IAudioVolumeControl _control;

    public bool IsOpen => UiPanelVisibility.IsVisible(gameObject);
    public event Action Closed;

    private void Awake()
    {
        ConfigureSlider(_masterSlider);
        ConfigureSlider(_musicSlider);
        ConfigureSlider(_sfxSlider);
        _masterSlider?.onValueChanged.AddListener(OnMasterChanged);
        _musicSlider?.onValueChanged.AddListener(OnMusicChanged);
        _sfxSlider?.onValueChanged.AddListener(OnSfxChanged);
        _backButton?.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        _masterSlider?.onValueChanged.RemoveListener(OnMasterChanged);
        _musicSlider?.onValueChanged.RemoveListener(OnMusicChanged);
        _sfxSlider?.onValueChanged.RemoveListener(OnSfxChanged);
        _backButton?.onClick.RemoveListener(Close);
    }

    public void Bind(IAudioVolumeControl control)
    {
        _boundControl = control;
        _control = control;
    }

    public void Open()
    {
        _control = _boundControl ?? AudioManager.Instance;
        UiPanelVisibility.SetVisible(gameObject, true);
        if (_control == null)
        {
            Debug.LogWarning("SettingsPanelView has no audio volume control; changes will be ignored.", this);
            return;
        }

        _masterSlider?.SetValueWithoutNotify(_control.MasterVolume);
        _musicSlider?.SetValueWithoutNotify(_control.MusicVolume);
        _sfxSlider?.SetValueWithoutNotify(_control.SfxVolume);
    }

    public void Close()
    {
        UiPanelVisibility.SetVisible(gameObject, false);
        Closed?.Invoke();
    }

    private static void ConfigureSlider(Slider slider)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void OnMasterChanged(float value) => _control?.SetMasterVolume(value);
    private void OnMusicChanged(float value) => _control?.SetMusicVolume(value);
    private void OnSfxChanged(float value) => _control?.SetSfxVolume(value);
}

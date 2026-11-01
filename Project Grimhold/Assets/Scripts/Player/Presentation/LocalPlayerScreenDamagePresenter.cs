using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalPlayerScreenDamagePresenter : MonoBehaviour
{
    [SerializeField] private ScreenDamageConfig _config;
    [SerializeField] private ScreenDamageView _view;
    private IScreenDamageSource _source;
    private ScreenDamageFeedbackState _state;
    private Transform _avatar;
    private ScreenDamageCameraEffect _cameraEffect;

    public void Bind(PlayerCharacter character)
    {
        Bind(character == null ? null : new PlayerScreenDamageSource(character));
        _avatar = character != null ? character.transform : null;
    }
    internal void Bind(IScreenDamageSource source)
    {
        Unbind();
        if (source == null) return;
        if (_config == null || _view == null)
        {
            Debug.LogError("LocalPlayerScreenDamagePresenter has missing serialized dependencies.", this);
            return;
        }
        _state = new ScreenDamageFeedbackState(_config);
        _source = source;
        Tick(0f);
    }

    public void Unbind()
    {
        _source = null;
        Clear();
        _avatar = null;
        _cameraEffect = null;
    }
    internal void Tick(float deltaTime)
    {
        if (_source == null || _state == null) return;
        if (!_source.TryRead(out float health, out float maximum)) { Clear(); return; }
        _state.Step(health, maximum, true, deltaTime, ScreenDamageAccessibility.Intensity);
        _view.Present(_state.Severity, _state.Opacity, _config.Tint);
        if (_avatar != null)
        {
            // Reuse the existing local-camera composition; no scene-wide lookup or new registry.
            if (_cameraEffect == null && LocalCameraController.Instance != null)
                LocalCameraController.Instance.TryGetComponent(out _cameraEffect);
            if (_cameraEffect != null)
                _cameraEffect.Apply(this, _avatar, _config, _state.PeripheralWeight);
        }
    }
    private void Clear()
    {
        _state?.Reset();
        if (_view != null) _view.Clear();
        if (_cameraEffect != null) _cameraEffect.Clear(this);
    }
    private void Update() => Tick(Time.unscaledDeltaTime);
    private void OnDisable() => Clear();
    private void OnDestroy() => Unbind();
}

using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalPlayerScreenDamagePresenter : MonoBehaviour
{
    [SerializeField] private ScreenDamageConfig _config;
    [SerializeField] private ScreenDamageView _view;
    private IScreenDamageSource _source;
    private ScreenDamageFeedbackState _state;

    public void Bind(PlayerCharacter character) => Bind(character == null ? null : new PlayerScreenDamageSource(character));
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
    }
    internal void Tick(float deltaTime)
    {
        if (_source == null || _state == null) return;
        if (!_source.TryRead(out float health, out float maximum)) { Clear(); return; }
        _state.Step(health, maximum, true, deltaTime, ScreenDamageAccessibility.Intensity);
        _view.Present(_state.Severity, _state.Opacity, _config.Tint);
    }
    private void Clear() { _state?.Reset(); if (_view != null) _view.Clear(); }
    private void Update() => Tick(Time.unscaledDeltaTime);
    private void OnDisable() => Clear();
    private void OnDestroy() => Unbind();
}

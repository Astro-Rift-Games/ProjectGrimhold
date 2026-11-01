using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScreenDamageOptionsView : MonoBehaviour
{
    [SerializeField] private Slider _intensity;
    private void Awake()
    {
        if (_intensity == null) return;
        _intensity.minValue = 0; _intensity.maxValue = 1; _intensity.wholeNumbers = false;
        _intensity.onValueChanged.AddListener(ScreenDamageAccessibility.SetIntensity);
    }
    private void OnEnable() { if (_intensity != null) _intensity.SetValueWithoutNotify(ScreenDamageAccessibility.Intensity); }
    private void OnDestroy() { if (_intensity != null) _intensity.onValueChanged.RemoveListener(ScreenDamageAccessibility.SetIntensity); }
}

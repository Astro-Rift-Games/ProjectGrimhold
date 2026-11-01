using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScreenDamageView : MonoBehaviour
{
    [SerializeField] private Image _lower;
    [SerializeField] private Image _upper;
    [SerializeField] private Sprite[] _stages;

    public void Present(float severity, float opacity, Color tint)
    {
        if (_lower == null || _upper == null || _stages == null || _stages.Length < 2) return;
        float index = Mathf.Clamp01(severity) * (_stages.Length - 1);
        int low = Mathf.FloorToInt(index);
        int high = Mathf.Min(low + 1, _stages.Length - 1);
        float blend = index - low;
        Apply(_lower, _stages[low], tint, opacity * (1f - blend));
        Apply(_upper, _stages[high], tint, opacity * blend);
    }

    public void Clear()
    {
        if (_lower != null) _lower.enabled = false;
        if (_upper != null) _upper.enabled = false;
    }

    private static void Apply(Image image, Sprite sprite, Color tint, float alpha)
    {
        image.raycastTarget = false;
        image.sprite = sprite;
        tint.a *= Mathf.Clamp01(alpha);
        image.color = tint;
        image.enabled = sprite != null && tint.a > .0001f;
    }
    private void OnDisable() => Clear();
}

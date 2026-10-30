using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Swaps empty and equipped art on one Image while retaining each authored visual style.</summary>
[Serializable]
public sealed class EquipmentSlotIconPresentation
{
    [SerializeField] private Sprite _sprite;
    [SerializeField] private Style _emptyStyle;
    [SerializeField] private Style _itemStyle;

    public bool TryPresent(Image image, Sprite itemSprite, bool empty, float itemAlpha = 1f)
    {
        if (image == null || _sprite == null) return false;
        (empty ? _emptyStyle : _itemStyle).Apply(image, empty ? 1f : itemAlpha);
        image.overrideSprite = null;
        image.sprite = empty ? _sprite : itemSprite;
        image.enabled = image.sprite != null;
        return true;
    }

    [Serializable]
    private struct Style
    {
        [SerializeField] private Vector2 _anchorMin;
        [SerializeField] private Vector2 _anchorMax;
        [SerializeField] private Vector2 _anchoredPosition;
        [SerializeField] private Vector2 _sizeDelta;
        [SerializeField] private Vector2 _pivot;
        [SerializeField] private Color _color;

        public void Apply(Image image, float alpha)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = _anchorMin;
            rect.anchorMax = _anchorMax;
            rect.pivot = _pivot;
            rect.sizeDelta = _sizeDelta;
            rect.anchoredPosition = _anchoredPosition;
            Color color = _color;
            color.a *= alpha;
            image.color = color;
        }
    }
}

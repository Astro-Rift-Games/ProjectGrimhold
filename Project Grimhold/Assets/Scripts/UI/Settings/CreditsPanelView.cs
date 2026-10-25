using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CreditsPanelView : MonoBehaviour
{
    [SerializeField] private Button _backButton;

    public bool IsOpen => UiPanelVisibility.IsVisible(gameObject);
    public event Action Closed;

    private void Awake() => _backButton?.onClick.AddListener(Close);

    private void OnDestroy() => _backButton?.onClick.RemoveListener(Close);

    public void Open() => UiPanelVisibility.SetVisible(gameObject, true);

    public void Close()
    {
        UiPanelVisibility.SetVisible(gameObject, false);
        Closed?.Invoke();
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders and emits one local contextual inventory action.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidLootContextActionButton : MonoBehaviour
{
    [SerializeField]
    private Button _button;

    [SerializeField]
    private TMP_Text _label;

    private LootContextActionId _actionId;

    public event Action<LootContextActionId> Invoked;

    private void Awake()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClicked);
        }
    }

    public void Present(in LootContextActionDescriptor descriptor)
    {
        _actionId = descriptor.Id;
        if (_label != null)
        {
            _label.text = descriptor.Label ?? string.Empty;
        }

        if (_button != null)
        {
            _button.interactable = descriptor.IsEnabled;
        }
    }

    private void OnClicked()
    {
        if (_button != null && _button.interactable && _actionId.IsValid)
        {
            Invoked?.Invoke(_actionId);
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterCreationPanelView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _characterNameInput;
    [SerializeField] private Button _createButton;
    [SerializeField] private TextMeshProUGUI _statusText;

    public string CharacterName => _characterNameInput.text.Trim();

    public void SetInteractable(bool interactable)
    {
        _characterNameInput.interactable = interactable;
        _createButton.interactable = interactable;
    }

    public void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }

    public void AddCreateListener(UnityEngine.Events.UnityAction action)
        => _createButton.onClick.AddListener(action);

    public void RemoveCreateListener(UnityEngine.Events.UnityAction action)
        => _createButton.onClick.RemoveListener(action);
}

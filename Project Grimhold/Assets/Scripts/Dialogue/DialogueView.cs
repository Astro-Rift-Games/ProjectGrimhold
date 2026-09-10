using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pasive presentation layer for the dialogue system.
/// Implements IDialogueView to display text, portraits, and speaker names.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueView : MonoBehaviour, IDialogueView
{
    [Header("UI Elements")]
    [Tooltip("The root container for the dialogue panel. Toggled on/off.")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("Text element for displaying the speaker's name.")]
    [SerializeField] private TMP_Text _speakerNameText;

    [Tooltip("Text element for displaying the typed dialogue content.")]
    [SerializeField] private TMP_Text _dialogueText;

    [Tooltip("Image element for the speaker's portrait. Hidden if no portrait is provided.")]
    [SerializeField] private Image _portraitImage;

    [Tooltip("Optional indicator to show when the player can continue.")]
    [SerializeField] private GameObject _continueIndicator;

    public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

    public void Show(string speakerName, Sprite portrait)
    {
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(true);
        }

        if (_speakerNameText != null)
        {
            _speakerNameText.text = speakerName;
        }

        if (_portraitImage != null)
        {
            if (portrait != null)
            {
                _portraitImage.sprite = portrait;
                _portraitImage.gameObject.SetActive(true);
            }
            else
            {
                _portraitImage.gameObject.SetActive(false);
            }
        }

        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
        }
    }

    public void UpdateTypedText(string partialText)
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = partialText;
        }
    }

    public void CompleteText(string fullText)
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = fullText;
        }
    }

    public void Hide()
    {
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(false);
        }
    }
}

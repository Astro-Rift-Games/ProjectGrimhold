using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored modal of the Town loss notice. It owns no session or inventory state.</summary>
[DisallowMultipleComponent]
public sealed class TownRaidLossNoticeView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_Text _itemsText;
    [SerializeField] private Button _continueButton;
    [SerializeField] private TMP_Text _continueLabel;

    public event Action ContinueRequested;

    public bool IsOpen => _root != null && _root.activeSelf;

    private void Awake()
    {
        _continueButton?.onClick.AddListener(OnContinue);
        _root?.SetActive(false);
    }

    private void OnDestroy()
    {
        _continueButton?.onClick.RemoveListener(OnContinue);
    }

    public void Present(TownRaidLossNoticePresentation presentation)
    {
        if (_root == null || _titleText == null || _bodyText == null || _itemsText == null ||
            _continueButton == null || _continueLabel == null)
        {
            Debug.LogError($"{nameof(TownRaidLossNoticeView)} is missing serialized references.", this);
            return;
        }

        _titleText.text = presentation.Title;
        _bodyText.text = presentation.Body;
        _itemsText.text = string.Join("\n", presentation.Lines);
        _continueLabel.text = presentation.ContinueLabel;
        _root.SetActive(true);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }
    }

    public void Hide() => _root?.SetActive(false);

    private void OnContinue() => ContinueRequested?.Invoke();
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TownPartyInvitationView : MonoBehaviour
{
    [SerializeField]
    private GameObject _pendingRoot;

    [SerializeField]
    private TMP_Text _messageText;

    [SerializeField]
    private TMP_Text _countdownText;

    [SerializeField]
    private Button _acceptButton;

    [SerializeField]
    private Button _rejectButton;

    public event Action Accepted;
    public event Action Rejected;

    private void Awake()
    {
        _acceptButton?.onClick.AddListener(OnAccepted);
        _rejectButton?.onClick.AddListener(OnRejected);
    }

    private void OnDestroy()
    {
        _acceptButton?.onClick.RemoveListener(OnAccepted);
        _rejectButton?.onClick.RemoveListener(OnRejected);
    }

    public void PresentIncoming(string inviterName, float remainingSeconds)
    {
        SetPending($"{inviterName} te invitó a su grupo", remainingSeconds, true);
    }

    public void PresentOutgoing(string recipientName, float remainingSeconds)
    {
        SetPending($"Esperando respuesta de {recipientName}", remainingSeconds, false);
    }

    public void HidePending()
    {
        if (_pendingRoot != null)
        {
            _pendingRoot.SetActive(false);
        }
    }

    private void SetPending(string message, float remainingSeconds, bool canRespond)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
        }

        if (_countdownText != null)
        {
            _countdownText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds))} s";
        }

        if (_acceptButton != null)
        {
            _acceptButton.gameObject.SetActive(canRespond);
        }

        if (_rejectButton != null)
        {
            _rejectButton.gameObject.SetActive(canRespond);
        }

        if (_pendingRoot != null)
        {
            _pendingRoot.SetActive(true);
        }
    }

    private void OnAccepted() => Accepted?.Invoke();
    private void OnRejected() => Rejected?.Invoke();
}

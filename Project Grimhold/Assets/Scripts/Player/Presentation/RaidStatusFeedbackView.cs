using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored local feedback surface. All roots, icons, buttons and audio are serialized dependencies.</summary>
[DisallowMultipleComponent]
public sealed class RaidStatusFeedbackView : MonoBehaviour
{
    [SerializeField] private Button _previousSetButton;
    [SerializeField] private Button _nextSetButton;
    [SerializeField] private TMP_Text _activeSetText;
    [SerializeField] private RectTransform _weaponPulseRoot;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _weaponChangedClip;
    [SerializeField, Min(.05f)] private float _weaponPulseDuration = .2f;
    [SerializeField, Min(1f)] private float _weaponPulseScale = 1.12f;
    [SerializeField] private GameObject _noticeRoot;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField, Min(.1f)] private float _noticeDuration = 3f;
    [SerializeField] private GameObject _empowerRoot;
    [SerializeField] private GameObject _purificationRoot;
    [SerializeField] private GameObject _immobilizedRoot;

    private readonly RaidStatusFeedbackState _state = new();
    private Vector3 _weaponBaseScale;
    private float _weaponPulseUntil;
    public event Action SwitchRequested;

    private void Awake()
    {
        if (_weaponPulseRoot != null) _weaponBaseScale = _weaponPulseRoot.localScale;
        _previousSetButton?.onClick.AddListener(RequestSwitch);
        _nextSetButton?.onClick.AddListener(RequestSwitch);
        if (_previousSetButton == null || _nextSetButton == null || _activeSetText == null ||
            _weaponPulseRoot == null || _noticeRoot == null || _noticeText == null ||
            _empowerRoot == null || _purificationRoot == null || _immobilizedRoot == null ||
            _audioSource == null || _weaponChangedClip == null)
            Debug.LogError($"{nameof(RaidStatusFeedbackView)} has missing serialized feedback dependencies.", this);
        Clear();
    }

    private void OnDestroy()
    {
        _previousSetButton?.onClick.RemoveListener(RequestSwitch);
        _nextSetButton?.onClick.RemoveListener(RequestSwitch);
    }

    private void RequestSwitch() => SwitchRequested?.Invoke();

    public void PresentWeaponSet(WeaponSetSlot confirmedSet, bool canRequest)
    {
        if (_activeSetText != null)
            _activeSetText.text = confirmedSet == WeaponSetSlot.SetA ? "A" : confirmedSet == WeaponSetSlot.SetB ? "B" : "—";
        if (_previousSetButton != null) _previousSetButton.interactable = canRequest;
        if (_nextSetButton != null) _nextSetButton.interactable = canRequest;
        if (!_state.ObserveWeaponSet(confirmedSet)) return;
        _weaponPulseUntil = Time.unscaledTime + _weaponPulseDuration;
        if (_audioSource != null && _weaponChangedClip != null) _audioSource.PlayOneShot(_weaponChangedClip);
    }

    public void PresentEffects(bool empowered, bool purified, bool immobilized)
    {
        SetActive(_empowerRoot, empowered);
        SetActive(_purificationRoot, purified);
        SetActive(_immobilizedRoot, immobilized);
    }

    public void ShowNotice(string message)
    {
        _state.ShowNotice(message, Time.unscaledTime, _noticeDuration);
        RefreshNotice();
    }

    public void Clear()
    {
        _state.Reset();
        _weaponPulseUntil = 0;
        if (_weaponPulseRoot != null) _weaponPulseRoot.localScale = _weaponBaseScale;
        if (_activeSetText != null) _activeSetText.text = "—";
        if (_previousSetButton != null) _previousSetButton.interactable = false;
        if (_nextSetButton != null) _nextSetButton.interactable = false;
        PresentEffects(false, false, false);
        RefreshNotice();
    }

    private void Update()
    {
        RefreshNotice();
        if (_weaponPulseRoot == null) return;
        float remaining = Mathf.Clamp01((_weaponPulseUntil - Time.unscaledTime) / Mathf.Max(.05f, _weaponPulseDuration));
        _weaponPulseRoot.localScale = _weaponBaseScale * (1 + (_weaponPulseScale - 1) * Mathf.Sin(remaining * Mathf.PI));
    }

    private void RefreshNotice()
    {
        string notice = _state.ReadNotice(Time.unscaledTime);
        if (_noticeText != null) _noticeText.text = notice;
        SetActive(_noticeRoot, !string.IsNullOrEmpty(notice));
    }

    private static void SetActive(GameObject root, bool active)
    {
        if (root != null && root.activeSelf != active) root.SetActive(active);
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// Presents confirmed local combat outcomes without affecting simulation.
/// Damage values are anchored to confirmed hit points and cooldown rejection pulses
/// the existing local HUD widget.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatFeedbackPresenter : MonoBehaviour
{
    [SerializeField]
    private RectTransform _impactRoot;

    [SerializeField]
    private TMP_Text _impactTemplate;

    [SerializeField, Range(1, 8)]
    private int _impactPoolSize = 4;

    [SerializeField]
    private RectTransform _cooldownIconRoot;

    [SerializeField, Min(0.01f)]
    private float _impactDuration = 0.55f;

    [SerializeField, Min(0f)]
    private float _impactRisePixels = 42f;

    [SerializeField, Min(0.01f)]
    private float _cooldownPulseDuration = 0.18f;

    [SerializeField, Min(1f)]
    private float _cooldownPulseScale = 1.12f;

    private ImpactSlot[] _impactSlots;
    private PlayerCombatNetworkController _combatController;
    private PlayerCharacter _character;
    private Camera _presentationCamera;
    private int _lastConsumedSequence;
    private int _nextImpactSlot;
    private float _cooldownPulseRemaining;
    private Vector3 _cooldownBaseScale = Vector3.one;
    private bool _isBound;

    /// <summary>
    /// Binds feedback to the current Input Authority player and starts from the
    /// current authoritative sequence so old session results are not replayed.
    /// </summary>
    public void Bind(PlayerCombatNetworkController combatController, PlayerCharacter character)
    {
        Unbind();
        if (combatController == null || character == null)
        {
            return;
        }

        EnsureImpactSlots();
        _combatController = combatController;
        _character = character;
        _presentationCamera = Camera.main;
        _lastConsumedSequence = combatController.CurrentCombatFeedbackSequence;
        _cooldownBaseScale = _cooldownIconRoot != null
            ? _cooldownIconRoot.localScale
            : Vector3.one;
        _combatController.CombatFeedbackResolved += OnCombatFeedbackResolved;
        _isBound = true;
    }

    /// <summary>Clears subscriptions and every transient visual for session teardown.</summary>
    public void Unbind()
    {
        if (_isBound && _combatController != null)
        {
            _combatController.CombatFeedbackResolved -= OnCombatFeedbackResolved;
        }

        _combatController = null;
        _character = null;
        _presentationCamera = null;
        _isBound = false;
        ClearPresentation();
    }

    private void Awake()
    {
        EnsureImpactSlots();
        ClearPresentation();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (!_isBound)
        {
            return;
        }

        if (_character == null || !_character.IsAlive)
        {
            ClearPresentation();
            return;
        }

        UpdateImpactSlots();
        UpdateCooldownPulse();
    }

    private void OnCombatFeedbackResolved(CombatPresentationEvent feedbackEvent)
    {
        if (!_isBound || feedbackEvent.Sequence <= _lastConsumedSequence ||
            _character == null || !_character.IsAlive)
        {
            return;
        }

        _lastConsumedSequence = feedbackEvent.Sequence;
        if (feedbackEvent.Kind == CombatFeedbackKind.ConfirmedImpact &&
            feedbackEvent.AppliedDamage > 0f)
        {
            PresentImpact(feedbackEvent.HitPoint, feedbackEvent.AppliedDamage);
            return;
        }

        if (feedbackEvent.Kind == CombatFeedbackKind.AttackRejected &&
            feedbackEvent.AttackFailureReason == AttackFailureReason.CooldownActive)
        {
            _cooldownPulseRemaining = _cooldownPulseDuration;
        }
    }

    private void PresentImpact(Vector2 worldPoint, float appliedDamage)
    {
        if (_impactSlots == null || _impactSlots.Length == 0)
        {
            return;
        }

        int index = _nextImpactSlot % _impactSlots.Length;
        _nextImpactSlot = (_nextImpactSlot + 1) % _impactSlots.Length;
        ImpactSlot slot = _impactSlots[index];
        slot.WorldPoint = worldPoint;
        slot.Remaining = _impactDuration;
        slot.Text.text = appliedDamage.ToString("0.#");
        slot.Text.color = slot.BaseColor;
        slot.Text.gameObject.SetActive(true);
        PositionImpact(slot, 0f);
    }

    private void UpdateImpactSlots()
    {
        if (_impactSlots == null)
        {
            return;
        }

        for (int i = 0; i < _impactSlots.Length; i++)
        {
            ImpactSlot slot = _impactSlots[i];
            if (slot == null)
            {
                continue;
            }
            if (slot.Remaining <= 0f)
            {
                continue;
            }

            slot.Remaining = Mathf.Max(0f, slot.Remaining - Time.deltaTime);
            float progress = 1f - slot.Remaining / _impactDuration;
            PositionImpact(slot, progress);

            Color color = slot.BaseColor;
            color.a *= 1f - progress;
            slot.Text.color = color;
            slot.Text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, progress);
            if (slot.Remaining <= 0f)
            {
                slot.Text.gameObject.SetActive(false);
            }
        }
    }

    private void PositionImpact(ImpactSlot slot, float progress)
    {
        if (_presentationCamera == null || _impactRoot == null)
        {
            return;
        }

        Vector3 screenPoint = _presentationCamera.WorldToScreenPoint(slot.WorldPoint);
        if (screenPoint.z < 0f ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _impactRoot,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            slot.Text.gameObject.SetActive(false);
            slot.Remaining = 0f;
            return;
        }

        localPoint.y += _impactRisePixels * progress;
        slot.Text.rectTransform.anchoredPosition = localPoint;
    }

    private void UpdateCooldownPulse()
    {
        if (_cooldownIconRoot == null)
        {
            return;
        }

        if (_cooldownPulseRemaining <= 0f)
        {
            _cooldownIconRoot.localScale = _cooldownBaseScale;
            return;
        }

        _cooldownPulseRemaining = Mathf.Max(0f, _cooldownPulseRemaining - Time.deltaTime);
        float progress = 1f - _cooldownPulseRemaining / _cooldownPulseDuration;
        float pulse = Mathf.Sin(progress * Mathf.PI);
        _cooldownIconRoot.localScale = _cooldownBaseScale * Mathf.Lerp(1f, _cooldownPulseScale, pulse);
    }

    private void EnsureImpactSlots()
    {
        if (_impactSlots != null)
        {
            return;
        }

        if (_impactTemplate == null || _impactRoot == null)
        {
            _impactSlots = System.Array.Empty<ImpactSlot>();
            return;
        }

        _impactSlots = new ImpactSlot[_impactPoolSize];
        for (int i = 0; i < _impactPoolSize; i++)
        {
            TMP_Text text = Instantiate(_impactTemplate, _impactRoot);
            text.name = $"ImpactValue_{i}";
            text.gameObject.SetActive(false);
            _impactSlots[i] = new ImpactSlot(text);
        }
    }

    private void ClearPresentation()
    {
        _cooldownPulseRemaining = 0f;
        _nextImpactSlot = 0;
        if (_cooldownIconRoot != null)
        {
            _cooldownIconRoot.localScale = _cooldownBaseScale;
        }

        if (_impactSlots == null)
        {
            return;
        }

        for (int i = 0; i < _impactSlots.Length; i++)
        {
            ImpactSlot slot = _impactSlots[i];
            if (slot == null)
            {
                continue;
            }

            slot.Remaining = 0f;
            slot.Text.color = slot.BaseColor;
            slot.Text.rectTransform.localScale = Vector3.one;
            slot.Text.gameObject.SetActive(false);
        }
    }

    private sealed class ImpactSlot
    {
        public TMP_Text Text { get; }
        public Color BaseColor { get; }
        public Vector2 WorldPoint { get; set; }
        public float Remaining { get; set; }

        public ImpactSlot(TMP_Text text)
        {
            Text = text;
            BaseColor = text.color;
        }
    }
}

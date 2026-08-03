using TMPro;
using UnityEngine;

/// <summary>
/// Presents deduplicated local feedback for confirmed pickup deliveries.
/// The raid-inventory screen owns persistent content presentation.
/// </summary>
[DisallowMultipleComponent]
public sealed class LootHudPresenter : MonoBehaviour
{
    [SerializeField]
    private GameObject _toastRoot;

    [SerializeField]
    private TMP_Text _toastText;

    [SerializeField, Min(0f)]
    private float _toastDuration = 1.5f;

    private PlayerLootReceiver _lootReceiver;
    private int _lastGrantSequence;
    private float _toastRemaining;
    private bool _isBound;

    public void Bind(PlayerLootReceiver lootReceiver)
    {
        Unbind();

        _lootReceiver = lootReceiver;
        if (_lootReceiver == null)
        {
            return;
        }

        _lastGrantSequence = _lootReceiver.LootChangeSequence;
        _lootReceiver.LootGranted += OnLootGranted;
        _isBound = true;
    }

    public void Unbind()
    {
        if (_isBound && _lootReceiver != null)
        {
            _lootReceiver.LootGranted -= OnLootGranted;
        }

        _lootReceiver = null;
        _isBound = false;
        HideToast();
    }

    private void Update()
    {
        if (!_isBound || _toastRemaining <= 0f)
        {
            return;
        }

        _toastRemaining -= Time.deltaTime;
        if (_toastRemaining <= 0f)
        {
            HideToast();
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnLootGranted(LootGrantPresentationEvent grantEvent)
    {
        if (grantEvent.Sequence == _lastGrantSequence)
        {
            return;
        }

        _lastGrantSequence = grantEvent.Sequence;
        if (!_lootReceiver.TryResolveDefinition(grantEvent.LootId, out LootDefinition definition))
        {
            Debug.LogError($"{nameof(LootHudPresenter)} could not resolve loot grant metadata.", this);
            return;
        }

        long valueIncrement = (long)grantEvent.Amount * definition.SellValuePerUnit;
        if (_toastText != null)
        {
            _toastText.text = $"+{grantEvent.Amount} {definition.DisplayName}  (+{valueIncrement})";
        }

        if (_toastRoot != null)
        {
            _toastRoot.SetActive(true);
        }

        _toastRemaining = _toastDuration;
    }

    private void HideToast()
    {
        _toastRemaining = 0f;
        if (_toastRoot != null)
        {
            _toastRoot.SetActive(false);
        }
    }
}

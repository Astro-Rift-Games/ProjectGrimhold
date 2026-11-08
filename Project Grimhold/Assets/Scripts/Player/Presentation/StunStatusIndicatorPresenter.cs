using UnityEngine;

/// <summary>
/// Frame-driven provisional feedback only. EntityVisibilityPresenter owns the indicator renderer's
/// visibility/fade, while this component owns its active state. Never writes simulation state.
/// </summary>
[DisallowMultipleComponent]
public sealed class StunStatusIndicatorPresenter : MonoBehaviour
{
    [Tooltip("Same-root component implementing IStunStatus.")]
    [SerializeField] private MonoBehaviour _statusSource;
    [Tooltip("Dedicated child indicator, also registered with EntityVisibilityPresenter.")]
    [SerializeField] private GameObject _indicator;
    private IStunStatus _status;

    private void Start()
    {
        _status = _statusSource as IStunStatus;
        if (_status == null || _statusSource.gameObject != gameObject || _indicator == null ||
            _indicator == gameObject || !_indicator.transform.IsChildOf(transform) ||
            _indicator.GetComponent<SpriteRenderer>() == null)
        {
            Debug.LogError("StunStatusIndicatorPresenter requires a same-root IStunStatus source and a dedicated child SpriteRenderer indicator.", this);
            enabled = false;
            return;
        }
        _indicator.SetActive(false);
    }

    private void LateUpdate()
    {
        bool visible = _statusSource != null && _status != null && _status.IsStunned;
        if (_indicator != null && _indicator.activeSelf != visible) _indicator.SetActive(visible);
    }

    private void OnDisable()
    {
        if (_indicator != null && _indicator != gameObject && _indicator.transform.IsChildOf(transform))
            _indicator.SetActive(false);
    }
}

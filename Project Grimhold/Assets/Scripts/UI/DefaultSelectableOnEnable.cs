using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selects a configured <see cref="Selectable"/> every time this GameObject is enabled so
/// keyboard Submit works without a prior mouse click. Contains no menu logic.
/// </summary>
[DisallowMultipleComponent]
public sealed class DefaultSelectableOnEnable : MonoBehaviour
{
    [SerializeField] private Selectable _selectable;

    private Coroutine _pendingSelection;
    private bool _missingSelectableLogged;

    private void OnEnable()
    {
        if (_selectable == null)
        {
            if (!_missingSelectableLogged)
            {
                _missingSelectableLogged = true;
                Debug.LogError($"[{nameof(DefaultSelectableOnEnable)}] No Selectable assigned.", this);
            }
            return;
        }

        if (EventSystem.current == null)
        {
            // Scene load order: the EventSystem may enable after this component.
            _pendingSelection = StartCoroutine(SelectNextFrame());
            return;
        }

        EventSystem.current.SetSelectedGameObject(_selectable.gameObject);
    }

    private void OnDisable()
    {
        if (_pendingSelection == null) return;

        StopCoroutine(_pendingSelection);
        _pendingSelection = null;
    }

    private IEnumerator SelectNextFrame()
    {
        yield return null;
        _pendingSelection = null;

        if (EventSystem.current == null)
        {
            Debug.LogError($"[{nameof(DefaultSelectableOnEnable)}] No EventSystem available to select the default control.", this);
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(_selectable.gameObject);
    }
}

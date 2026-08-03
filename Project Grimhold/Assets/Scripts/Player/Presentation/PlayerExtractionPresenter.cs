using Fusion;
using UnityEngine;

/// <summary>
/// Projects a player's confirmed extraction state into the player's world visuals.
///
/// This presenter observes <see cref="PlayerExtractionController"/> on every peer
/// and hides only the configured visual roots after extraction. It does not disable
/// the network object, gameplay colliders, camera, HUD or any authoritative system.
/// Its editor execution is limited to lifecycle restoration; network observation is
/// still gated by the spawned Fusion object check.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class PlayerExtractionPresenter : MonoBehaviour
{
    [SerializeField]
    private PlayerExtractionController _extractionController;

    [SerializeField]
    private GameObject[] _visualRoots;

    private bool[] _initialRootStates;
    private bool _hasObservedState;
    private ExtractionState _observedState;

    private void Awake()
    {
        CacheDependencies();
        EnsureInitialRootStates();
    }

    private void OnEnable()
    {
        CacheDependencies();
        EnsureInitialRootStates();
        RestoreVisuals();
        _hasObservedState = false;
        _observedState = ExtractionState.None;
    }

    private void LateUpdate()
    {
        if (!IsSpawned(_extractionController))
        {
            _hasObservedState = false;
            return;
        }

        ExtractionState currentState = _extractionController.State;
        if (!_hasObservedState || _observedState != currentState)
        {
            _hasObservedState = true;
            _observedState = currentState;
            ApplyVisualState(currentState);
            return;
        }

        if (currentState == ExtractionState.Extracted)
        {
            ApplyVisualState(currentState);
        }
    }

    private void OnDisable()
    {
        RestoreVisuals();
        _hasObservedState = false;
    }

    private void OnDestroy()
    {
        RestoreVisuals();
    }

    private void ApplyVisualState(ExtractionState state)
    {
        EnsureInitialRootStates();

        bool hideVisuals = state == ExtractionState.Extracted;
        if (_visualRoots == null)
        {
            return;
        }

        for (int i = 0; i < _visualRoots.Length; i++)
        {
            GameObject visualRoot = _visualRoots[i];
            if (visualRoot == null)
            {
                continue;
            }

            bool targetActive = hideVisuals ? false : GetInitialRootState(i);
            if (visualRoot.activeSelf != targetActive)
            {
                visualRoot.SetActive(targetActive);
            }
        }
    }

    private void RestoreVisuals()
    {
        EnsureInitialRootStates();

        if (_visualRoots == null)
        {
            return;
        }

        for (int i = 0; i < _visualRoots.Length; i++)
        {
            GameObject visualRoot = _visualRoots[i];
            if (visualRoot == null)
            {
                continue;
            }

            bool targetActive = GetInitialRootState(i);
            if (visualRoot.activeSelf != targetActive)
            {
                visualRoot.SetActive(targetActive);
            }
        }
    }

    private void CacheDependencies()
    {
        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }
    }

    private void CaptureInitialRootStates()
    {
        if (_visualRoots == null)
        {
            _initialRootStates = null;
            return;
        }

        _initialRootStates = new bool[_visualRoots.Length];
        for (int i = 0; i < _visualRoots.Length; i++)
        {
            _initialRootStates[i] = _visualRoots[i] != null && _visualRoots[i].activeSelf;
        }
    }

    private void EnsureInitialRootStates()
    {
        if (_visualRoots == null)
        {
            _initialRootStates = null;
            return;
        }

        if (_initialRootStates == null || _initialRootStates.Length != _visualRoots.Length)
        {
            CaptureInitialRootStates();
        }
    }

    private bool GetInitialRootState(int index)
    {
        return _initialRootStates != null &&
            index >= 0 &&
            index < _initialRootStates.Length &&
            _initialRootStates[index];
    }

    private static bool IsSpawned(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.Object != null && behaviour.Object.IsValid;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}

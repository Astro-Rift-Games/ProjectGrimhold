using TMPro;
using UnityEngine;

/// <summary>Displays the configured application version without owning menu state.</summary>
[DisallowMultipleComponent]
public sealed class ApplicationVersionLabel : MonoBehaviour
{
    [Tooltip("The menu's version text. Disable Raycast Target in its Inspector.")]
    [SerializeField] private TMP_Text _versionText;

    private void OnEnable()
    {
        if (_versionText == null)
        {
            Debug.LogError($"[{nameof(ApplicationVersionLabel)}] Version text is not assigned.", this);
            enabled = false;
            return;
        }

        _versionText.text = $"v{Application.version}";
    }
}

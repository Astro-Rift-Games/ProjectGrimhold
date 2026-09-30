using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-only rendering surface for an already resolved Town progression presentation.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownProgressionView : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Image _progressFill;

    public TMP_Text LevelText => _levelText;
    public TMP_Text StatusText => _statusText;
    public Image ProgressFill => _progressFill;

    public void Present(in TownProgressionPresentation presentation)
    {
        _levelText.text = $"Nivel {presentation.Level}";
        _statusText.text = presentation.IsMaximumLevel
            ? "Nivel máximo"
            : $"XP {presentation.CurrentExperience} / {presentation.RequiredExperience}";
        _progressFill.fillAmount = presentation.NormalizedProgress;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

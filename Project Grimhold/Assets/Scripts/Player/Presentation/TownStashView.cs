using UnityEngine;

/// <summary>
/// Local screen lifetime for the persistent stash prefab used in Town and in the Lobby.
/// The whole screen, including its Canvas, is authored in the prefab; this component owns
/// presentation lifetime only, creates nothing at runtime and contains no stash state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class TownStashView : MonoBehaviour
{
    public bool IsOpen => UiPanelVisibility.IsVisible(gameObject);

    public void Open()
    {
        if (!IsOpen)
        {
            UiPanelVisibility.SetVisible(gameObject, true);
        }
    }

    public void Close()
    {
        if (IsOpen)
        {
            UiPanelVisibility.SetVisible(gameObject, false);
        }
    }
}

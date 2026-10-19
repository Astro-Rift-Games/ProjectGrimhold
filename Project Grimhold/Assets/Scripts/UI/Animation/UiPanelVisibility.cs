using UnityEngine;

/// <summary>
/// Drop-in replacement for <c>root.SetActive(visible)</c> and <c>root.activeSelf</c> in UI views.
/// A root with a <see cref="UiPanelTransition"/> animates; any other root toggles immediately.
/// Called on open/close only, never per frame.
/// </summary>
public static class UiPanelVisibility
{
    public static bool IsVisible(GameObject root)
    {
        if (root == null)
        {
            return false;
        }

        return root.TryGetComponent(out UiPanelTransition transition)
            ? transition.IsShown
            : root.activeSelf;
    }

    public static void SetVisible(GameObject root, bool visible)
    {
        if (root == null)
        {
            return;
        }

        if (!root.TryGetComponent(out UiPanelTransition transition))
        {
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }

            return;
        }

        if (visible)
        {
            transition.Show();
        }
        else
        {
            transition.Hide();
        }
    }
}

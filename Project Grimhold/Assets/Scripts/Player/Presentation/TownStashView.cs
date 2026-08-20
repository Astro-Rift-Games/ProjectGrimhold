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
    public bool IsOpen => gameObject.activeSelf;

    public void Open()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void Close()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}

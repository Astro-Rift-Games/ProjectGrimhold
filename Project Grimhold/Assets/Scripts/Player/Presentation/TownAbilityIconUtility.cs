using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows an ability icon, or a neutral placeholder frame while no art is authored.</summary>
internal static class TownAbilityIconUtility
{
    public static void Apply(Image image, GameObject placeholder, Sprite sprite)
    {
        bool hasSprite = sprite != null;
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = hasSprite;
        }

        if (placeholder != null)
        {
            placeholder.SetActive(!hasSprite);
        }
    }
}

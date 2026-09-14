using UnityEngine;

[DisallowMultipleComponent]
public sealed class TownRaidPreparationDirectoryContext : MonoBehaviour
{
    public TownRaidPreparationDirectory Directory { get; private set; }

    public void Register(TownRaidPreparationDirectory directory)
    {
        if (directory != null)
        {
            Directory = directory;
        }
    }

    public void Unregister(TownRaidPreparationDirectory directory)
    {
        if (Directory == directory)
        {
            Directory = null;
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public sealed class TownPartyDirectoryContext : MonoBehaviour
{
    public TownPartyDirectory Directory { get; private set; }
    public void Register(TownPartyDirectory directory) { if (directory != null) Directory = directory; }
    public void Unregister(TownPartyDirectory directory) { if (Directory == directory) Directory = null; }
}

/// <summary>
/// Notified by the Downed controller, in the same authority, when damage reduced the reserve.
/// Lets a recovery owner interrupt without the Downed controller depending on its concrete type.
/// </summary>
public interface IDownedDamageObserver
{
    void NotifyDownedDamaged();
}

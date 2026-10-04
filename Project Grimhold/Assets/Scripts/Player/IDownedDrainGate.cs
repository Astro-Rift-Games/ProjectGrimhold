/// <summary>
/// Read-only signal consumed by the Downed controller to pause the reserve drain while a
/// recovery session is valid. Derived by the owner, never stored as a duplicate flag.
/// </summary>
public interface IDownedDrainGate
{
    bool IsDrainPaused { get; }
}

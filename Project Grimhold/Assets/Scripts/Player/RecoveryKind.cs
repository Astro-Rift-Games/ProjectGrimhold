/// <summary>Kind of Downed recovery session. <see cref="Self"/> is reserved for a future task.</summary>
public enum RecoveryKind : byte
{
    None = 0,
    Assisted = 1,
    Self = 2
}

/// <summary>
/// Kind of Downed recovery session. <see cref="Handoff"/> is the brief Self-reviver delivery from an
/// Active teammate (GD 13 section 8); on completion it hands over to a <see cref="Self"/> session.
/// </summary>
public enum RecoveryKind : byte
{
    None = 0,
    Assisted = 1,
    Self = 2,
    Handoff = 3
}

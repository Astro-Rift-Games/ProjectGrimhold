/// <summary>How the last assisted recovery session ended (presentation record, never gameplay input).</summary>
public enum RecoveryOutcomeKind : byte
{
    None = 0,
    Interrupted = 1,
    Completed = 2
}

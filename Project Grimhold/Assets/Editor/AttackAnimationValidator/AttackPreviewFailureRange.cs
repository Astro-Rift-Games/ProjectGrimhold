#if UNITY_EDITOR
public sealed class AttackPreviewFailureRange
{
    public AttackPreviewCheckId Id { get; }
    public AttackPreviewHand Hand { get; }
    public AttackPreviewSeverity Severity { get; }
    public float StartSeconds { get; }
    public float EndSeconds { get; internal set; }
    internal AttackPreviewFailureRange(AttackPreviewCheck check, float seconds)
    {
        Id = check.Id;
        Hand = check.Hand;
        Severity = check.Severity;
        StartSeconds = EndSeconds = seconds;
    }
}
#endif

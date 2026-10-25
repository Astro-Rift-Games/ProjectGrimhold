#if UNITY_EDITOR

    public enum AttackPreviewCheckId { WeaponGripOnHand, GripTransformOnHand, HandSpriteIdentity, DepthOrder, VfxOrigin, ReturnsToIdle, HandOccludedByWeapon, PresentationConfiguration, SlashArcContract }
    public enum AttackPreviewSeverity { Warning, Error }
    public enum AttackPreviewHand { Main, Off }

    /// <summary>Evaluate emits violations; Measurements also includes passing observations.
    /// Distances use the holding sprite's pixels unless the message documents another unit.</summary>
    public sealed class AttackPreviewCheck
    {
        public AttackPreviewCheckId Id { get; }
        public AttackPreviewSeverity Severity { get; }
        public AttackPreviewHand Hand { get; }
        public float TimeSeconds { get; }
        public float MeasuredValue { get; }
        public float Threshold { get; }
        public string Message { get; }
        public bool IsViolation { get; }
        public AttackPreviewCheck(AttackPreviewCheckId id, AttackPreviewSeverity severity, AttackPreviewHand hand,
            float timeSeconds, float measuredValue, float threshold, string message, bool isViolation = true)
        {
            Id = id; Severity = severity; Hand = hand; TimeSeconds = timeSeconds;
            MeasuredValue = measuredValue; Threshold = threshold; Message = message; IsViolation = isViolation;
        }
    }
#endif

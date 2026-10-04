public enum AbilityActivationFailure : byte
{
    None,
    PlayerUnavailable,
    MissingBehaviour,
    RequirementsNotMet,
    AlreadyExecuting,
    Cooldown,
    ResourceUnavailable,
    InsufficientResource,
    BehaviourRejected,
    InvalidPlan
}

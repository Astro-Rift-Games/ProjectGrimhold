/// <summary>Outcome of a persistent unlocked-ability repertoire mutation.</summary>
public enum AbilityUnlockResult
{
    Success,
    AlreadyUnlocked,
    InvalidAbility,
    UnknownAbility,
    ProfileUnavailable,
    PersistenceFailed
}

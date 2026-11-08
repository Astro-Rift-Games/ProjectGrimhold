/// <summary>Read-only replicated Stun status for HUD and presentation on every peer.</summary>
public interface IStunStatus
{
    bool IsStunned { get; }
    float RemainingStunSeconds { get; }
}

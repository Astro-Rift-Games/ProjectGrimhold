#if UNITY_INCLUDE_TESTS
using System.Reflection;
using Fusion;

/// <summary>
/// Stages cooldown state from a Fusion simulation tick so the HUD integration
/// tests can observe the production combat query without synthesizing device input.
/// </summary>
public sealed class PrimaryAttackStatusSimulationDriver : SimulationBehaviour
{
    private static readonly PropertyInfo AttackCooldownProperty =
        typeof(PlayerCombatNetworkController).GetProperty(
            "AttackCooldown",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public PlayerCombatNetworkController Target { get; set; }
    public float RequestedCooldownSeconds { get; set; }
    public bool IsRequested { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (!IsRequested || Target == null)
        {
            return;
        }

        IsRequested = false;
        TickTimer timer = RequestedCooldownSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, RequestedCooldownSeconds)
            : TickTimer.None;
        AttackCooldownProperty.SetValue(Target, timer);
    }
}
#endif

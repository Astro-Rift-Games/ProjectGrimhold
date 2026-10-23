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
    private static readonly PropertyInfo AttackCooldownDurationProperty =
        typeof(PlayerCombatNetworkController).GetProperty(
            "AttackCooldownDurationSeconds",
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
        AttackCooldownDurationProperty.SetValue(Target, RequestedCooldownSeconds);
        var equipment = Target.GetComponent<PlayerWeaponEquipmentNetworkController>();
        if (equipment != null && equipment.ActiveWeaponSetSlot != WeaponSetSlot.None)
        {
            var hands = (NetworkArray<WeaponHandAttackState>)typeof(PlayerCombatNetworkController)
                .GetProperty("HandAttacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Target);
            int index = equipment.ActiveWeaponSetSlot == WeaponSetSlot.SetA ? 0 : 2;
            WeaponHandAttackState state = hands[index];
            state.Timing = default;
            state.Timing.TryStart(Runner.Tick, Runner.DeltaTime, RequestedCooldownSeconds, 0f, out _);
            hands.Set(index, state);
        }
    }
}
#endif

#if UNITY_INCLUDE_TESTS
using Fusion;
using UnityEngine;

/// <summary>
/// Applies requested breakable hits from Fusion simulation so tests exercise
/// the authoritative timing used by production combat.
/// </summary>
public sealed class BreakableDamageSimulationDriver : SimulationBehaviour
{
    public BreakableObject Target { get; set; }
    public float DamageAmount { get; set; }
    public int RequestedHits { get; set; }
    public int AppliedRequests { get; private set; }
    public DamageResult FirstResult { get; private set; }
    public DamageResult LastResult { get; private set; }
    public DamageResult FatalResult { get; private set; }
    public NetworkLootPickup PickupTarget { get; set; }
    public EntityId PickupInteractorId { get; set; }
    public bool IsPickupRequested { get; set; }
    public InteractionResult LastInteractionResult { get; private set; }
    public NetworkLootContainerInteractable ContainerTarget { get; set; }
    public EntityId ContainerInteractorId { get; set; }
    public bool IsContainerInteractionRequested { get; set; }
    public InteractionResult LastContainerInteractionResult { get; private set; }

    public override void FixedUpdateNetwork()
    {
        if (Target != null && RequestedHits > 0)
        {
            ApplyRequestedDamage();
        }

        if (PickupTarget != null && IsPickupRequested)
        {
            IsPickupRequested = false;
            LastInteractionResult = PickupTarget.Interact(new InteractionRequest(
                PickupInteractorId,
                PickupTarget.Id,
                Runner.Tick));
        }

        if (ContainerTarget != null && IsContainerInteractionRequested)
        {
            IsContainerInteractionRequested = false;
            LastContainerInteractionResult = ContainerTarget.Interact(new InteractionRequest(
                ContainerInteractorId,
                ContainerTarget.Id,
                Runner.Tick));
        }
    }

    private void ApplyRequestedDamage()
    {
        int hitCount = RequestedHits;
        RequestedHits = 0;
        for (int i = 0; i < hitCount; i++)
        {
            DamageResult result = Target.ApplyDamage(new DamageRequest(
                new EntityId(int.MaxValue),
                Target.Id,
                DamageAmount,
                DamageType.TrueDamage,
                Vector2.down,
                Target.transform.position,
                Runner.Tick));
            if (AppliedRequests == 0)
            {
                FirstResult = result;
            }

            LastResult = result;
            if (result.IsFatal)
            {
                FatalResult = result;
            }
            AppliedRequests++;
        }
    }
}
#endif

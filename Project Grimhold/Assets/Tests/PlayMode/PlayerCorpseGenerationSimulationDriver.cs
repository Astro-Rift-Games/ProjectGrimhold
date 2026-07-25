#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Stages player inventory grants and fatal damage from Fusion simulation for the
/// focused player-corpse integration tests.
/// </summary>
public sealed class PlayerCorpseGenerationSimulationDriver : SimulationBehaviour
{
    private IReadOnlyList<LootEntry> _entries;

    public PlayerCharacter Target { get; set; }
    public PlayerLootReceiver Receiver { get; set; }
    public bool IsRequested { get; set; }
    public bool RepeatFatalDamage { get; set; }
    public DamageResult FirstResult { get; private set; }
    public DamageResult SecondResult { get; private set; }

    public void SetEntries(IReadOnlyList<LootEntry> entries)
    {
        _entries = entries;
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsRequested || Target == null || Receiver == null)
        {
            return;
        }

        IsRequested = false;
        if (_entries != null)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                LootEntry entry = _entries[index];
                var receive = new LootTransferRequest(
                    new EntityId(int.MaxValue),
                    Receiver.Id,
                    entry.LootId,
                    entry.Amount,
                    Runner.Tick);
                if (Receiver.ValidateReceive(receive) == LootTransferFailureReason.None)
                {
                    Receiver.CommitReceive(receive);
                }
            }
        }

        var damage = new DamageRequest(
            new EntityId(int.MaxValue),
            Target.Id,
            1000f,
            DamageType.TrueDamage,
            Vector2.down,
            Target.transform.position,
            Runner.Tick);
        FirstResult = Target.ApplyDamage(damage);
        if (RepeatFatalDamage)
        {
            SecondResult = Target.ApplyDamage(damage);
        }
    }
}
#endif

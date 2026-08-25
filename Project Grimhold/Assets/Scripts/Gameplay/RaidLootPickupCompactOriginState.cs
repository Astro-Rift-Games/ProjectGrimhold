using Fusion;

/// <summary>
/// Fixed single-stack encoding indexed by Dungeon (0) or RaidParticipantId (1..16).
/// The amount for participant 16 is derived from the pickup's replicated total.
/// </summary>
public struct RaidLootPickupCompactOriginState : INetworkStruct
{
    public int DungeonAmount;
    public int Player1Amount;
    public int Player2Amount;
    public int Player3Amount;
    public int Player4Amount;
    public int Player5Amount;
    public int Player6Amount;
    public int Player7Amount;
    public int Player8Amount;
    public int Player9Amount;
    public int Player10Amount;
    public int Player11Amount;
    public int Player12Amount;
    public int Player13Amount;
    public int Player14Amount;
    public int Player15Amount;

    public int GetStoredAmount(int originSlot) => originSlot switch
    {
        0 => DungeonAmount,
        1 => Player1Amount, 2 => Player2Amount, 3 => Player3Amount,
        4 => Player4Amount, 5 => Player5Amount, 6 => Player6Amount,
        7 => Player7Amount, 8 => Player8Amount, 9 => Player9Amount,
        10 => Player10Amount, 11 => Player11Amount, 12 => Player12Amount,
        13 => Player13Amount, 14 => Player14Amount, 15 => Player15Amount,
        _ => 0
    };

    public void SetStoredAmount(int originSlot, int amount)
    {
        switch (originSlot)
        {
            case 0: DungeonAmount = amount; break;
            case 1: Player1Amount = amount; break;
            case 2: Player2Amount = amount; break;
            case 3: Player3Amount = amount; break;
            case 4: Player4Amount = amount; break;
            case 5: Player5Amount = amount; break;
            case 6: Player6Amount = amount; break;
            case 7: Player7Amount = amount; break;
            case 8: Player8Amount = amount; break;
            case 9: Player9Amount = amount; break;
            case 10: Player10Amount = amount; break;
            case 11: Player11Amount = amount; break;
            case 12: Player12Amount = amount; break;
            case 13: Player13Amount = amount; break;
            case 14: Player14Amount = amount; break;
            case 15: Player15Amount = amount; break;
        }
    }
}

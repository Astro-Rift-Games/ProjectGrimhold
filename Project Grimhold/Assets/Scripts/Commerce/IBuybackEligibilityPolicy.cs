/// <summary>
/// Presentation-hint extension point for a future "only upgraded equipment, or items the market
/// does not sell" rule. Backend isBuybackEligible is authoritative; never gate persistence here.
/// </summary>
public interface IBuybackEligibilityPolicy
{
    bool IsEligible(LootDefinition definition);
}

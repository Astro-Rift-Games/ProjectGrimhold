/// <summary>Entry-keyed presentation intentions, separate from normal stock purchase lines.</summary>
public interface IBuybackShopIntentions
{
    bool SetBuyback(string entryId, int amount);
    bool RemoveBuyback(string entryId);
}

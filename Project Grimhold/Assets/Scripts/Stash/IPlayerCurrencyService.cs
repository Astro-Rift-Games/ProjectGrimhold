using System;

/// <summary>
/// Service abstraction for interacting with a player's currency.
/// </summary>
public interface IPlayerCurrencyService
{
    /// <summary>
    /// Retrieves the current currency balance for the specified profile.
    /// </summary>
    long GetCurrency(ProfileId profileId);

    /// <summary>
    /// Attempts to add currency to the profile.
    /// </summary>
    StashOperationResult TryCreditCurrency(ProfileId profileId, long amount);

    /// <summary>
    /// Attempts to consume currency from the profile.
    /// </summary>
    StashOperationResult TryDebitCurrency(ProfileId profileId, long amount);

    /// <summary>
    /// Fired when a profile's currency balance has changed.
    /// </summary>
    event Action<ProfileId> CurrencyChanged;
}

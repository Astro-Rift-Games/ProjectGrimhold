using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

public sealed class BackendTownSessionTicketVerifier : ITownSessionTicketVerifier
{
    private readonly BackendConfiguration _config;
    private readonly string _bearerToken;
    private readonly Dictionary<string, Task<(bool valid, ProfileId profileId, string characterName, string error)>> _verificationCache =
        new Dictionary<string, Task<(bool, ProfileId, string, string)>>(StringComparer.Ordinal);

    public BackendTownSessionTicketVerifier(BackendConfiguration config, string bearerToken)
    {
        _config = config;
        _bearerToken = bearerToken;
    }

    public Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketAsync(string rawTicket)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(ProfileId), string.Empty, "EMPTY_TICKET"));
        }

        if (_verificationCache.TryGetValue(rawTicket, out var existingTask))
        {
            if (!existingTask.IsCompleted)
            {
                return existingTask;
            }

            if (!existingTask.IsFaulted && !existingTask.IsCanceled)
            {
                var cachedResult = existingTask.Result;
                if (cachedResult.valid || !BackendErrorUtility.IsTransportFailure(cachedResult.error))
                {
                    return existingTask;
                }
            }

            _verificationCache.Remove(rawTicket);
        }

        var verificationTask = VerifyTownTicketInternalAsync(rawTicket);
        _verificationCache[rawTicket] = verificationTask;
        return verificationTask;
    }

    private async Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketInternalAsync(string rawTicket)
    {
        var (success, data, error) = await CharacterClient.VerifyTownSessionTicketAsync(_config, _bearerToken, rawTicket);
        if (!success || !data.valid)
        {
            return (false, default, string.Empty, data.error ?? error.error ?? "VERIFICATION_FAILED");
        }

        return (true, new ProfileId(data.characterId), data.characterName, null);
    }
}

public sealed class MockTownSessionTicketVerifier : ITownSessionTicketVerifier
{
    private readonly Dictionary<string, (ProfileId profileId, string name, bool valid, string error)> _registeredTickets =
        new Dictionary<string, (ProfileId, string, bool, string)>(StringComparer.Ordinal);

    public void RegisterTicket(string rawTicket, ProfileId profileId, string characterName, bool valid = true, string error = null)
    {
        _registeredTickets[rawTicket] = (profileId, characterName, valid, error);
    }

    public Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketAsync(string rawTicket)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(ProfileId), string.Empty, "EMPTY_TICKET"));
        }

        if (_registeredTickets.TryGetValue(rawTicket, out var entry))
        {
            return Task.FromResult((entry.valid, entry.profileId, entry.name, entry.error));
        }

        return Task.FromResult((false, default(ProfileId), string.Empty, "UNREGISTERED_TICKET"));
    }
}

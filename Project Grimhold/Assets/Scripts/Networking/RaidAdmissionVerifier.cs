using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

public sealed class BackendRaidAdmissionVerifier : IRaidAdmissionVerifier
{
    private readonly BackendConfiguration _config;
    private readonly string _bearerToken;
    private readonly Dictionary<string, Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)>> _verificationCache =
        new Dictionary<string, Task<(bool, AuthoritativeRaidAdmissionTicket, string)>>(StringComparer.Ordinal);

    public BackendRaidAdmissionVerifier(BackendConfiguration config, string bearerToken)
    {
        _config = config;
        _bearerToken = bearerToken;
    }

    public Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "EMPTY_TICKET"));
        }

        string cacheKey = $"{expectedRaidCode.Value}:{rawTicket}";
        if (_verificationCache.TryGetValue(cacheKey, out var existingTask))
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

            _verificationCache.Remove(cacheKey);
        }

        var verificationTask = VerifyRaidAdmissionTicketInternalAsync(rawTicket, expectedRaidCode);
        _verificationCache[cacheKey] = verificationTask;
        return verificationTask;
    }

    private async Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketInternalAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        var (success, data, error) = await CharacterClient.VerifyAuthoritativeRaidAdmissionTicketAsync(
            _config,
            _bearerToken,
            rawTicket,
            expectedRaidCode.Value);

        if (!success || !data.valid)
        {
            return (false, default, data.error ?? error.error ?? "VERIFICATION_FAILED");
        }

        var snapshot = data.snapshot;
        if (!RaidAdmissionSnapshotMapper.TryProject(snapshot, out var attributes, out var abilities,
            out var items, out var preparedEquipment))
            return (false, default, "INVALID_ADMISSION_SNAPSHOT");

        RaidCode.TryParse(data.raidCode, out RaidCode parsedRaidCode);
        if (!parsedRaidCode.IsValid)
        {
            parsedRaidCode = expectedRaidCode;
        }

        var ticket = new AuthoritativeRaidAdmissionTicket(
            rawTicket,
            new ProfileId(data.characterId),
            parsedRaidCode,
            data.reservationId,
            snapshot.level,
            snapshot.experience,
            attributes,
            abilities,
            items,
            preparedEquipment,
            snapshot.lastAppliedProgressionResultSequence,
            data.expiresAt);

        return (true, ticket, null);
    }

}

public sealed class LocalDeterministicRaidAdmissionVerifier : IRaidAdmissionVerifier
{
    private readonly Dictionary<string, (bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> _registeredTickets =
        new Dictionary<string, (bool, AuthoritativeRaidAdmissionTicket, string)>(StringComparer.Ordinal);

    public void RegisterTicket(string rawTicket, in AuthoritativeRaidAdmissionTicket ticket, bool valid = true, string error = null)
    {
        _registeredTickets[rawTicket] = (valid, ticket, error);
    }

    public Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(
        string rawTicket,
        RaidCode expectedRaidCode)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "EMPTY_TICKET"));
        }

        if (_registeredTickets.TryGetValue(rawTicket, out var entry))
        {
            if (entry.valid && entry.ticket.RaidCode != expectedRaidCode)
            {
                return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "RAID_CODE_MISMATCH"));
            }

            return Task.FromResult((entry.valid, entry.ticket, entry.error));
        }

        return Task.FromResult((false, default(AuthoritativeRaidAdmissionTicket), "UNREGISTERED_TICKET"));
    }
}

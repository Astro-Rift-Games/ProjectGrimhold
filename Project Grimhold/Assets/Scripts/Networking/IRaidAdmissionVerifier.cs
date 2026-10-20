using System.Threading.Tasks;

/// <summary>
/// Verifies an AuthoritativeRaidAdmissionTicket either against the backend or via deterministic validation in test suites.
/// </summary>
public interface IRaidAdmissionVerifier
{
    Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(string rawTicket, RaidCode expectedRaidCode);
}

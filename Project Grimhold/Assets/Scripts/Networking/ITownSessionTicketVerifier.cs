using System.Threading.Tasks;

/// <summary>
/// Verifies a TownSessionTicket either against the backend or via deterministic validation in test suites.
/// Ensures the verifier does not require issuer secrets in client builds.
/// </summary>
public interface ITownSessionTicketVerifier
{
    Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketAsync(string rawTicket);
}

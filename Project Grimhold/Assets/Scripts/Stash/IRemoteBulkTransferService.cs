using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Remote-first Take All / Leave All in one request. Success returns both authoritative lists and per-stack results.</summary>
public interface IRemoteBulkTransferService
{
    Task<(bool success, TransferAllResult data, BackendError error)> TransferAllAsync(bool fromStash);
}

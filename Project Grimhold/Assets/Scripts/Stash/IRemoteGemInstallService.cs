using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Remote-first gem operation. Success returns both authoritative lists; never applies local preview data.</summary>
public interface IRemoteGemInstallService
{
    Task<(bool success, MoveItemResult data, BackendError error)> InstallGemAsync(InstallGemRequest request);
}

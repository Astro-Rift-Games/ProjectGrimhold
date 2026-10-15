using System.Threading.Tasks;

/// <summary>
/// Replaces the local profile with the backend's confirmed state after a rejected or undetermined
/// remote operation. <see cref="ProfileReconciliationService"/> is the production implementation.
/// </summary>
public interface IProfileReconciliation
{
    /// <summary>Returns whether the local profile now matches the backend.</summary>
    Task<bool> ReconcileAsync();
}

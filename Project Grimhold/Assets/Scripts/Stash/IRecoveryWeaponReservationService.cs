using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Reservation transport only: never publishes a revision-only local save.</summary>
public interface IRecoveryWeaponReservationService
{
    Task<(bool success, SaveReservationResult data, BackendError error)> ReserveRecoveryWeaponAsync(string reservationId, System.Func<bool> isCurrent);
    Task<(bool success, BackendError error)> RollbackRecoveryWeaponAsync(string expectedReservationId, System.Func<bool> isCurrent);
}

using UnityEngine;

/// <summary>
/// Componente local asociado al runner para conservar la selección de clase del host
/// cuando no existe un token de conexión disponible en el servidor.
/// </summary>
public sealed class LocalPlayerJoinContext : MonoBehaviour
{
    /// <summary>
    /// Datos de conexión del jugador local.
    /// </summary>
    public PlayerJoinData JoinData { get; private set; }
    public RaidAdmissionData RaidAdmission { get; private set; }
    public bool HasRaidAdmission { get; private set; }
    public AuthoritativeRaidAdmissionTicket AuthoritativeAdmissionTicket { get; private set; }
    public bool HasAuthoritativeAdmissionTicket => AuthoritativeAdmissionTicket.IsValid;

    /// <summary>
    /// Inicializa el contexto local con los datos provistos.
    /// </summary>
    public void Initialize(in PlayerJoinData joinData)
    {
        JoinData = joinData;
        RaidAdmission = default;
        HasRaidAdmission = false;
        AuthoritativeAdmissionTicket = default;
    }

    public void Initialize(
        in PlayerJoinData joinData,
        in RaidAdmissionData raidAdmission,
        in AuthoritativeRaidAdmissionTicket authoritativeAdmissionTicket = default)
    {
        JoinData = joinData;
        RaidAdmission = raidAdmission;
        HasRaidAdmission = raidAdmission.IsValid;
        AuthoritativeAdmissionTicket = authoritativeAdmissionTicket;
    }
}

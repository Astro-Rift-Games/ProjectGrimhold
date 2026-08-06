/// <summary>
/// Representa los datos inmutables de conexión del jugador al unirse a una sesión.
/// </summary>
public readonly struct PlayerJoinData
{
    /// <summary>
    /// Identificador de la clase del jugador.
    /// </summary>
    public PlayerClassId ClassId { get; }

    /// <summary>
    /// Identificador del perfil persistente del jugador.
    /// </summary>
    public ProfileId ProfileId { get; }

    public PlayerJoinData(PlayerClassId classId)
    {
        ClassId = classId;
        ProfileId = default;
    }

    public PlayerJoinData(PlayerClassId classId, ProfileId profileId)
    {
        ClassId = classId;
        ProfileId = profileId;
    }
}

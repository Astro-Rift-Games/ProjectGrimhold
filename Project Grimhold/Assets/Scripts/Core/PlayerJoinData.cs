/// <summary>
/// Representa los datos inmutables de conexión del jugador al unirse a una sesión.
/// </summary>
public readonly struct PlayerJoinData
{
    /// <summary>
    /// Identificador del perfil del jugador para la ejecución actual.
    /// </summary>
    public ProfileId ProfileId { get; }

    public PlayerJoinData(ProfileId profileId)
    {
        ProfileId = profileId;
    }
}

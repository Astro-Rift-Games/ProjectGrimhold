namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Identifica los estados autoritativos de la FSM del Espectro de Catacumbas (US-78 / TASK-272).
    /// </summary>
    public enum SpecterStateType : byte
    {
        Reposo = 0,
        Persecucion = 1,
        Canalizacion = 2,
        Detonacion = 3,
        Recuperacion = 4,
        Desvanecimiento = 5,
        Desvanecido = 6,
        Reaparicion = 7,
        Muerte = 8
    }
}

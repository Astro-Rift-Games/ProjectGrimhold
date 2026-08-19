using UnityEngine;

/// <summary>
/// Provee métodos estáticos para emitir feedback visual de partículas sin requerir 
/// emisores globales, respetando la arquitectura compositiva.
/// </summary>
public static class ParticleEffectPlayer
{
    /// <summary>
    /// Reproduce un ParticleSystem que ya existe en la escena, moviéndolo primero a la posición indicada.
    /// Útil para sistemas que viven dentro de un prefab y sobreviven al evento (ej. daño a character).
    /// Evita instanciación y allocations en runtime.
    /// </summary>
    /// <param name="ps">ParticleSystem a reproducir. Puede ser null.</param>
    /// <param name="position">Posición global donde emitir las partículas.</param>
    public static void PlayInPlace(ParticleSystem ps, Vector2 position)
    {
        if (ps == null)
        {
            return;
        }

        ps.transform.position = position;
        ps.Play();
    }

    /// <summary>
    /// Instancia una copia de un prefab de ParticleSystem en la posición indicada y lo reproduce.
    /// Útil para eventos donde la entidad emisora es despawneada inmediatamente (ej. impactos de proyectil)
    /// o para eventos puntuales independientes (ej. consumo de ítems).
    /// El prefab debe tener configurado Stop Action = Destroy.
    /// </summary>
    /// <param name="prefab">Prefab de ParticleSystem a instanciar. Puede ser null.</param>
    /// <param name="position">Posición global donde emitir las partículas.</param>
    /// <returns>El GameObject instanciado, o null si el prefab era null.</returns>
    public static GameObject InstantiateAndPlay(ParticleSystem prefab, Vector2 position)
    {
        if (prefab == null)
        {
            return null;
        }

        ParticleSystem instance = Object.Instantiate(prefab, position, Quaternion.identity);
        instance.Play();
        return instance.gameObject;
    }
}

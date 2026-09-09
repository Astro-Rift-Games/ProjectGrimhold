# Audio Architecture

Este documento define las responsabilidades, los límites arquitectónicos y la estructura técnica del sistema de audio del proyecto.

## Responsabilidades del Sistema de Audio

El audio es puramente una **capa de presentación**. Al igual que los VFX y la animación visual:
- Reacciona ante eventos del juego (ataques, daño, recolección).
- Lee estado del juego (salud, estados activos).
- Jamás es fuente de verdad.
- Jamás muta el estado de red ni emite eventos que afecten al flujo del gameplay autoritativo.
- No utiliza llamadas remotas (RPCs) propias; debe reproducirse como resultado local de observar el estado simulado (Networked properties o eventos).

## Estructura de Configuraciones (Data-Driven)

El sistema de audio elimina referencias rígidas mediante el uso de **Scriptable Objects** jerárquicos y configurables por los diseñadores:

- **CustomClip**: Struct serializable que define un arreglo de `AudioClip[]` y los parámetros de mezcla (`Volume`, `Pitch`, `SpatialBlend`, `Loop`). Otorga aleatoriedad nativa al proveer el método `GetRandomClip()`, eliminando la repetición artificial en efectos como golpes de espada o pasos.
- **EntityAudioConfig**: Clase base de la que heredan los perfiles concretos (`PlayerAudioConfig`, `WeaponAudioConfig`, etc.).
  - Serializa visualmente en Unity una lista de diccionarios `[string key -> CustomClip]`.
  - Expone el método seguro `TryGetClip(string key, out CustomClip clip)` para acceder a los clips en O(1) en tiempo de ejecución.
  - Elimina las propiedades fijas. Si un arma tiene llaves `"Swing"` o `"Hit"`, el presentador las buscará por string, evitando que las clases C# cambien por cada variante de diseño.

## AudioManager y Ciclo de Vida

El orquestador central es el **AudioManager**:
- Es un Singleton (`AudioManager.Instance`) con `DontDestroyOnLoad`. Solo existe uno durante el tiempo de vida de la aplicación.
- Debe inicializarse desde la escena de `MainMenu` o primera escena en cargar y autoprotegerse destruyendo copias duplicadas de las escenas posteriores.
- No instancia ni destruye objetos frecuentemente. Usa un pool preinstanciado (tamaño configurable, por defecto 16) de objetos `AudioSource` asignados al Mixer de SFX.
- Usa una política de reciclaje (Round-Robin) donde, si el pool se satura, "roba" el AudioSource más antiguo antes de fallar.

## AudioPresenters (Conectores)

Los scripts que disparan el audio (`WeaponAudioPresenter`, `EnemyAudioPresenter`) se rigen por la arquitectura de presentación pasiva:

1. **Armas**: Dado que las armas son `ScriptableObjects` (`WeaponDefinition`) y no prefabs instanciados, `WeaponDefinition` almacena su propio `WeaponAudioConfig`. El `WeaponAudioPresenter` reside en el prefab del Jugador, suscrito a `ICombatController.AttackPerformed`, y resuelve el arma actualmente equipada en `PlayerWeaponEquipmentNetworkController` para disparar el sonido `"Swing"`. Si el arma no tiene audio o clave `"Swing"`, se omite silenciosamente.
2. **Enemigos**: `EnemyAudioPresenter` se ubica en el prefab del enemigo y utiliza **Polling a estado de simulación** en `LateUpdate()` sobre `CharacterBase.Health` y `CharacterBase.IsAlive` (siguiendo el mismo patrón de `DamageFeedbackPresenter`) para disparar `"TakeDamage"` y `"Death"`.


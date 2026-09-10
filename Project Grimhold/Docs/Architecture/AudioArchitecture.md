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

Los scripts que disparan el audio se rigen por la arquitectura de presentación pasiva:

1. **Armas (`WeaponAudioPresenter`)**:
   - Reside en el prefab del Jugador.
   - Resuelve el arma equipada vía `PlayerWeaponEquipmentNetworkController`.
   - Dispara `"Swing"` (melee) o `"Shoot"` (rango) al recibir `ICombatController.AttackPerformed`.
   - Para rango, temporiza el disparo de `"Reload"` al entrar en cooldown.
   - Escucha `CombatFeedbackResolved`: si confirma impacto contra un personaje reproduce `"Attack"`, y si impacta contra un destructible (`BreakableObject`) o el escenario (`WorldCollision`/`Obstacles`) reproduce `"Block"`.
2. **Enemigos (`EnemyAudioPresenter`)**:
   - Ubicado en el prefab del enemigo.
   - Escucha `ICombatController.AttackPerformed` para disparar `"Attack"` (melee) o `"Shoot"` y `"Reload"` (rango).
   - Utiliza **Polling a estado de simulación** en `LateUpdate()` sobre `CharacterBase.Health` para reproducir `"TakeDamage"` y `"Death"`.
   - Observa `IMovementState.IsMoving` para emitir `"Movement"` según el intervalo de pasos.
3. **Jugador (`PlayerAudioPresenter` y `PlayerAnimationAudioListener`)**:
   - `PlayerAudioPresenter`: Ubicado en el prefab raíz del Jugador. Monitorea `CharacterBase.Health` para `"TakeDamage"`/`"Death"`, y expone `PlayAudio(key)` para llamadas manuales.
   - `PlayerAnimationAudioListener`: Ubicado en el GameObject hijo junto al `Animator`. Actúa como puente para los `AnimationEvents` (`PlayAudioEvent(string)` o `PlayFootstep()`), delegando la reproducción hacia el `PlayerAudioPresenter` del padre.
4. **Música (`SceneMusicPresenter`)**:
   - Componente colocado en la raíz de cada escena (`Lobby-Town`, `Gameplay`).
   - Dispara en `Start()` la reproducción en loop de la pista correspondiente (`"Town"`, `"Raid"`) al `AudioManager.Instance`.



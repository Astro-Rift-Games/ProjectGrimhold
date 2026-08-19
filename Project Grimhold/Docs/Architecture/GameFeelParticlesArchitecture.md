# Arquitectura de Partículas de Game Feel

Este documento detalla la arquitectura para el feedback visual basado en partículas al ocurrir eventos de gameplay (impactos, daño, destrucción y consumo).

## Separación de Responsabilidades

El sistema sigue la estricta separación entre **Simulación** y **Presentación** del proyecto.
Las partículas son puramente visuales, no afectan colisiones, no tienen autoridad de red y no se sincronizan como `NetworkObject`.

El mecanismo utilizado se basa en la **Observación** (a través de `LateUpdate()`) para componentes que persisten, y **Suscripción a Eventos** para eventos fugaces o que derivan en un despawn inmediato.

### Mecanismos de Detección

1. **Daño a Characters y Breakables**
   Los componentes de presentación observan las propiedades públicas sincronizadas (`Health`, `IsDestroyed`) desde sus `LateUpdate()`. Cuando se detecta un cambio válido (ej: reducción de vida y estado de vida activo), reproducen el sistema de partículas referenciado dentro de su propio Prefab.
   - **Beneficio**: Reutiliza la vida útil del objeto. No requiere instanciar partículas (cero allocations).

2. **Impactos de Proyectil en Escenario**
   Los proyectiles desaparecen (`Runner.Despawn()`) en el mismo tick en el que impactan, por lo que un sistema de partículas hijo no podría sobrevivir.
   El `NetworkProjectile` dispara un evento C# normal durante su `Despawned(runner, hasState: true)` reportando su posición final y si fue solo impacto con el entorno. El Presenter escucha el evento e **instancia** el Prefab de partículas de impacto (con `StopAction = Destroy`).
   - **Beneficio**: El efecto sobrevive a la entidad que lo originó.

3. **Consumo de Ítems**
   Se utiliza el evento `ConsumeConfirmed` ya existente en `PlayerConsumableNetworkController`. El Presenter reacciona instanciando las partículas asociadas desde el scriptable object de la definición del consumible, en la posición local del jugador.

## Helper Core

### `ParticleEffectPlayer`
Clase estática que estandariza las dos aproximaciones:
- `PlayInPlace(ParticleSystem, Vector2)`: Ubica el sistema y lo reproduce.
- `InstantiateAndPlay(ParticleSystem prefab, Vector2)`: Genera un nuevo objeto autónomo a partir del Prefab.

## Principios Cumplidos

- **Sin Event Bus Global**: Mantiene flujos de comunicación y acoplamientos explícitos.
- **Data-Driven**: Cada GameObject (Character, Proyectil, Item) define sus propios Prefabs de partículas en el inspector.
- **Sin Sincronización Innecesaria**: Solo se propaga la mínima señal necesaria, los clientes resuelven la presentación leyendo el último snapshot garantizado por Fusion.

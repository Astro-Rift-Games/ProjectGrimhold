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

## Impactos melee confirmados

Solo el **cliente del atacante** emite partículas específicas en el `HitPoint` confirmado (superficie de contacto, no centro del objetivo), ajustado a la cuadrícula de 1/16 unidades. `PlayerCombatNetworkController.Render` consume la confirmación ya secuenciada del RPC State Authority → Input Authority; dentro del deduplicado existente llama a `MeleeImpactParticleEmitter` por separado de hitstop/reacción. Solo acepta `ConfirmedImpact`, `IsMeleeImpact`, daño aplicado positivo, arma melee resoluble por catálogo y objetivo character registrado distinto del atacante. Un golpe mortal también emite si el objetivo sigue registrado; breakables, escenario, daño rechazado/cero y ranged no entran en esta ruta.

### Configuración visual

`WeaponDefinition.Presentation.ImpactParticles` referencia opcionalmente un `MeleeImpactParticleProfile` validado. Un perfil nulo conserva las partículas genéricas. Los tres perfiles de `Assets/Scriptable Objects/Combat/ImpactParticles/` reutilizan la clasificación de las reacciones melee:

| Perfil | Armas | Cantidad | Velocidad (u/s) | Cono | Tamaño (px) | Vida (s) |
| --- | --- | --- | --- | --- | --- | --- |
| Light | Rondel Dagger, Magic Cinquedea | 5–7 | 13–15 | 70° | 2 | .32–.40 |
| Medium | Arming Sword, Magic Sword, Rapier, Spellbook | 8–11 | 15–18 | 80° | 2–3 | .36–.46 |
| Heavy | Long Sword, Zweihander, Great Hammer | 14–18 | 17–21 | 90° | 3–4 | .40–.52 |

Los perfiles conservan una chispa cálida clara `(1, .92, .65, 1)` como fallback. Antes de emitir, el controlador consulta `CharacterDamageParticlePresenter.TryGetMeleeImpactColor(out color)` en el objetivo. `_overrideMeleeImpactColor` habilita el color serializado `_meleeImpactColor`: esqueleto base y ranged heredado `( .82, .82, .85, 1)`, slime azul `(.208, .631, .757, 1)`, verde `(.349, .757, .208, 1)` y rojo `(.757, .208, .369, 1)`. Los slimes guardan overrides de variante; NetworkPlayer no habilita el override y mantiene la chispa cálida. `Emit` recibe un `Color?` opcional y escribe `EmitParams.startColor`, sin modificar el perfil compartido ni las partículas genéricas. Las gotas nacen sobre la superficie cercana de un cuerpo de ~1 u y se comportan como una salpicadura viscosa: la velocidad de la tabla es la de salida y el prefab aplica drag lineal `k = 8` (Limit Velocity over Lifetime, sin límite ni dampen), de modo que `v(t) = v0·e^(−k·t)` y el recorrido es `v0/k·(1 − e^(−k·t))`. La gota más lenta debe salir del cuerpo (1 u) dentro del 40% de su vida y asentarse a ≥ 1.25 u (20 px), pasando la mayor parte de su vida sobre el suelo; lo verifica `MeleeImpactParticleProfileTests`. Se conserva el test de contraste del color fallback (no dominante en rojo), antes camuflado con el flash rojo genérico; los colores de objetivo son deliberados y el melee local ahora usa una silueta blanca unlit. Cantidad, velocidad, dispersión, tamaño, PPU, vida y color son ajustables sin cambiar gameplay. Cada partícula toma una dirección dentro de ±medio cono alrededor de `HitDirection`; dirección cero/no finita produce un círculo completo. Los tamaños son píxeles enteros / PPU (16). La validación exige rangos ordenados y finitos, cantidad 1–256, tamaño 1–4 px, vida .001–.6 s y color normalizado.

### Reutilización, tiempo y limpieza

Los perfiles comparten `Assets/Prefabs/VFX/MeleeImpactParticles.prefab`: cuadrados billboard sin textura, material **Sprite-Unlit-Default** de URP reutilizado (la iluminación 2D no apaga el color del objetivo en el dungeon oscuro), sorting **VFX/0** y alpha opaca (1) hasta el 80% de la vida, con fade solo al final. No se añaden sprites ni materiales. La cuadrícula y los tamaños mantienen la escala de 16 PPU; la rasterización final depende de la cámara y requiere validación visual.

El emisor es una clase plana propiedad del controlador: crea perezosamente **una instancia por prefab**, sin padre y con simulación mundial; reutiliza `EmitParams` sin allocations por contacto. Posición/velocidad por partícula permiten contactos simultáneos en múltiples objetivos sin mover partículas anteriores. Máximo 256 partículas vivas por emisor: saturar ese límite puede omitir partículas adicionales. No se instancia/destruye por golpe; `Spawned` reinicia y `Despawned`/`OnDestroy` liberan las instancias. El efecto no depende del target ni de su Animator tras emitirlo.

Las partículas avanzan con tiempo normal (`Time.deltaTime`), no con el reloj mapeado del ataque: **continúan durante el hitstop**, sin congelarse ni rebobinar. No se cambia `Time.timeScale`, daño, knockback, detección, cooldown, timings, hitstop ni hit reaction; no hay nuevos eventos globales ni estado de red.

### Coalescencia del daño genérico

Tras emitir, el controlador llama a `CharacterDamageParticlePresenter.AbsorbConfirmedMeleeImpact(appliedDamage)` del objetivo, únicamente en ese cliente. Health replica un agregado sin fuente, por lo que la correlación es **por cantidad** dentro de una ventana serializada (por defecto .15 s):

- Si un burst genérico se reprodujo dentro de la ventana y su reducción de Health es explicable por el daño melee confirmado (`reducción <= AppliedDamage`), se limpia **ese burst**. Si la reducción fue mayor, incluía otra fuente y el burst genérico se conserva.
- En caso contrario se arma un presupuesto de absorción igual a `AppliedDamage`; varios impactos melee confirmados sobre el mismo objetivo dentro de la ventana suman su daño, porque pueden llegar en una sola reducción observada.
- Una reducción posterior dentro de la ventana se absorbe solo si el presupuesto restante la explica, y lo consume. Una reducción mayor que el presupuesto (daño independiente incluido), o fuera de la ventana, reproduce las partículas genéricas y descarta el presupuesto.
- `OnDisable` limpia el estado local. Otros peers nunca reciben esta llamada y mantienen las partículas genéricas. Las demás fuentes siguen el camino Health existente.

Límite residual: un daño independiente que llegue dentro de la ventana con una cantidad menor o igual al presupuesto melee pendiente no se distingue del golpe confirmado y se coalesce. No se bloquean todas las reducciones durante una ventana ni se modifica el estado autoritativo.

### Pruebas y comprobaciones manuales

EditMode cubre validación, catálogo/pesos, muestreo (punto, dirección, cantidad, tamaño y vida), reutilización/limpieza del emisor y absorción anterior/posterior/caducada/de un uso. PlayMode comprueba el release/RPC melee equipado, ausencia de emisor en ranged, filtrado/deduplicado en Render, múltiples contactos y limpieza al despawn sin cambiar Health. La aprobación estética, lectura Light/Medium/Heavy y verificación Host + clientes/proxies reales siguen siendo manuales; los tests de un runner no prueban esos casos visuales multijugador.

Véase también [PlayerCombatArchitecture — Melee impact particles](PlayerCombatArchitecture.md#melee-impact-particles).

## Helper Core

### `ParticleEffectPlayer`
Clase estática que estandariza las dos aproximaciones:
- `PlayInPlace(ParticleSystem, Vector2)`: Ubica el sistema y lo reproduce.
- `InstantiateAndPlay(ParticleSystem prefab, Vector2)`: Genera un nuevo objeto autónomo a partir del Prefab.

## Principios Cumplidos

- **Sin Event Bus Global**: Mantiene flujos de comunicación y acoplamientos explícitos.
- **Data-Driven**: Cada GameObject (Character, Proyectil, Item) define sus propios Prefabs de partículas en el inspector.
- **Sin Sincronización Innecesaria**: Solo se propaga la mínima señal necesaria, los clientes resuelven la presentación leyendo el último snapshot garantizado por Fusion.

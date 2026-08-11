# Enemy Patrol & Navigation - Etapa 2

## 1. Resumen de cambios realizados

* **EnemyPatrolRoute**:
  * Se creó un nuevo componente `EnemyPatrolRoute` que funciona únicamente como contenedor de datos para los waypoints. Soporta navegación circular utilizando módulo matemático.
* **EnemyStateType & EnemyFSM**:
  * Se agregó el estado `Patrol = 4` al enumerador `EnemyStateType`.
  * Se creó la clase `EnemyPatrolState` que implementa `IEnemyState`.
  * Se registró el nuevo estado en `EnemyFSM`.
* **Transiciones de estado**:
  * `EnemyIdleState`: Ahora transita automáticamente a `Patrol` si el enemigo tiene una ruta asignada (`HasPatrolRoute`).
  * `EnemyChaseState` y `EnemyAttackState`: Al perder el objetivo, ahora transitan de vuelta a `Patrol` en lugar de `Idle` si el enemigo tiene una ruta configurada.
* **EnemyMovementAIController**:
  * Se agregó el manejo de la variable de red `PatrolWaypointIndex` para almacenar el progreso de la patrulla del enemigo.
  * Se implementó `ComputePatrolDirection` para calcular el vector de movimiento hacia el waypoint actual. Cuando la distancia al waypoint es menor a `_waypointReachRadius`, el índice avanza y se recalcula el nuevo vector inmediatamente para evitar interrupciones de 1 tick.
  * Se actualizó `HasPatrolRoute` para depender dinámicamente de la configuración real (`_patrolRoute != null && _patrolRoute.HasWaypoints`).

## 2. Configuración en Unity Editor

En el prefab del enemigo (`EnemyMovementAIController`):
* **Patrol**:
  * **`_patrolRoute`**: Arrastrar la referencia al componente `EnemyPatrolRoute` (puede estar en un objeto hijo o separado, preferiblemente inyectado/asignado en el prefab o en la instancia en escena).
  * **`_waypointReachRadius`**: Distancia de tolerancia para considerar que el enemigo alcanzó el waypoint y debe avanzar al siguiente (recomendado: 0.3f a 0.5f).

Para configurar la ruta (`EnemyPatrolRoute`):
* Crear un GameObject en la escena (o dentro de un contenedor en la jerarquía del nivel) y añadirle `EnemyPatrolRoute`.
* Asignar Transforms a la lista `_waypoints` en el orden deseado.
* Los Gizmos (líneas cian y esferas) dibujarán la ruta completa de forma circular en el editor.

## 3. Validación

* **Patrol (PlayMode)**:
  * El enemigo sin ruta configurada entra en `Idle` y no se mueve.
  * El enemigo con ruta configurada entra en `Patrol` inmediatamente tras hacer spawn y comienza a moverse hacia el primer waypoint.
  * Al alcanzar un waypoint, el enemigo debe dirigirse suavemente al siguiente sin detenerse y repetir la ruta infinitamente.
* **Chase & Return**:
  * Al detectar un objetivo durante la patrulla, el enemigo entra en `Chase` o `Attack`.
  * Al perder el objetivo, el enemigo retorna a `Patrol` y reanuda el movimiento hacia el waypoint en el que se había quedado (el índice no se reinicia al salir del estado, respetando el determinismo y la intención de patrullaje).
* **Networking / Resimulation**:
  * El índice de waypoint `PatrolWaypointIndex` es `[Networked]` y es avanzado únicamente por el State Authority.

## 4. Commit

**Título:**
`feat: implementar sistema de patrullaje con waypoints (Enemy Patrol Etapa 2)`

**Descripción:**
```
- Añade componente de datos EnemyPatrolRoute con soporte para rutas circulares.
- Añade EnemyPatrolState al FSM e inyecta soporte en EnemyMovementAIController.
- Las transiciones desde Idle/Chase/Attack ahora vuelven a Patrol si hay ruta.
- Implementa ComputePatrolDirection con PatrolWaypointIndex [Networked].
- Evita 1-tick stop recalculando la dirección hacia el siguiente waypoint de inmediato.
```

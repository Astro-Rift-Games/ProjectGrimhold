# Enemy Patrol & Navigation - Etapa 1

## 1. Resumen de cambios realizados

* **EnemyMovementAIController**:
  * Se eliminó `_moveSpeed` no serializado y `Random.Range` en simulación (los enemigos sin ruta ahora se quedan estáticos).
  * Se reemplazó la costosa búsqueda `FindObjectsByType` por `Physics2D.OverlapCircleNonAlloc` con un buffer pre-alocado (`_overlapBuffer`).
  * **Target Detection**: Se dividió en dos caminos lógicos. Si no hay objetivo, se ejecuta un escaneo ligero cadenciado por un `ScanTimer` (0.1s de intervalo default). Si hay un objetivo, se evalúa distancia y LOS directamente en cada tick (O(1)).
  * **Rangos separados**: Se implementaron `_detectionRange` (para adquirir el objetivo) y `_disengageRange` (para perderlo), evitando el "flickering" en el borde.
  * **Tolerancia de pérdida**: Se agregó `_pursuitLostGraceTicks` (10 ticks) que mantiene la persecución brevemente si se pierde el LOS pero el jugador sigue dentro del `_disengageRange`.
  * **Determinismo de selección**: Si el escaneo detecta múltiples jugadores, se elige el de menor distancia. Si hay empate, se usa `EntityId.Value` como criterio determinista estable. Se agregó protección para ignorar múltiples colliders de una misma entidad (hitboxes).
  * Se usan `Physics2D.Linecast` para evaluar el LOS.
* **Arquitectura**:
  * Se actualizó `Docs/Architecture/EnemyFSMArchitecture.md` para reflejar la separación entre sensores (Controller) y transiciones (FSM).

*(Nota: Patrol y Obstacle Avoidance corresponden a las Etapas 2 y 3 respectivamente, por lo que aún no están implementados en esta etapa).*

## 2. Configuración en Unity Editor

En el prefab del enemigo (`EnemyMovementAIController`):
* **Movement**:
  * Ajustar `_patrolSpeed` y `_pursuitSpeedMultiplier`.
* **Detection**:
  * `_detectionRange`: Distancia para empezar a perseguir (recomendado: 6).
  * `_disengageRange`: Distancia para dejar de perseguir (recomendado: 8+). Debe ser `>= _detectionRange`.
  * `_pursuitLostGraceTicks`: Ticks de gracia sin LOS antes de perder el objetivo (recomendado: 10).
* **Obstacle Detection**:
  * `_obstacleLayer`: Asegurarse de que esté asignado **únicamente** a la geometría estática del nivel.
* **Scan**:
  * `_scanInterval`: Frecuencia del OverlapCircle (recomendado: 0.1).
  * `_playerLayer`: Asignar a la layer donde se encuentran los colliders/hitboxes de los jugadores.

Los Gizmos en la escena dibujan el rango de detección en **verde**, el rango de desenganche en **amarillo** y el rango de ataque en **rojo**.

## 3. Validación

* **Target Detection (PlayMode)**:
  * El enemigo en Idle se queda estático.
  * El jugador entra en el círculo verde (`_detectionRange`) y tiene LOS: el enemigo inicia persecución.
  * El jugador sale del círculo amarillo (`_disengageRange`): el enemigo detiene la persecución inmediatamente.
  * El jugador se esconde detrás de un obstáculo (estando en el círculo amarillo): el enemigo detiene la persecución tras 10 ticks (0.16s).
  * Varios jugadores entran al área: el enemigo ataca al más cercano de forma estable (sin cambiar aleatoriamente entre ellos).
* **Networking / Resimulation**:
  * Ejecutar el juego con Host y Cliente(s).
  * El State Authority ejecuta los raycasts y detección. El Client (Proxy) simplemente interpola y reproduce el movimiento (IsMoving, FacingDirection, etc) sin ejecutar ningún `OverlapCircle`.

## 4. Commit

**Título:**
`fix: optimizar y hacer determinista la detección de objetivos (Enemy Patrol Etapa 1)`

**Descripción:**
```
- Reescribe EnemyMovementAIController separando lógica de sensores de FSM.
- Elimina FindObjectsByType y Random.Range de FixedUpdateNetwork.
- Introduce OverlapCircleNonAlloc determinista con ScanTimer (0.1s default) usando buffer pre-alocado.
- Separa _detectionRange y _disengageRange; añade _pursuitLostGraceTicks (10 ticks) para evitar flickering de borde.
- Añade _playerLayer y selección determinista (menor distancia + desempate por EntityId.Value).
```

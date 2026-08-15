# Etapa 4: EnemyPathfindingNavigator

## Instrucciones de Configuración en Unity
1. Abre el prefab `NetworkEnemy` (o los equivalentes base de todos tus enemigos).
2. Añade el componente `EnemyPathfindingNavigator`.
3. Asigna el asset `PathfindingGridConfig` (creado en la etapa 1) al campo `_config` del componente.
4. Ajusta los parámetros según las necesidades del enemigo:
   - **Repath Interval Seconds:** `0.5` (Recomendado). Aumentar si se necesita mayor rendimiento y se toleran persecuciones con más "lag", disminuir si el jugador esquiva muy rápido.
   - **Waypoint Reach Radius:** `0.3` (Debe ser similar al usado en patrulla).
   - **Target Moved Threshold:** `1.5`. (Forzará un recalculo antes del timer si el jugador se movió esta cantidad de distancia).

## Información para el Commit
```text
feat(pathfinding): add EnemyPathfindingNavigator component

- Implemented lifecycle management for enemy path requests.
- Integrated Runner.Tick based timers to ensure idempotent execution during Fusion resimulation.
- Added visual Gizmos for debugging active paths and current waypoints.
- Handled path invalidation on target movement or timer expiration.
```

## Expectativa de Validación
- Selecciona un enemigo en tiempo de ejecución (Play mode) y asegúrate de que el componente `EnemyPathfindingNavigator` recibe el runner mediante la invocación manual en el código (`EnemyMovementAIController.Spawned`).
- Aún no moverá al enemigo por su cuenta (eso ocurre en la Etapa 5), pero con **Show Gizmos** activado podrías comenzar a ver líneas color Cian si fuerzas una llamada a `GetDirectionToTarget()`.

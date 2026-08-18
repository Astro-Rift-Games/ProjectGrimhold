# Etapa 5: Integración con Chase

## Instrucciones de Configuración en Unity
1. Selecciona el prefab `NetworkEnemy`.
2. En el componente `EnemyMovementAIController`, busca la sección de **Dependencies**.
3. Arrastra el propio GameObject (donde está el `EnemyPathfindingNavigator` añadido en la etapa 4) al campo `_pathfindingNavigator`.

## Información para el Commit
```text
feat(pathfinding): integrate navigator into Chase movement

- Added EnemyPathfindingNavigator dependency to EnemyMovementAIController.
- Initialized navigator during Spawned() execution.
- Modified ComputePursuitDirection to query the navigator for the next waypoint direction.
- Ensured path is invalidated when pursuit ends (ClearCurrentTarget).
```

## Expectativa de Validación
- Entra en Play Mode (como Host).
- Acércate a un enemigo para desencadenar el estado de `Chase`.
- Muévete de tal forma que haya un obstáculo entre tú y el enemigo.
- El enemigo no debería atascarse contra el obstáculo, sino que lo rodeará utilizando el camino óptimo dibujado por la ruta Cian de los Gizmos.

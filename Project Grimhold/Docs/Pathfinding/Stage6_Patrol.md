# Etapa 6: Integración con Patrol

## Instrucciones de Configuración en Unity
- La dependencia del componente ya fue asignada en la Etapa 5, por lo tanto no se requiere configuración manual extra en Unity para esta etapa.

## Información para el Commit
```text
feat(pathfinding): integrate navigator into Patrol movement

- Modified ComputePatrolDirection to query EnemyPathfindingNavigator instead of direct line-of-sight direction to waypoints.
- Added navigator invalidation when the current patrol waypoint index changes, forcing an immediate path recalculation toward the new waypoint.
```

## Expectativa de Validación
- Entra en Play Mode (como Host).
- Observa a un enemigo en estado `Patrol` (sin acercarte).
- Si sus nodos de patrulla están ubicados de manera tal que exista una pared entre medio, el enemigo ya no se chocará contra la pared tratando de llegar en línea recta, sino que caminará por pasillos o puertas para llegar a su siguiente punto de patrulla.

# Etapa 3: AStarPathSolver

## Instrucciones de Configuración en Unity
- Ninguna configuración en el Editor de Unity. Esta etapa consta de clases de soporte puramente C# que no heredan de `MonoBehaviour`.

## Información para el Commit
```text
feat(pathfinding): implement allocation-free AStarPathSolver and BinaryMinHeap

- Created BinaryMinHeap to efficiently process the A* open list.
- Implemented stateless AStarPathSolver with pre-allocated buffers.
- Supports octile heuristic for 8-directional movement.
- Integrated path smoothing via Physics2D.CircleCast to handle sub-node precision and remove redundant waypoints.
```

## Expectativa de Validación
- Esta etapa no es testeable visualmente de forma aislada, pero puedes instanciar temporalmente un `AStarPathSolver` desde un script de prueba.
- Confirma que la creación de un solver aloca memoria una sola vez en el Profiler. Las llamadas consecutivas a `FindPath()` deben mostrar **0 Bytes** de alocación de memoria GC.

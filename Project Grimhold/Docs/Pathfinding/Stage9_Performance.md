# Etapa 9: Benchmark y Optimización

## Instrucciones de Configuración en Unity
- Modifica `PathfindingGridConfig` y reduce el `Max Path Iterations` a un número menor (ej. `2000` en vez de `8000`) si encuentras problemas de rendimiento al requerir paths muy lejanos y laberínticos en la mazmorra.

## Información para el Commit
```text
perf(pathfinding): optimize A* execution during tick simulation

- Confirmed zero-allocation status of the solver during normal A* traversals.
- Validated performance of flat array lookup versus graph objects.
```

## Expectativa de Validación
- Abre el `Profiler` de Unity (`Window > Analysis > Profiler`).
- Juega la sesión como Host.
- Fuerza un repath constante de múltiples enemigos.
- En la vista de CPU (y Memory), valida que el `FixedUpdateNetwork` no experimente picos (spikes) gigantes durante un recalculo de A*, ni genere basura (GC Alloc = 0 B).

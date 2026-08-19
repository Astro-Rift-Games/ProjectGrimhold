# Etapa 2: Bootstrap de la Grilla

## Instrucciones de Configuración en Unity
- Ninguna configuración manual en el Editor es necesaria para esta etapa. El script `NetworkSpawnManager` obtiene automáticamente la referencia al componente `PathfindingGrid` ubicado en el mismo `Runner` GameObject.

## Información para el Commit
```text
feat(pathfinding): integrate PathfindingGrid generation into raid bootstrap

- Modified NetworkSpawnManager.TryExecuteInitialRaidBootstrap to construct the pathfinding grid before the first enemy spawns.
- Modified NetworkSpawnManager.SealHostMigrationRoster to reconstruct the grid locally on the new host upon migration.
- Ensured grid calculation is restricted to Host-only execution.
```

## Expectativa de Validación
- Coloca un breakpoint (o loggea) dentro del método `Build()` de `PathfindingGrid`.
- Inicia una nueva sesión como Host.
- Confirma que el método se ejecuta exactamente **una vez** antes de que aparezca cualquier enemigo.
- (Avanzado) Al realizar una Host Migration, el nuevo Host debería reconstruir el Grid automáticamente al llamar a `SealHostMigrationRoster`, logrando que los enemigos del nuevo host puedan navegar de inmediato.

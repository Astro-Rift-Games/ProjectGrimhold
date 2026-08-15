# Etapa 10: Validación Multiplayer y Arquitectura

## Instrucciones de Configuración en Unity
- Realiza una validación completa de una sesión Hosted con clientes.
- No hay parámetros a ajustar, sólo pruebas.

## Información para el Commit
```text
docs(pathfinding): document A* architecture and State Authority model

- Created Docs/Architecture/PathfindingArchitecture.md detailing separation of concerns.
- Documented Host Migration rebuild rules.
- Validated server-only execution pattern for proxy clients.
```

## Expectativa de Validación
- Compila el proyecto con múltiples clientes.
- Lanza un Host y 1 Cliente.
- Asegúrate de que los enemigos puedan perseguir y moverse con pathfinding correctamente en la visión de ambos clientes, a pesar de que el código solo corre en el Host (validando así que el Input de movimiento direccional está siendo replicado por el `NetworkTransform` o estado de locomoción correctamente y los proxys no requieren la grilla).

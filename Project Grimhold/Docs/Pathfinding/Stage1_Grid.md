# Etapa 1: PathNode, PathfindingGridConfig y PathfindingGrid

## Instrucciones de Configuración en Unity
1. En Unity, dirígete a `Assets/ScriptableObjects/` (créala si no existe).
2. Haz clic derecho -> **Create -> Grimhold -> Pathfinding -> Grid Config**.
3. Nombra al archivo como `PathfindingGridConfig`.
4. Configura los siguientes valores en el Inspector del asset:
   - **Node Size:** `0.5`
   - **Agent Radius:** `0.35`
   - **Obstacle Layer:** Selecciona la capa que corresponde a los muros/obstáculos en el Tilemap.
   - **Max Path Iterations:** `8000` (o ajusta según el tamaño de tu nivel).
5. En la escena principal del nivel/calabozo, crea un nuevo **GameObject vacío** y nómbralo `PathfindingGrid`.
6. Añade el componente `PathfindingGrid` a este nuevo GameObject.
7. Asigna el `PathfindingGridConfig` recién creado al campo `_config` del componente `PathfindingGrid`.

## Información para el Commit
```text
feat(pathfinding): implement PathNode, PathfindingGridConfig, and PathfindingGrid

- Added PathNode struct for zero-allocation A* working data.
- Created PathfindingGridConfig ScriptableObject for shared tuning.
- Implemented PathfindingGrid MonoBehaviour to evaluate walkable area using Physics2D.OverlapBox and agent radius.
- Includes Gizmo support for editor debugging.
```

## Expectativa de Validación
- En Unity, con el modo de ejecución (Play mode) pausado justo después de inicializar la sesión, selecciona el GameObject que tiene el `PathfindingGrid`.
- Activa el checkbox de **Debug -> Show Gizmos**.
- Deberías ver un grid de cuadrados verdes cubriendo el suelo del calabozo y cuadrados rojos en los bordes de los muros y obstáculos estáticos. Los rojos representan nodos no caminables o demasiado cerca de un muro (teniendo en cuenta el `Agent Radius`).

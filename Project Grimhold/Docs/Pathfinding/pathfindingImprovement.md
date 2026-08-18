# Fix Pathfinding Corner-Cutting Loop

## Contexto y Causa Raíz
El sistema entra en un bucle infinito (repath loop) porque `SmoothPath` elimina nodos intermedios asumiendo que los atajos directos son seguros utilizando un radio de evaluación (`0.35`) menor al colisionador físico del enemigo (`0.4`). Esto provoca que el enemigo intente atravesar un atajo demasiado ajustado, colisione físicamente (o active `EnemyObstacleAvoidance`), ingrese a zonas prohibidas y fuerce un recálculo que volverá a generar el mismo atajo inválido.

## 1. Justificación del Radio de Suavizado
No utilizaremos multiplicadores arbitrarios ni valores mágicos combinados. En su lugar, reflejaremos explícitamente los conceptos físicos en `PathfindingGridConfig.cs`:

*   **`PhysicalColliderRadius`**: Representa el tamaño real del enemigo. Si en el futuro cambia el tamaño del prefab, el diseñador solo actualiza este valor. Para el enemigo actual: `0.4`.
*   **`PathSmoothingSafetyMargin`**: Un margen para absorber imprecisiones de coma flotante y prevenir fricciones/grazing en el motor de físicas. Un valor de `0.02` es adecuado porque es el doble del `defaultContactOffset` estándar de Unity 2D (`0.01`), proveyendo 1-2 frames físicos de margen sin alterar significativamente la trayectoria.
*   **`PathSmoothingRadius`**: Propiedad calculada como `PhysicalColliderRadius + PathSmoothingSafetyMargin` (`0.42`).

Esta separación garantiza que cualquier cambio futuro en el colisionador del agente mantendrá automáticamente el margen de seguridad correcto sin requerir conocimiento interno del sistema de pathfinding.

## 2. Precisión del Criterio Geométrico
El sistema debe garantizar exactamente lo siguiente:
**`SmoothPath` no debe eliminar nodos intermedios cuando el atajo no es físicamente seguro para el collider real del enemigo.**

*   **Grid:** `PathfindingGrid` asegura que sus nodos son transitables (distancia >= 0.6 gracias al `OverlapBox`).
*   **SmoothPath:** Garantiza que, si se traza una línea recta entre dos nodos seguros, un barrido circular continuo (`CircleCast`) del tamaño físico del agente (`PathSmoothingRadius`) no intersectará ningún obstáculo. Si hay intersección (ej. una esquina convexa muy cerrada), el atajo se descarta y el enemigo navegará conservando el nodo intermedio de la cuadrícula, que rodea la esquina de forma segura.

## 3. Mantener Responsabilidades Arquitectónicas
Este cambio es estrictamente un ajuste paramétrico en el planificador (A*). 
*   **Se confirma explícitamente que no se modificará `EnemyObstacleAvoidance` ni el sistema de movimiento físico.**
*   El pathfinding seguirá proveyendo rutas puras. El navegador (`EnemyPathfindingNavigator`) seguirá gestionando su ciclo de vida.
*   La disminución del steering/evasion local será simplemente el **resultado (validación)** de haberle entregado una ruta físicamente transitable al motor, no una nueva responsabilidad que el pathfinding deba calcular activamente.

## Proposed Changes

### [MODIFY] `PathfindingGridConfig.cs` (file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Pathfinding/PathfindingGridConfig.cs)
Añadir variables separadas para el radio físico y el margen de seguridad:
```csharp
    [Header("Path Smoothing")]
    [Tooltip("El radio del CircleCollider2D físico del enemigo actual. Utilizado para validar atajos rectos.")]
    [SerializeField, Min(0f)]
    private float _physicalColliderRadius = 0.4f;

    [Tooltip("Margen de seguridad extra añadido al radio del collider para evitar fricción por errores de precisión en esquinas (Recomendado: 0.02).")]
    [SerializeField, Min(0f)]
    private float _pathSmoothingSafetyMargin = 0.02f;

    /// <summary>Radio final utilizado por el CircleCast de SmoothPath.</summary>
    public float PathSmoothingRadius => _physicalColliderRadius + _pathSmoothingSafetyMargin;
```
*(Nota: `_agentRadius = 0.35` se mantiene intacto y con su propósito original exclusivo de Minkowski erosion).*

### [MODIFY] `EnemyPathfindingNavigator.cs` (file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Pathfinding/EnemyPathfindingNavigator.cs)
*   Modificar la llamada `_solver.FindPath(...)` para pasar `_config.PathSmoothingRadius` en lugar de `_config.AgentRadius`.
*   Añadir instrumentación diagnóstica (Loop Detection).

### [MODIFY] `AStarPathSolver.cs` (file:///e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project%20Grimhold/Assets/Scripts/Pathfinding/AStarPathSolver.cs)
*   Renombrar el parámetro `agentRadius` a `pathSmoothingRadius` en los métodos `FindPath`, `SmoothPath` y `HasDirectPath` para que el contrato del método refleje su propósito exacto.

## Loop Detection (Diagnostic Only)
Se añadirá una lógica puramente diagnóstica en `EnemyPathfindingNavigator` para alertar en consola sobre posibles regresiones sin afectar la simulación:
*   **Variables de rastreo:** `_repathCountInWindow`, `_windowStartTick`, `_positionAtWindowStart`.
*   **Ventana temporal:** 3 segundos (`3.0f / Runner.DeltaTime` ticks).
*   **Umbral de repaths:** Más de `5` recálculos en una sola ventana.
*   **Progreso mínimo:** Menos de `0.5` unidades de distancia lineal desde la posición inicial de la ventana.
*   **Reset:** Si la ventana expira, o si la distancia superó `0.5`, se reinicia el conteo y la ventana.
*   **Acción:** Si `repaths > 5` y `distancia < 0.5`, se dispara un `Debug.LogWarning("Pathfinding repath loop detected...")` y se resetea la ventana para no spamear la consola.

## Verification Plan

### Prueba de Regresión Específica del Bug
1. Mover al enemigo deliberadamente muy lejos de su `PatrolRoute`.
2. Posicionar al enemigo de modo que su ruta óptima de regreso pase rasante por una esquina convexa (esquina de 90° de un muro sólido).
3. **Validar geométricamente:** Observar (vía Gizmos) que `SmoothPath` rechaza el atajo ceñido a la pared y conserva el nodo intermedio de la grilla que lo hace rodear la esquina abriéndose.
4. **Validar movimiento:** El enemigo debe moverse hacia el nodo intermedio, doblar la esquina y continuar a su patrulla sin colisionar físicamente con la esquina y sin invadir la zona prohibida.
5. **Validar estabilidad:** No debe dispararse la alerta de recálculo (Loop Detection) en la consola.

### Verificación de No-Regresión de Conectividad
*   Colocar al enemigo en un pasillo estrecho (ej. ancho visual mínimo que permita pasar a un collider de 0.4).
*   Verificar que A* sigue encontrando la ruta hacia afuera del pasillo, demostrando que `AgentRadius = 0.35` sigue permitiendo que los nodos se generen correctamente y conecten, y que el suavizado más estricto simplemente fuerza a caminar por los nodos centrales en lugar de bloquear la ruta.

---

**Veredicto:** **APPROVE — Ready for Development**
El plan cubre todos los requerimientos técnicos, previene loops forzando un suavizado físicamente seguro, mantiene intacta la permisividad de la grilla y respeta la arquitectura sin introducir "magic numbers".
